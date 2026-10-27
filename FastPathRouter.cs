using System.Text.RegularExpressions;

namespace SemanticKernelRagDemo;

/// <summary>
/// Handles trivial intents (greetings, thanks, capability questions) with canned
/// responses so they never reach the LLM. In production this pattern eliminated
/// a large share of API calls, cutting cost and latency for the most common messages.
///
/// A message only takes the fast path when the whole message is the trigger, or the
/// trigger plus a filler word ("hi there", "thanks a lot"). Prefix matching would send
/// real questions such as "hidden charges?" or "help with my recharge" to a canned reply.
/// </summary>
public static class FastPathRouter
{
    private static readonly (string Intent, string[] Triggers, string Response)[] Intents =
    [
        ("greeting", ["hi", "hello", "hey", "namaste", "good morning", "good evening"],
         "Hello! I'm the StreamCo support assistant. Ask me anything about plans, billing, devices, or troubleshooting."),
        ("thanks", ["thanks", "thank you", "thx", "great thanks"],
         "You're welcome! Is there anything else I can help you with?"),
        ("goodbye", ["bye", "goodbye", "see you"],
         "Goodbye! Feel free to come back any time you need help."),
        ("capabilities", ["what can you do", "help", "who are you"],
         "I answer questions about StreamCo subscriptions, billing, devices, downloads, parental controls, and troubleshooting — all grounded in our official knowledge base."),
    ];

    private static readonly HashSet<string> Fillers =
        ["there", "team", "bot", "everyone", "again", "a lot", "so much", "very much"];

    public static (string Intent, string Response)? TryHandle(string userMessage)
    {
        var normalized = Normalize(userMessage);
        foreach (var (intent, triggers, response) in Intents)
        {
            foreach (var trigger in triggers)
            {
                if (normalized == trigger ||
                    (normalized.StartsWith(trigger + " ") && Fillers.Contains(normalized[(trigger.Length + 1)..])))
                    return (intent, response);
            }
        }
        return null;
    }

    /// <summary>Lower-case, punctuation removed, single spaces.</summary>
    private static string Normalize(string text) =>
        string.Join(' ', Regex.Replace(text.ToLowerInvariant(), @"[^\p{L}\p{N}\s]", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
