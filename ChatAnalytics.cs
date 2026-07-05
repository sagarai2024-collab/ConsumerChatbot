using System.Collections.Concurrent;

namespace SemanticKernelRagDemo;

public sealed record ChatLogEntry(DateTimeOffset At, string Question, string Topic, string Path);

/// <summary>
/// AI chat analytics: every conversation is logged with automatic topic
/// detection, powering the admin dashboard of most-asked topics.
/// </summary>
public sealed class ChatAnalytics
{
    private readonly ConcurrentQueue<ChatLogEntry> _log = new();

    public void Record(string question, string topic, string path) =>
        _log.Enqueue(new ChatLogEntry(DateTimeOffset.UtcNow, question, topic, path));

    public object Summary()
    {
        var entries = _log.ToArray();
        return new
        {
            totalMessages = entries.Length,
            byPath = entries
                .GroupBy(e => e.Path)
                .OrderByDescending(g => g.Count())
                .Select(g => new { path = g.Key, count = g.Count() }),
            topTopics = entries
                .Where(e => e.Path is "rag" or "offline-retrieval" or "cache")
                .GroupBy(e => e.Topic)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => new { topic = g.Key, count = g.Count() }),
            recent = entries
                .OrderByDescending(e => e.At)
                .Take(20)
                .Select(e => new { at = e.At, question = e.Question, topic = e.Topic, path = e.Path })
        };
    }
}
