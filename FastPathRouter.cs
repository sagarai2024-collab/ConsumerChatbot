namespace SemanticKernelRagDemo;

/// <summary>
/// Handles trivial intents (greetings, thanks, capability questions) with canned
/// responses so they never reach the LLM. In production this pattern eliminated
/// a large share of API calls, cutting cost and latency for the most common messages.
/// </summary>
public static class FastPathRouter
{
    private static readonly (string Intent, string[] Triggers, string Response)[] Intents =
    [
        ("greeting", ["hi", "hello", "hey", "namaste", "good morning", "good evening"],
         "Hello! I'm the StreamCo support assistant. Ask me anything about plans, billing, devices, or troubleshooting."),
        ("thanks", ["thanks", "thank you", "thx", "great, thanks"],
         "You're welcome! Is there anything else I can help you with?"),
        ("goodbye", ["bye", "goodbye", "see you"],
         "Goodbye! Feel free to come back any time you need help."),
        ("capabilities", ["what can you do", "help", "who are you"],
         "I answer questions about StreamCo subscriptions, billing, devices, downloads, parental controls, and troubleshooting — all grounded in our official knowledge base."),
    ];

    public static (string Intent, string Response)? TryHandle(string userMessage)
    {
        var normalized = userMessage.Trim().ToLowerInvariant().TrimEnd('!', '.', '?');
        foreach (var (intent, triggers, response) in Intents)
        {
            if (triggers.Any(t => normalized == t || (normalized.Length <= 25 && normalized.StartsWith(t))))
                return (intent, response);
        }
        return null;
    }
}
