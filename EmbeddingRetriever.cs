using Microsoft.SemanticKernel.Embeddings;

namespace SemanticKernelRagDemo;

/// <summary>
/// Semantic retriever: embeds every knowledge-base article once at startup,
/// then ranks articles against the query embedding by cosine similarity.
/// </summary>
public sealed class EmbeddingRetriever : IRetriever
{
    private readonly ITextEmbeddingGenerationService _embeddings;
    private readonly List<(KnowledgeArticle Article, ReadOnlyMemory<float> Vector)> _index;

    private EmbeddingRetriever(ITextEmbeddingGenerationService embeddings,
        List<(KnowledgeArticle, ReadOnlyMemory<float>)> index)
    {
        _embeddings = embeddings;
        _index = index;
    }

    public static async Task<EmbeddingRetriever> CreateAsync(
        IReadOnlyList<KnowledgeArticle> articles, ITextEmbeddingGenerationService embeddings)
    {
        var vectors = await embeddings.GenerateEmbeddingsAsync(
            articles.Select(a => a.EmbeddingText).ToList());

        var index = articles.Zip(vectors, (article, vector) => (article, vector)).ToList();
        return new EmbeddingRetriever(embeddings, index);
    }

    public double MinRelevanceScore => 0.45;

    public async Task<IReadOnlyList<RetrievedArticle>> RetrieveAsync(string query, int topK)
    {
        var queryVector = (await _embeddings.GenerateEmbeddingsAsync([query]))[0];

        return _index
            .Select(entry => new RetrievedArticle(entry.Article,
                VectorMath.CosineSimilarity(queryVector, entry.Vector)))
            .Where(r => r.Score >= MinRelevanceScore)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }
}
