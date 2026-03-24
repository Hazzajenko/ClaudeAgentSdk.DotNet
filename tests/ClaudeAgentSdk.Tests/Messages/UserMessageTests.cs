using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Tests.Messages;

public class UserMessageTests
{
    [Test]
    public async Task StringContent_TextContentReturnsString_ContentBlocksNull()
    {
        var message = new UserMessage { Content = "hello" };

        await Assert.That(message.TextContent).IsEqualTo("hello");
        await Assert.That(message.ContentBlocks).IsNull();
    }

    [Test]
    public async Task BlockContent_ContentBlocksReturnsList_TextContentNull()
    {
        IReadOnlyList<ContentBlock> blocks = [new TextBlock { Text = "hello" }];
        var message = new UserMessage { Content = blocks };

        await Assert.That(message.ContentBlocks).IsNotNull();
        await Assert.That(message.ContentBlocks!.Count).IsEqualTo(1);
        await Assert.That(message.TextContent).IsNull();
    }
}
