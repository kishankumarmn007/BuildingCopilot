import { useEffect, useRef, useState } from 'react'
import {
  ArrowUp,
  Building2,
  Check,
  ChevronDown,
  CirclePlus,
  Copy,
  MessageSquareText,
  PanelLeftClose,
  PanelLeftOpen,
  Sparkles,
  Thermometer,
  Wind,
  X,
} from 'lucide-react'
import { useAuth } from './auth/useAuth.js'
import './App.css'

const STORAGE_KEY = 'building-copilot-conversations'

const suggestions = [
  { icon: Wind, label: 'Room is hot, but the HVAC is running' },
  { icon: Thermometer, label: 'Explain what a 29.5 C room reading means' },
  { icon: Sparkles, label: 'Build a quick HVAC inspection checklist' },
]

function createConversation() {
  return {
    id: crypto.randomUUID(),
    title: 'New conversation',
    messages: [],
    updatedAt: Date.now(),
  }
}

function readConversations() {
  try {
    const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) || '[]')
    return Array.isArray(saved) && saved.length ? saved : [createConversation()]
  } catch {
    return [createConversation()]
  }
}

function App() {
  const { user, isReady, error: authError, signIn, signOut, getAccessToken } = useAuth()
  const [conversations, setConversations] = useState(readConversations)
  const [activeId, setActiveId] = useState(() => conversations[0].id)
  const [draft, setDraft] = useState('')
  const [isSending, setIsSending] = useState(false)
  const [sidebarOpen, setSidebarOpen] = useState(false)
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false)
  const [copiedId, setCopiedId] = useState(null)
  const messagesEndRef = useRef(null)
  const textareaRef = useRef(null)

  const activeConversation = conversations.find((item) => item.id === activeId)
  const messages = activeConversation?.messages ?? []
  const userName = user?.profile.name
    || user?.profile.preferred_username
    || user?.profile.email
    || 'Building Copilot user'
  const userInitials = userName.split(/\s+/).slice(0, 2).map(part => part[0]?.toUpperCase()).join('')

  useEffect(() => {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(conversations))
  }, [conversations])

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' })
  }, [activeId, messages.length, isSending])

  function startConversation() {
    const conversation = createConversation()
    setConversations((current) => [conversation, ...current])
    setActiveId(conversation.id)
    setDraft('')
    setSidebarOpen(false)
  }

  function toggleSidebar() {
    if (window.matchMedia('(max-width: 760px)').matches) {
      setSidebarOpen((open) => !open)
    } else {
      setSidebarCollapsed((collapsed) => !collapsed)
    }
  }

  async function sendMessage(value = draft) {
    const question = value.trim()
    if (!question || isSending || !activeConversation) return

    const userMessage = { id: crypto.randomUUID(), role: 'user', content: question }
    const priorMessages = activeConversation.messages
    const conversationPrompt = [...priorMessages, userMessage]
      .map((message) => `${message.role === 'user' ? 'User' : 'Assistant'}: ${message.content}`)
      .join('\n\n')
    const conversationId = activeConversation.id

    setDraft('')
    if (textareaRef.current) textareaRef.current.style.height = 'auto'
    setIsSending(true)
    setConversations((current) => current.map((conversation) => {
      if (conversation.id !== conversationId) return conversation
      return {
        ...conversation,
        title: conversation.messages.length ? conversation.title : question.slice(0, 42),
        messages: [...conversation.messages, userMessage],
        updatedAt: Date.now(),
      }
    }))

    try {
      const accessToken = await getAccessToken()
      const response = await fetch('/api/chat', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${accessToken}`,
        },
        body: JSON.stringify({ question: conversationPrompt }),
      })
      const payload = await response.json().catch(() => ({}))
      if (!response.ok) {
        if (response.status === 401) {
          throw new Error('Your sign-in has expired. Sign out and sign in again.')
        }
        if (response.status === 403) {
          throw new Error('Your account is not authorized to use the API. Sign out and sign in again; if this continues, check that your Keycloak user has the building-user role.')
        }
        throw new Error(payload.detail || payload.message || `Request failed (${response.status})`)
      }

      const answer = payload.answer?.trim()
      if (!answer) throw new Error('The API returned an empty answer.')

      setConversations((current) => current.map((conversation) => conversation.id === conversationId
        ? { ...conversation, messages: [...conversation.messages, { id: crypto.randomUUID(), role: 'assistant', content: answer }], updatedAt: Date.now() }
        : conversation))
    } catch (error) {
      setConversations((current) => current.map((conversation) => conversation.id === conversationId
        ? { ...conversation, messages: [...conversation.messages, { id: crypto.randomUUID(), role: 'error', content: error.message }], updatedAt: Date.now() }
        : conversation))
    } finally {
      setIsSending(false)
      textareaRef.current?.focus()
    }
  }

  function handleSubmit(event) {
    event.preventDefault()
    sendMessage()
  }

  function handleTextareaChange(event) {
    setDraft(event.target.value)
    event.target.style.height = 'auto'
    event.target.style.height = `${Math.min(event.target.scrollHeight, 180)}px`
  }

  async function copyMessage(message) {
    await navigator.clipboard.writeText(message.content)
    setCopiedId(message.id)
    window.setTimeout(() => setCopiedId(null), 1400)
  }

  if (!isReady) {
    return <main className="auth-screen" role="status">Connecting to secure sign-in…</main>
  }

  if (!user) {
    return (
      <main className="auth-screen">
        <section className="auth-panel">
          <div className="welcome-emblem"><Building2 size={22} /></div>
          <p className="eyebrow">BUILDING COPILOT</p>
          <h1>Sign in to continue</h1>
          <p className="welcome-copy">Use your organization account to access building operations and maintenance guidance.</p>
          {authError && <p className="auth-error" role="alert">{authError}</p>}
          <button className="auth-button" onClick={signIn}>Sign in with Keycloak</button>
        </section>
      </main>
    )
  }

  return (
    <div className="app-shell">
      {sidebarOpen && <button className="mobile-scrim" aria-label="Close sidebar" onClick={() => setSidebarOpen(false)} />}
      <aside className={`sidebar ${sidebarOpen ? 'sidebar--open' : ''} ${sidebarCollapsed ? 'sidebar--collapsed' : ''}`}>
        <div className="sidebar-topline">
          <a className="brand" href="/" aria-label="Building Copilot home">
            <span className="brand-mark"><Building2 size={18} strokeWidth={2.2} /></span>
            <span>building<span className="brand-light">copilot</span></span>
          </a>
          <button className="icon-button sidebar-close" aria-label="Close sidebar" onClick={() => setSidebarOpen(false)}><X size={18} /></button>
        </div>

        <button className="new-chat-button" onClick={startConversation}>
          <CirclePlus size={18} strokeWidth={1.8} />
          <span>New conversation</span>
          <span className="shortcut">⌘ K</span>
        </button>

        <div className="history-label">YOUR WORKSPACE</div>
        <div className="conversation-list" aria-label="Conversations">
          {conversations.map((conversation) => (
            <button
              className={`conversation-item ${conversation.id === activeId ? 'conversation-item--active' : ''}`}
              key={conversation.id}
              onClick={() => { setActiveId(conversation.id); setSidebarOpen(false) }}
              title={conversation.title}
            >
              <MessageSquareText size={16} strokeWidth={1.8} />
              <span>{conversation.title}</span>
            </button>
          ))}
        </div>

        <div className="sidebar-footer">
          <div className="user-avatar">{userInitials}</div>
          <div className="user-copy"><strong>{userName}</strong><span>Signed in</span></div>
          <button className="icon-button account-menu" aria-label="Sign out" title="Sign out" onClick={signOut}><ChevronDown size={16} /></button>
        </div>
      </aside>

      <main className={`main-panel ${sidebarCollapsed ? 'main-panel--expanded' : ''}`}>
        <header className="topbar">
          <div className="topbar-left">
            <button className="icon-button sidebar-toggle" aria-label="Toggle sidebar" onClick={toggleSidebar}>
              {sidebarOpen || (window.innerWidth > 760 && !sidebarCollapsed) ? <PanelLeftClose size={19} /> : <PanelLeftOpen size={19} />}
            </button>
            <span className="topbar-divider" />
            <button className="workspace-picker" type="button">
              Building assistant <ChevronDown size={15} />
            </button>
          </div>
          <div className="connection-status"><span className="status-dot" />Local model</div>
        </header>

        <section className="chat-area" aria-label="Chat conversation">
          {messages.length === 0 ? (
            <div className="welcome-screen">
              <div className="welcome-emblem"><Sparkles size={22} strokeWidth={1.7} /></div>
              <p className="eyebrow">YOUR BUILDING, BETTER UNDERSTOOD</p>
              <h1>What can I help<br className="mobile-break" /> you figure out?</h1>
              <p className="welcome-copy">Ask about building operations, room conditions, or what to check next.</p>
              <div className="suggestion-grid">
                {suggestions.map(({ icon: Icon, label }) => (
                  <button className="suggestion-card" key={label} onClick={() => sendMessage(label)} disabled={isSending}>
                    <span className="suggestion-icon"><Icon size={17} strokeWidth={1.8} /></span>
                    <span>{label}</span>
                    <ArrowUp className="suggestion-arrow" size={15} />
                  </button>
                ))}
              </div>
            </div>
          ) : (
            <div className="message-list">
              {messages.map((message) => (
                <article className={`message message--${message.role}`} key={message.id}>
                  {message.role === 'user' ? (
                    <div className="user-message">{message.content}</div>
                  ) : (
                    <>
                      <div className={`assistant-mark ${message.role === 'error' ? 'assistant-mark--error' : ''}`}>
                        {message.role === 'error' ? <X size={16} /> : <Building2 size={16} />}
                      </div>
                      <div className="assistant-content">
                        <div className="assistant-name">{message.role === 'error' ? 'Connection issue' : 'Building Copilot'}</div>
                        <div className="message-text">{message.content}</div>
                        {message.role === 'assistant' && (
                          <div className="message-actions">
                            <button className="message-action" onClick={() => copyMessage(message)} aria-label="Copy response" title="Copy response">
                              {copiedId === message.id ? <Check size={15} /> : <Copy size={15} />}
                              <span>{copiedId === message.id ? 'Copied' : 'Copy'}</span>
                            </button>
                          </div>
                        )}
                      </div>
                    </>
                  )}
                </article>
              ))}
              {isSending && (
                <div className="message message--assistant" role="status" aria-label="Assistant is thinking">
                  <div className="assistant-mark"><Building2 size={16} /></div>
                  <div className="thinking-indicator"><span /><span /><span /></div>
                </div>
              )}
              <div ref={messagesEndRef} />
            </div>
          )}
        </section>

        <footer className="composer-dock">
          <form className="composer" onSubmit={handleSubmit}>
            <textarea
              ref={textareaRef}
              value={draft}
              onChange={handleTextareaChange}
              onKeyDown={(event) => {
                if (event.key === 'Enter' && !event.shiftKey) {
                  event.preventDefault()
                  handleSubmit(event)
                }
              }}
              placeholder="Ask anything about your building..."
              aria-label="Message Building Copilot"
              rows={1}
              disabled={isSending}
            />
            <div className="composer-bottom">
              <span className="composer-hint"><span className="hint-dot" /> Responses may need verification</span>
              <div className="composer-controls">
                <span className="keyboard-hint">Enter to send</span>
                <button className="send-button" type="submit" aria-label="Send message" disabled={!draft.trim() || isSending}>
                  <ArrowUp size={18} strokeWidth={2.3} />
                </button>
              </div>
            </div>
          </form>
          <p className="composer-disclaimer">Building Copilot can make mistakes. Verify critical maintenance decisions.</p>
        </footer>
      </main>

    </div>
  )
}

export default App
