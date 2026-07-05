using System.Text.Json;

namespace SemanticKernelRagDemo;

/// <summary>
/// Dynamic knowledge base with admin CRUD, persisted to JSON.
/// Raises <see cref="Changed"/> so caches and retrieval indexes invalidate
/// instantly — answers update in seconds with zero redeployment.
/// </summary>
public sealed class KnowledgeStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _path;
    private readonly Lock _lock = new();
    private List<KnowledgeArticle> _articles;

    public event Action? Changed;

    public KnowledgeStore(string path)
    {
        _path = path;
        var json = File.ReadAllText(path);
        _articles = JsonSerializer.Deserialize<List<KnowledgeArticle>>(json, JsonOptions)
                    ?? throw new InvalidOperationException($"Knowledge base at '{path}' is empty or malformed.");
    }

    public IReadOnlyList<KnowledgeArticle> Snapshot
    {
        get { lock (_lock) return _articles.ToList(); }
    }

    public KnowledgeArticle Add(string title, string[] tags, string content)
    {
        lock (_lock)
        {
            var nextNumber = _articles
                .Select(a => int.TryParse(a.Id.TrimStart('K', 'B'), out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;
            var article = new KnowledgeArticle($"KB{nextNumber:D3}", title, tags, content);
            _articles.Add(article);
            Persist();
            return article;
        }
    }

    public bool Update(string id, string title, string[] tags, string content)
    {
        lock (_lock)
        {
            var index = _articles.FindIndex(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return false;
            _articles[index] = new KnowledgeArticle(_articles[index].Id, title, tags, content);
            Persist();
            return true;
        }
    }

    public bool Delete(string id)
    {
        lock (_lock)
        {
            var removed = _articles.RemoveAll(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed) Persist();
            return removed;
        }
    }

    private void Persist()
    {
        File.WriteAllText(_path, JsonSerializer.Serialize(_articles, JsonOptions));
        Changed?.Invoke();
    }
}
