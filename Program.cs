using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using SemanticKernelRagDemo;

var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
var apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");
var chatDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_CHAT_DEPLOYMENT") ?? "gpt-4o-mini";
var embeddingDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_EMBEDDING_DEPLOYMENT") ?? "text-embedding-3-small";

var articles = KnowledgeBase.Load(Path.Combine(AppContext.BaseDirectory, "data", "knowledge-base.json"));

IRetriever retriever;
IChatCompletionService? chatService = null;

var hasAzure = !string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey);
if (hasAzure)
{
    var kernel = Kernel.CreateBuilder()
        .AddAzureOpenAIChatCompletion(chatDeployment, endpoint!, apiKey!)
        .AddAzureOpenAITextEmbeddingGeneration(embeddingDeployment, endpoint!, apiKey!)
        .Build();

    chatService = kernel.GetRequiredService<IChatCompletionService>();
    var embeddings = kernel.GetRequiredService<ITextEmbeddingGenerationService>();

    Console.WriteLine("Indexing knowledge base with Azure OpenAI embeddings...");
    retriever = await EmbeddingRetriever.CreateAsync(articles, embeddings);
    Console.WriteLine($"Indexed {articles.Count} articles (semantic retrieval + grounded generation).\n");
}
else
{
    retriever = new KeywordRetriever(articles);
    Console.WriteLine("AZURE_OPENAI_ENDPOINT / AZURE_OPENAI_API_KEY not set — running in OFFLINE mode.");
    Console.WriteLine("Retrieval works with keyword scoring; answers show the best matching article.");
    Console.WriteLine("Set the environment variables (see README) for full RAG with GPT-4o-mini.\n");
}

var ragChat = new RagChatService(retriever, chatService);

Console.WriteLine("StreamCo Support Assistant — ask about plans, billing, devices, downloads...");
Console.WriteLine("Type 'exit' to quit.\n");

while (true)
{
    Console.Write("You: ");
    var question = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(question))
        continue;
    if (question.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    try
    {
        var result = await ragChat.AskAsync(question);

        Console.WriteLine($"\nBot ({result.Path}): {result.Answer}\n");
        if (result.Sources.Count > 0)
        {
            Console.WriteLine("Sources: " + string.Join(", ",
                result.Sources.Select(s => $"{s.Article.Id} \"{s.Article.Title}\" ({s.Score:F2})")));
            Console.WriteLine();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nError: {ex.Message}\n");
    }
}
