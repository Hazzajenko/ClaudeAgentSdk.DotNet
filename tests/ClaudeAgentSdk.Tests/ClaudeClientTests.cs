using System.Text.Json;
using ClaudeAgentSdk;

namespace ClaudeAgentSdk.Tests;

public class ClaudeClientTests
{
    [Test]
    public async Task BuildUserMessageJson_ProducesValidJson()
    {
        string json = ClaudeClient.BuildUserMessageJson("Hello Claude", "default");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        await Assert.That(root.GetProperty("type").GetString()).IsEqualTo("user");
        await Assert.That(root.GetProperty("message").GetProperty("role").GetString()).IsEqualTo("user");
        await Assert.That(root.GetProperty("message").GetProperty("content").GetString()).IsEqualTo("Hello Claude");
        await Assert.That(root.GetProperty("session_id").GetString()).IsEqualTo("default");
    }

    [Test]
    public async Task BuildUserMessageJson_EscapesSpecialCharacters()
    {
        string json = ClaudeClient.BuildUserMessageJson("Hello \"world\" \n\ttab", "default");

        using var doc = JsonDocument.Parse(json);
        string? content = doc.RootElement.GetProperty("message").GetProperty("content").GetString();

        await Assert.That(content).IsEqualTo("Hello \"world\" \n\ttab");
    }

    [Test]
    public async Task ClaudeClient_DisposeAsync_CanBeCalledMultipleTimes()
    {
        var client = new ClaudeClient();

        await client.DisposeAsync();
        await client.DisposeAsync(); // Should not throw
    }

    [Test]
    public async Task ClaudeClient_SendAfterDispose_Throws()
    {
        var client = new ClaudeClient();
        await client.DisposeAsync();

        await Assert.That(async () =>
        {
            await foreach (var _ in client.SendAsync("test"))
            {
                // Should not reach
            }
        }).ThrowsException();
    }
}
