using System.Collections.Concurrent;

namespace SemanticKernelRagDemo;

/// <summary>
/// Simple in-memory response cache with a TTL, keyed by normalized question.
/// Repeated questions (very common in consumer support) are served without any
/// embedding or chat-completion call. Cleared instantly when the knowledge
/// base changes so stale answers are never served.
/// </summary>
public sealed class ResponseCache(TimeSpan ttl)
{
    private readonly ConcurrentDictionary<string, (string Answer, string Topic, DateTimeOffset Expires)> _cache = new();

    private static string Normalize(string question) =>
        string.Join(' ', question.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    public bool TryGet(string question, out (string Answer, string Topic) entry)
    {
        entry = default;
        var key = Normalize(question);
        if (!_cache.TryGetValue(key, out var cached))
            return false;

        if (cached.Expires < DateTimeOffset.UtcNow)
        {
            _cache.TryRemove(key, out _);
            return false;
        }

        entry = (cached.Answer, cached.Topic);
        return true;
    }

    public void Set(string question, string answer, string topic) =>
        _cache[Normalize(question)] = (answer, topic, DateTimeOffset.UtcNow.Add(ttl));

    public void Clear() => _cache.Clear();
}
