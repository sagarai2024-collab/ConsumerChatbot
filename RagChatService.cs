using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace SemanticKernelRagDemo;

public sealed record ChatResult(string Answer, IReadOnlyList<RetrievedArticle> Sources, string Path, string Topic);

/// <summary>
/// The RAG pipeline: fast-path intents -> response cache -> retrieval ->
/// context-augmented prompt -> grounded generation. Every conversation is
/// logged with automatic topic detection for the analytics dashboard.
/// </summary>
public sealed class RagChatService(IRetriever retriever, IChatCompletionService? chat, ChatAnalytics analytics)
{
    private const int TopK = 3;

    private readonly ResponseCache _cache = new(TimeSpan.FromHours(1));

    private const string SystemPrompt =
        """
        You are StreamCo's customer support assistant.
        Answer the customer's question using ONLY the knowledge-base articles provided below.
        Rules:
        - If the articles do not contain the answer, say you don't have that information and
          suggest contacting support at help.streamco.example — never invent policies, prices, or steps.
        - Be concise and friendly. Use the customer's terminology.
        - When the answer comes from an article, you may mention the article title for reference.
        """;

    /// <summary>Called when the knowledge base changes: cached answers may be stale.</summary>
    public void InvalidateCache() => _cache.Clear();

    public async Task<ChatResult> AskAsync(string question)
    {
        var result = await AnswerAsync(question);
        analytics.Record(question, result.Topic, result.Path);
        return result;
    }

    private async Task<ChatResult> AnswerAsync(string question)
    {
        // 1. Fast path: trivial intents never reach the LLM.
        if (FastPathRouter.TryHandle(question) is { } fastPath)
            return new ChatResult(fastPath.Response, [], "fast-path", fastPath.Intent);

        // 2. Cache: repeated questions are answered without any model call.
        if (_cache.TryGet(question, out var cached))
            return new ChatResult(cached.Answer, [], "cache", cached.Topic);

        // 3. Retrieve grounding context.
        var sources = await retriever.RetrieveAsync(question, TopK);
        if (sources.Count == 0)
        {
            return new ChatResult(
                "I couldn't find anything about that in our knowledge base. " +
                "Please contact support at help.streamco.example for further assistance.",
                [], "no-context", "unanswered");
        }

        // Topic detection: the best-matching article's primary tag.
        var topic = sources[0].Article.Tags.FirstOrDefault() ?? "general";

        // Offline mode: no chat model available, return the best article directly.
        if (chat is null)
        {
            var best = sources[0].Article;
            var offlineAnswer = $"[offline mode — best matching article: {best.Title}]\n{best.Content}";
            _cache.Set(question, offlineAnswer, topic);
            return new ChatResult(offlineAnswer, sources, "offline-retrieval", topic);
        }

        // 4. Context-augmented prompt + grounded generation.
        var context = new StringBuilder();
        foreach (var (article, score) in sources.Select(s => (s.Article, s.Score)))
            context.AppendLine($"[{article.Id}] {article.Title} (relevance {score:F2})\n{article.Content}\n");

        var history = new ChatHistory(SystemPrompt);
        history.AddUserMessage($"Knowledge-base articles:\n{context}\nCustomer question: {question}");

        var reply = await chat.GetChatMessageContentAsync(history);
        var answer = reply.Content ?? "Sorry, I couldn't generate a response. Please try again.";

        _cache.Set(question, answer, topic);
        return new ChatResult(answer, sources, "rag", topic);
    }
}
