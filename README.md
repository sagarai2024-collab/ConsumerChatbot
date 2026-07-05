# ConsumerChatbot — AI-Powered Support Portal (RAG Demo)

A full-stack **RAG customer-support chatbot** built with **.NET 9**, **Microsoft Semantic Kernel**, and **Azure OpenAI**, with a live chat UI, an **AI chat analytics dashboard**, and a **dynamic knowledge base with admin CRUD and instant cache invalidation**.

This is an original demo of the architecture I designed and shipped in production as a live consumer-portal chatbot serving OTT/DTH subscribers (DishTV). It contains **no proprietary code or data** — the "StreamCo" brand, knowledge base, and content are fictional, created for this demo.

## What a recruiter can see working, in one minute

```bash
dotnet run
# open http://localhost:5000
```

No credentials needed — without Azure OpenAI keys the app runs in **offline mode** (keyword retrieval, returns the grounding article). With keys configured it does full semantic RAG with GPT-4o-mini.

1. **Chat page (`/`)** — ask about plans, billing, devices. Each answer shows its *path* (fast-path / cache / rag), detected *topic*, and *source articles with relevance scores*. Ask something out of scope ("can I fly to the moon?") and watch it refuse to invent an answer — hallucination prevention in action.
2. **Admin dashboard (`/admin.html`)** —
   - **AI Chat Analytics:** every conversation logged with automatic topic detection; most-asked topics ranked live.
   - **Dynamic Knowledge Base:** add or edit an article, then re-ask the related question in chat — the answer updates **in seconds, with zero redeployment**, because the response cache invalidates on every knowledge-base change.

## Architecture

```
User question
   │
   ▼
1. Fast-path intent router      — greetings/thanks answered instantly, no LLM call
   │
   ▼
2. Response cache (1h TTL)      — repeated questions served without any model call;
   │                              invalidated instantly on knowledge-base edits
   ▼
3. Retrieval                    — Azure OpenAI embeddings + cosine similarity,
   │                              top-3 articles above a relevance threshold
   │                              (offline fallback: keyword scoring)
   ▼
4. Grounded generation          — context-augmented prompt to GPT-4o-mini with
   │                              strict "answer only from the articles" rules
   ▼
5. Analytics                    — automatic topic detection, logged per message,
                                  powering the admin dashboard
```

Steps 1–2 mirror the cost engineering from the production system, where fast-path handlers and caching cut AI API costs by ~90%.

## Full RAG mode (optional)

```powershell
$env:AZURE_OPENAI_ENDPOINT = "https://<your-resource>.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY  = "<your-key>"
$env:AZURE_OPENAI_CHAT_DEPLOYMENT      = "gpt-4o-mini"            # default
$env:AZURE_OPENAI_EMBEDDING_DEPLOYMENT = "text-embedding-3-small" # default
dotnet run
```

## API

| Endpoint | Purpose |
|----------|---------|
| `POST /api/chat` | Ask a question → answer, path, topic, sources with scores |
| `GET/POST /api/articles`, `PUT/DELETE /api/articles/{id}` | Knowledge-base CRUD (persists to JSON, invalidates caches) |
| `GET /api/analytics` | Totals, path breakdown, most-asked topics, recent questions |

*Demo note: admin endpoints are open for easy exploration; the production system used role-based authorization (ASP.NET Core Identity).*

## Project layout

| File | Purpose |
|------|---------|
| `Program.cs` | Minimal-API startup, endpoints, Azure vs offline mode selection |
| `RagChatService.cs` | The pipeline: fast-path → cache → retrieve → grounded generation |
| `EmbeddingRetriever.cs` | Semantic retrieval; vector index re-syncs on KB edits |
| `KeywordRetriever.cs` | Offline fallback retrieval (token overlap) |
| `KnowledgeStore.cs` | Dynamic KB with CRUD, JSON persistence, change events |
| `ChatAnalytics.cs` | Per-message logging with topic detection |
| `FastPathRouter.cs` / `ResponseCache.cs` | Cost/latency optimizations |
| `wwwroot/` | Chat UI and admin dashboard (vanilla JS, no build step) |
| `Dockerfile` | One-command container build for cloud deployment |

## Deploy (free tier)

Works on any container host — e.g. [Render](https://render.com): *New → Web Service → connect this repo → Docker*. The app honors the `PORT` environment variable automatically.
