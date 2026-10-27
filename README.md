# ConsumerChatbot

A RAG (Retrieval Augmented Generation) customer-support chatbot built with .NET 9, Microsoft Semantic Kernel, and Azure OpenAI. It ships with a web chat UI, an analytics dashboard, and a dynamic knowledge base that can be edited at runtime without redeploying.

The design comes from a consumer-portal chatbot I built and ran in production for an OTT/DTH subscription platform. This repository is a clean-room implementation: the "StreamCo" brand, the knowledge base, and all content here are fictional. Vectors and articles live in memory and a JSON file here; the Postgres version on Supabase pgvector is in [Phoenix-Watcho](https://github.com/sagarai2024-collab/Phoenix-Watcho).

## Features

- **Grounded answers only.** The model answers strictly from knowledge-base articles retrieved for the question. If nothing relevant is found, it says so instead of guessing.
- **Chat analytics.** Every conversation is logged with automatic topic detection. The admin dashboard shows total traffic, most-asked topics, and recent questions. The log is held in memory, so it resets when the app restarts.
- **Dynamic knowledge base.** Articles can be added, edited, or deleted from the admin page. The response cache invalidates on every change, so answers update within seconds — no restart, no redeploy.
- **Cost-aware pipeline.** Greetings and other trivial intents are answered by a fast-path router without touching the LLM, and repeated questions are served from a 1-hour cache. In the production system this class of optimization cut API costs by roughly 90%.
- **Runs without credentials.** With no Azure OpenAI keys configured, the app falls back to keyword retrieval and returns the matching article directly, so the whole flow can be tried locally in offline mode.

## Quick start

```bash
dotnet run
```

Then open http://localhost:5000:

- `/` — the chat. Each reply shows which path produced it (`fast-path`, `cache`, `rag`) and the source articles with relevance scores.
- `/admin.html` — analytics plus knowledge-base management. Edit an article, then re-ask a related question in the chat to see the answer change immediately.

## Enabling Azure OpenAI

Set these environment variables before `dotnet run` to switch from offline mode to full semantic retrieval and generation:

```powershell
$env:AZURE_OPENAI_ENDPOINT = "https://<your-resource>.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY  = "<your-key>"
$env:AZURE_OPENAI_CHAT_DEPLOYMENT      = "gpt-4o-mini"            # default
$env:AZURE_OPENAI_EMBEDDING_DEPLOYMENT = "text-embedding-3-small" # default
```

## How it works

```
User question
   │
   ▼
1. Fast-path intent router      greetings/thanks answered instantly, no LLM call
   │
   ▼
2. Response cache (1h TTL)      repeated questions served without a model call;
   │                            invalidated whenever the knowledge base changes
   ▼
3. Retrieval                    Azure OpenAI embeddings + cosine similarity,
   │                            top-3 articles above a relevance threshold
   │                            (offline fallback: keyword scoring)
   ▼
4. Grounded generation          context-augmented prompt to GPT-4o-mini with
   │                            strict "answer only from the articles" rules
   ▼
5. Analytics                    topic detected and logged per message
```

## API

| Endpoint | Purpose |
|----------|---------|
| `POST /api/chat` | Ask a question; returns the answer, path, topic, and sources with scores |
| `GET /api/articles` | List knowledge-base articles |
| `POST /api/articles` | Create an article |
| `PUT /api/articles/{id}` | Update an article |
| `DELETE /api/articles/{id}` | Delete an article |
| `GET /api/analytics` | Totals, path breakdown, top topics, recent questions |

Admin endpoints are left open in this demo to make it easy to explore. The production system used role-based authorization (ASP.NET Core Identity).

## Project structure

| File | Purpose |
|------|---------|
| `Program.cs` | Minimal-API startup, endpoints, Azure/offline mode selection |
| `RagChatService.cs` | Pipeline: fast-path → cache → retrieve → grounded generation |
| `EmbeddingRetriever.cs` | Semantic retrieval; vector index re-syncs on KB edits |
| `KeywordRetriever.cs` | Offline fallback retrieval |
| `KnowledgeStore.cs` | Knowledge base CRUD with JSON persistence and change events |
| `ChatAnalytics.cs` | Per-message logging with topic detection |
| `FastPathRouter.cs`, `ResponseCache.cs` | Cost and latency optimizations |
| `wwwroot/` | Chat UI and admin dashboard (vanilla JS, no build step) |
| `Dockerfile` | Container build for cloud deployment |
| `tests/` | xUnit tests for the fast-path router (`dotnet test tests/ConsumerChatbot.Tests`) |

## Deployment

The Dockerfile works on any container host. On the free tier of [Render](https://render.com): New → Web Service → connect this repo → Docker. The app honors the `PORT` environment variable automatically.
