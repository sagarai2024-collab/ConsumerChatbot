# Semantic Kernel RAG Demo — Customer Support Chatbot

A Retrieval Augmented Generation (RAG) chatbot built with **.NET 9**, **Microsoft Semantic Kernel**, and **Azure OpenAI**, demonstrating the architecture I used to ship a live AI-powered consumer-portal chatbot serving OTT/DTH subscribers in production.

The demo plays a support assistant for a fictional streaming service ("StreamCo"): every answer is grounded in a knowledge base — the model is never allowed to invent policies, prices, or steps.

## Architecture

```
User question
   │
   ▼
1. Fast-path intent router     — greetings/thanks answered instantly, no LLM call
   │
   ▼
2. Response cache (1h TTL)     — repeated questions served without any model call
   │
   ▼
3. Retrieval                   — Azure OpenAI embeddings + cosine similarity,
   │                             top-3 articles above a relevance threshold
   ▼
4. Grounded generation         — context-augmented prompt to GPT-4o-mini with
                                 strict "answer only from the articles" rules
                                 (hallucination prevention)
```

Steps 1–2 mirror the cost optimizations from the production system, where fast-path handlers and caching cut AI API costs by ~90%.

## Run it

Works out of the box **without any credentials** (offline mode: keyword retrieval, returns the best matching article):

```bash
dotnet run
```

For the full RAG experience with semantic retrieval and GPT-generated answers, set your Azure OpenAI details first:

```bash
# PowerShell
$env:AZURE_OPENAI_ENDPOINT = "https://<your-resource>.openai.azure.com/"
$env:AZURE_OPENAI_API_KEY  = "<your-key>"
$env:AZURE_OPENAI_CHAT_DEPLOYMENT      = "gpt-4o-mini"            # default
$env:AZURE_OPENAI_EMBEDDING_DEPLOYMENT = "text-embedding-3-small" # default

dotnet run
```

## Sample session

```
You: how many screens can I watch on at the same time?

Bot (rag): It depends on your plan — Basic allows 1 screen, Standard allows 2,
and Premium allows 4 simultaneous screens. (See "Managing devices and screen limits".)

Sources: KB003 "Managing devices and screen limits" (0.71), KB001 "Subscription plans and pricing" (0.58)

You: can I get a refund on the moon?

Bot (rag): I couldn't find anything about that in our knowledge base. Please
contact support at help.streamco.example for further assistance.
```

## Project layout

| File | Purpose |
|------|---------|
| `Program.cs` | Startup, Azure OpenAI vs offline mode selection, chat loop |
| `RagChatService.cs` | The pipeline: fast-path → cache → retrieve → grounded generation |
| `EmbeddingRetriever.cs` | Semantic retrieval (embeddings + cosine similarity) |
| `KeywordRetriever.cs` | Offline fallback retrieval (token overlap) |
| `FastPathRouter.cs` | Canned answers for trivial intents (cost/latency optimization) |
| `ResponseCache.cs` | TTL cache keyed by normalized question |
| `data/knowledge-base.json` | The grounding corpus (10 support articles) |

## Key ideas demonstrated

- **Grounding & hallucination prevention** — the system prompt restricts answers to retrieved articles; out-of-scope questions get an honest "I don't know" instead of a confident guess.
- **Relevance thresholding** — articles below a similarity floor are discarded, so weak matches never pollute the prompt.
- **Cost engineering** — trivial intents and repeated questions never reach the model.
- **Graceful degradation** — the same pipeline runs offline with keyword retrieval when no credentials are configured.
