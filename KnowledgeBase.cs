using System.Text.Json.Serialization;

namespace SemanticKernelRagDemo;

public sealed record KnowledgeArticle(string Id, string Title, string[] Tags, string Content)
{
    [JsonIgnore]
    public string EmbeddingText => $"{Title}. {Content}";
}

public sealed record RetrievedArticle(KnowledgeArticle Article, double Score);
