# Building Copilot

A local building-operations assistant with an ASP.NET Core controller API, an HTTP MCP server, a React client, and Ollama-backed chat and retrieval-augmented responses.

## Architecture

```text
client (React/Vite)
        |
        v
API host (controllers + MCP tools)
       |             |
       v             v
Application      Infrastructure ----> Ollama
       |
       v
     Domain
```

- `Api/` contains MVC controllers, API contracts, MCP tools, and global exception handling.
- `src/BuildingCopilot.Application/` contains use cases and service abstractions.
- `src/BuildingCopilot.Domain/` contains core building entities.
- `src/BuildingCopilot.Infrastructure/` contains Ollama and telemetry adapters.
- `client/` contains the React/Vite user interface.
- `tests/BuildingCopilot.UnitTests/` tests application use cases with a deterministic fake model client.
- `tests/BuildingCopilot.IntegrationTests/` tests controllers, Problem Details mappings, and MCP over TestServer.
- `postman/BuildingCopilot.AiRegression.postman_collection.json` checks API behavior, citations, evidence, and semantic answer quality.

The dependency direction is inward: API and Infrastructure depend on Application; Application depends on Domain. The MCP tools call the same Application services as the REST controllers.

## Resilience and errors

The typed Ollama HTTP client uses Polly's standard resilience pipeline with two retries, exponential backoff with jitter, a 75-second per-attempt timeout, a 180-second overall timeout, and a circuit breaker. Chat and embedding POSTs are retried because they are inference operations without application-side write effects.

The global exception handler returns Problem Details: upstream unavailable and open circuit map to `503`, upstream HTTP failures to `502`, model timeouts to `504`, and unexpected errors to a sanitized `500`. Client cancellations are not turned into server errors.

The MCP server uses stateless Streamable HTTP at `http://localhost:5178/mcp` with `get_room_telemetry` and `ask_building_manual` tools.

## Prerequisites

- .NET 10 SDK
- Node.js and npm
- Ollama with `llama3.2:3b` and `nomic-embed-text` available locally

```powershell
ollama pull llama3.2:3b
ollama pull nomic-embed-text
```

## Run locally

Start the API in one terminal:

```powershell
dotnet run --project .\BuildingCopilot.csproj
```

The API listens on `http://localhost:5178` by default. Start the React client in another terminal:

```powershell
Set-Location .\client
npm install
npm run dev
```

Open `http://127.0.0.1:5173`. Vite proxies `/api` requests to the API at port `5178` during development.

## Tests

Run the separate unit and integration suites:

```powershell
dotnet test .\BuildingCopilot.slnx
```

Unit tests do not call Ollama. Integration tests use `WebApplicationFactory` and deterministic service fakes; the MCP integration test exercises initialize, tool discovery, and a real JSON-RPC tool call over Streamable HTTP.

## Postman AI regression

Import `postman/BuildingCopilot.AiRegression.postman_collection.json` into Postman. Start the API and Ollama, then run the collection. The RAG checks first assert returned source IDs/citations; semantic checks call the local `judgeModel` and score grounding, faithfulness, relevance, completeness, uncertainty handling, unsupported claims, and invented facts. Adjust `minimumAiScore` for your model and prompt changes.

## Verify

```powershell
dotnet build .\BuildingCopilot.csproj
Set-Location .\client
npm run build
npm run lint
```

API and MCP requests for health, telemetry, chat, RAG, room diagnosis, and MCP tool calls are in `BuildingCopilot.http`.

For your Docker Desktop setup, these are the main URLs:

What it’s for	URL
Building Copilot app	http://localhost:3000
Keycloak admin console	http://localhost:8081/admin
Keycloak sign-in realm	http://localhost:8081/realms/building-copilot
Qdrant dashboard	http://localhost:6333/dashboard

The app sends API requests through its web container; the API doesn’t have a host port published in the Docker setup, so you don’t need to open a separate API URL. Ollama and PostgreSQL are also available only to the other Docker services—not directly in your browser.

If you run the app in local development mode instead of Docker, the URLs differ: the API is http://localhost:5178, the frontend is http://127.0.0.1:5173, and MCP is http://localhost:5178/mcp. Those local-development URLs are described in the README.

For everyday use, start with http://localhost:3000.