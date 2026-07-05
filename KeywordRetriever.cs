namespace SemanticKernelRagDemo;

/// <summary>
/// Offline fallback retriever using token-overlap scoring, so the demo runs
/// end-to-end without Azure OpenAI credentials. With credentials configured,
/// <see cref="EmbeddingRetriever"/> is used instead.
/// </summary>
public sealed class KeywordRetriever(IReadOnlyList<KnowledgeArticle> articles) : IRetriever
{
    private static readonly char[] Separators = [' ', ',', '.', '?', '!', ':', ';', '(', ')', '\'', '"', '/', '\n', '\r', '\t'];

    private static readonly HashSet<string> StopWords =
    [
        "a", "an", "the", "is", "are", "was", "were", "do", "does", "did", "can", "i", "my", "me", "you",
        "your", "how", "what", "when", "where", "why", "which", "to", "of", "in", "on", "for", "and", "or",
        "it", "its", "not", "with", "at", "by", "from", "about", "there", "this", "that"
    ];

    private static HashSet<string> Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && !StopWords.Contains(t))
            .ToHashSet();

    public double MinRelevanceScore => 0.25;

    public Task<IReadOnlyList<RetrievedArticle>> RetrieveAsync(string query, int topK)
    {
        var queryTokens = Tokenize(query);
        if (queryTokens.Count == 0)
            return Task.FromResult<IReadOnlyList<RetrievedArticle>>([]);

        var results = articles
            .Select(article =>
            {
                var articleTokens = Tokenize(article.EmbeddingText + ' ' + string.Join(' ', article.Tags));
                var overlap = queryTokens.Count(articleTokens.Contains);
                var score = (double)overlap / queryTokens.Count;
                return new RetrievedArticle(article, score);
            })
            .Where(r => r.Score >= MinRelevanceScore)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();

        return Task.FromResult<IReadOnlyList<RetrievedArticle>>(results);
    }
}
