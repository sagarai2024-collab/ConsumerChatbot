using System.Collections.Concurrent;

namespace SemanticKernelRagDemo;

/// <summary>
/// Simple in-memory response cache with a sliding TTL, keyed by normalized question.
/// Repeated questions (very common in consumer support) are served without any
/// embedding or chat-completion call.
/// </summary>
public sealed class ResponseCache(TimeSpan ttl)
{
    private readonly ConcurrentDictionary<string, (string Answer, DateTimeOffset Expires)> _cache = new();

    private static string Normalize(string question) =>
        string.Join(' ', question.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public bool TryGet(string question, out string answer)
    {
        answer = string.Empty;
        var key = Normalize(question);
        if (!_cache.TryGetValue(key, out var entry))
            return false;

        if (entry.Expires < DateTimeOffset.UtcNow)
        {
            _cache.TryRemove(key, out _);
            return false;
        }

        answer = entry.Answer;
        return true;
    }

    public void Set(string question, string answer) =>
        _cache[Normalize(question)] = (answer, DateTimeOffset.UtcNow.Add(ttl));
}
