using System.Text.Json;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Tests.Messages;

public class AssistantMessageTests
{
    [Test]
    public async Task Text_SingleTextBlock_ReturnsText()
    {
        var message = new AssistantMessage
        {
            Content = [new TextBlock { Text = "hello" }],
            Model = "m",
        };

        await Assert.That(message.Text).IsEqualTo("hello");
    }

    [Test]
    public async Task Text_MultipleTextBlocks_Concatenates()
    {
        var message = new AssistantMessage
        {
            Content =
            [
                new TextBlock { Text = "hello " },
                new TextBlock { Text = "world" },
            ],
            Model = "m",
        };

        await Assert.That(message.Text).IsEqualTo("hello world");
    }

    [Test]
    public async Task Text_NoTextBlocks_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("{}");
        var message = new AssistantMessage
        {
            Content = [new ToolUseBlock { Id = "t1", Name = "Read", Input = doc.RootElement.Clone() }],
            Model = "m",
        };

        await Assert.That(message.Text).IsNull();
    }

    [Test]
    public async Task Text_MixedBlocks_OnlyTextConcatenated()
    {
        using var doc = JsonDocument.Parse("{}");
        var message = new AssistantMessage
        {
            Content =
            [
                new TextBlock { Text = "before " },
                new ToolUseBlock { Id = "t1", Name = "Read", Input = doc.RootElement.Clone() },
                new TextBlock { Text = "after" },
            ],
            Model = "m",
        };

        await Assert.That(message.Text).IsEqualTo("before after");
    }

    [Test]
    public async Task Text_EmptyContent_ReturnsNull()
    {
        var message = new AssistantMessage
        {
            Content = [],
            Model = "m",
        };

        await Assert.That(message.Text).IsNull();
    }
}
