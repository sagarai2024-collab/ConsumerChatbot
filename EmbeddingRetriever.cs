using Microsoft.SemanticKernel.Embeddings;

namespace SemanticKernelRagDemo;

/// <summary>
/// Semantic retriever: embeds knowledge-base articles and ranks them against
/// the query embedding by cosine similarity. The vector index syncs lazily with
/// the knowledge store, so admin edits are reflected on the next question
/// without a restart.
/// </summary>
public sealed class EmbeddingRetriever(KnowledgeStore store, ITextEmbeddingGenerationService embeddings) : IRetriever
{
    private readonly Dictionary<string, (string Text, ReadOnlyMemory<float> Vector)> _index = new();
    private readonly SemaphoreSlim _sync = new(1, 1);

    public double MinRelevanceScore => 0.45;

    public async Task<IReadOnlyList<RetrievedArticle>> RetrieveAsync(string query, int topK)
    {
        var articles = store.Snapshot;
        await SyncIndexAsync(articles);

        var queryVector = (await embeddings.GenerateEmbeddingsAsync([query]))[0];

        return articles
            .Where(a => _index.ContainsKey(a.Id))
            .Select(a => new RetrievedArticle(a, VectorMath.CosineSimilarity(queryVector, _index[a.Id].Vector)))
            .Where(r => r.Score >= MinRelevanceScore)
            .OrderByDescending(r => r.Score)
            .Take(topK)
            .ToList();
    }

    private async Task SyncIndexAsync(IReadOnlyList<KnowledgeArticle> articles)
    {
        var stale = articles
            .Where(a => !_index.TryGetValue(a.Id, out var entry) || entry.Text != a.EmbeddingText)
            .ToList();
        var deletedIds = _index.Keys.Except(articles.Select(a => a.Id)).ToList();
        if (stale.Count == 0 && deletedIds.Count == 0)
            return;

        await _sync.WaitAsync();
        try
        {
            foreach (var id in deletedIds)
                _index.Remove(id);

            if (stale.Count > 0)
            {
                var vectors = await embeddings.GenerateEmbeddingsAsync(stale.Select(a => a.EmbeddingText).ToList());
                foreach (var (article, vector) in stale.Zip(vectors))
                    _index[article.Id] = (article.EmbeddingText, vector);
            }
        }
        finally
        {
            _sync.Release();
        }
    }
}
