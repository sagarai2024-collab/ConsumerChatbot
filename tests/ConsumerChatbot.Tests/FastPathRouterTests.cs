using SemanticKernelRagDemo;
using Xunit;

public class FastPathRouterTests
{
    [Theory]
    [InlineData("hi", "greeting")]
    [InlineData("Hello!", "greeting")]
    [InlineData("hi there", "greeting")]
    [InlineData("Good morning", "greeting")]
    [InlineData("thanks a lot", "thanks")]
    [InlineData("Great, thanks!", "thanks")]
    [InlineData("thank you so much", "thanks")]
    [InlineData("bye", "goodbye")]
    [InlineData("help", "capabilities")]
    [InlineData("What can you do?", "capabilities")]
    public void Trivial_messages_take_the_fast_path(string message, string expectedIntent)
    {
        var result = FastPathRouter.TryHandle(message);

        Assert.NotNull(result);
        Assert.Equal(expectedIntent, result.Value.Intent);
    }

    [Theory]
    [InlineData("hidden charges?")]          // starts with "hi"
    [InlineData("help with my recharge")]    // starts with "help"
    [InlineData("hey my channels are gone")] // starts with "hey"
    [InlineData("thanks but my payment failed")]
    [InlineData("how do I cancel my plan")]
    public void Real_questions_go_to_retrieval(string message)
    {
        Assert.Null(FastPathRouter.TryHandle(message));
    }
}
