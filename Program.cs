using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using SemanticKernelRagDemo;

var builder = WebApplication.CreateBuilder(args);

// Cloud hosts (Render, Railway, etc.) inject PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var app = builder.Build();

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
var chatDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_CHAT_DEPLOYMENT") ?? "gpt-4o-mini";
var embeddingDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_EMBEDDING_DEPLOYMENT") ?? "text-embedding-3-small";

var store = new KnowledgeStore(Path.Combine(app.Environment.ContentRootPath, "data", "knowledge-base.json"));
var analytics = new ChatAnalytics();

IRetriever retriever;
IChatCompletionService? chatService = null;
var hasAzure = !string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey);
if (hasAzure)
{
    var kernel = Kernel.CreateBuilder()
        .AddAzureOpenAIChatCompletion(chatDeployment, endpoint!, apiKey!)
        .AddAzureOpenAITextEmbeddingGeneration(embeddingDeployment, endpoint!, apiKey!)
        .Build();

    chatService = kernel.GetRequiredService<IChatCompletionService>();
    retriever = new EmbeddingRetriever(store, kernel.GetRequiredService<ITextEmbeddingGenerationService>());
    app.Logger.LogInformation("Azure OpenAI configured: semantic retrieval + grounded generation.");
}
else
{
    retriever = new KeywordRetriever(store);
    app.Logger.LogWarning("AZURE_OPENAI_* not set - running in OFFLINE mode (keyword retrieval, no generation).");
}

var ragChat = new RagChatService(retriever, chatService, analytics);
store.Changed += ragChat.InvalidateCache; // KB edits invalidate cached answers instantly

app.UseDefaultFiles();
app.UseStaticFiles();

// ---- Chat ----
app.MapPost("/api/chat", async (ChatRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { error = "Message is required." });

    var result = await ragChat.AskAsync(request.Message.Trim());
    return Results.Ok(new
    {
        answer = result.Answer,
        path = result.Path,
        topic = result.Topic,
        mode = hasAzure ? "azure-openai" : "offline",
        sources = result.Sources.Select(s => new { id = s.Article.Id, title = s.Article.Title, score = Math.Round(s.Score, 2) })
    });
});

// ---- Knowledge-base admin (open for demo purposes; production had role-based auth) ----
app.MapGet("/api/articles", () => Results.Ok(store.Snapshot));

app.MapPost("/api/articles", (ArticleRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        return Results.BadRequest(new { error = "Title and content are required." });
    var article = store.Add(request.Title.Trim(), request.NormalizedTags, request.Content.Trim());
    return Results.Created($"/api/articles/{article.Id}", article);
});

app.MapPut("/api/articles/{id}", (string id, ArticleRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        return Results.BadRequest(new { error = "Title and content are required." });
    return store.Update(id, request.Title.Trim(), request.NormalizedTags, request.Content.Trim())
        ? Results.Ok()
        : Results.NotFound();
});

app.MapDelete("/api/articles/{id}", (string id) =>
    store.Delete(id) ? Results.Ok() : Results.NotFound());

// ---- Analytics dashboard ----
app.MapGet("/api/analytics", () => Results.Ok(analytics.Summary()));

app.Run();

public sealed record ChatRequest(string Message);

public sealed record ArticleRequest(string Title, string? Tags, string Content)
{
    public string[] NormalizedTags => (Tags ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(t => t.ToLowerInvariant())
        .ToArray();
}
