import { useEffect, useState } from 'react'
import { AuthContext } from './AuthContext.js'
import { oidcManager } from './oidcConfig'

let initializationTask

function getInitialUser() {
  if (!initializationTask) {
    initializationTask = (async () => {
      const query = new URLSearchParams(window.location.search)
      if (query.has('code') && query.has('state')) {
        await oidcManager.signinRedirectCallback()
        window.history.replaceState({}, document.title, window.location.pathname)
      }
      return oidcManager.getUser()
    })()
  }

  return initializationTask
}

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null)
  const [isReady, setIsReady] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    let mounted = true
    const handleUserLoaded = (loadedUser) => mounted && setUser(loadedUser)
    const handleUserUnloaded = () => mounted && setUser(null)

    oidcManager.events.addUserLoaded(handleUserLoaded)
    oidcManager.events.addUserUnloaded(handleUserUnloaded)
    getInitialUser()
      .then((loadedUser) => { if (mounted) setUser(loadedUser) })
      .catch((loadError) => { if (mounted) setError(loadError.message) })
      .finally(() => { if (mounted) setIsReady(true) })

    return () => {
      mounted = false
      oidcManager.events.removeUserLoaded(handleUserLoaded)
      oidcManager.events.removeUserUnloaded(handleUserUnloaded)
    }
  }, [])

  async function signIn() {
    setError('')
    await oidcManager.signinRedirect()
  }

  async function signOut() {
    await oidcManager.signoutRedirect()
  }

  async function getAccessToken() {
    let currentUser = await oidcManager.getUser()
    if (!currentUser || currentUser.expired) {
      currentUser = await oidcManager.signinSilent()
    }
    if (!currentUser?.access_token) {
      throw new Error('Your session expired. Sign in again to continue.')
    }
    return currentUser.access_token
  }

  return (
    <AuthContext.Provider value={{ user, isReady, error, signIn, signOut, getAccessToken }}>
      {children}
    </AuthContext.Provider>
  )
}