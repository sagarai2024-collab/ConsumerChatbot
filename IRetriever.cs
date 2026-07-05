namespace SemanticKernelRagDemo;

public interface IRetriever
{
    /// <summary>Relevance floor below which a match is discarded. Scale depends on the scoring method.</summary>
    double MinRelevanceScore { get; }

    Task<IReadOnlyList<RetrievedArticle>> RetrieveAsync(string query, int topK);
}
