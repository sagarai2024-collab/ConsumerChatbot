using System.Text.Json;

namespace SemanticKernelRagDemo;

public sealed record KnowledgeArticle(string Id, string Title, string[] Tags, string Content)
{
    public string EmbeddingText => $"{Title}. {Content}";
}

public sealed record RetrievedArticle(KnowledgeArticle Article, double Score);

public static class KnowledgeBase
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static IReadOnlyList<KnowledgeArticle> Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<KnowledgeArticle>>(json, Options)
               ?? throw new InvalidOperationException($"Knowledge base at '{path}' is empty or malformed.");
    }
}
