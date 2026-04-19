using System.Text.Json;
using ClaudeAgentSdk;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Tests;

public class UserContentBlockTests
{
    [Test]
    public async Task BuildUserMessageJson_WithTextBlock_EmitsArrayContent()
    {
        IReadOnlyList<UserContentBlock> blocks = [new UserTextBlock("Hello")];

        string json = ClaudeClient.BuildUserMessageJson(blocks, "default");

        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("message").GetProperty("content");
        await Assert.That(content.ValueKind).IsEqualTo(JsonValueKind.Array);
        await Assert.That(content.GetArrayLength()).IsEqualTo(1);

        var first = content[0];
        await Assert.That(first.GetProperty("type").GetString()).IsEqualTo("text");
        await Assert.That(first.GetProperty("text").GetString()).IsEqualTo("Hello");
    }

    [Test]
    public async Task BuildUserMessageJson_WithImageBase64Block_EmitsImageSourceObject()
    {
        IReadOnlyList<UserContentBlock> blocks = [new UserImageBlock("image/png", "iVBORw0KG=")];

        string json = ClaudeClient.BuildUserMessageJson(blocks, "default");

        using var doc = JsonDocument.Parse(json);
        var block = doc.RootElement.GetProperty("message").GetProperty("content")[0];
        await Assert.That(block.GetProperty("type").GetString()).IsEqualTo("image");

        var source = block.GetProperty("source");
        await Assert.That(source.GetProperty("type").GetString()).IsEqualTo("base64");
        await Assert.That(source.GetProperty("media_type").GetString()).IsEqualTo("image/png");
        await Assert.That(source.GetProperty("data").GetString()).IsEqualTo("iVBORw0KG=");
    }

    [Test]
    public async Task BuildUserMessageJson_WithImageUrlBlock_EmitsUrlSource()
    {
        IReadOnlyList<UserContentBlock> blocks = [new UserImageUrlBlock("https://example.com/cat.png")];

        string json = ClaudeClient.BuildUserMessageJson(blocks, "default");

        using var doc = JsonDocument.Parse(json);
        var block = doc.RootElement.GetProperty("message").GetProperty("content")[0];
        await Assert.That(block.GetProperty("type").GetString()).IsEqualTo("image");

        var source = block.GetProperty("source");
        await Assert.That(source.GetProperty("type").GetString()).IsEqualTo("url");
        await Assert.That(source.GetProperty("url").GetString()).IsEqualTo("https://example.com/cat.png");
    }

    [Test]
    public async Task BuildUserMessageJson_WithToolResultBlock_EmitsToolUseIdAndContent()
    {
        IReadOnlyList<UserContentBlock> blocks =
        [
            new UserToolResultBlock("toolu_123", Content: "result text", IsError: false)
        ];

        string json = ClaudeClient.BuildUserMessageJson(blocks, "default");

        using var doc = JsonDocument.Parse(json);
        var block = doc.RootElement.GetProperty("message").GetProperty("content")[0];
        await Assert.That(block.GetProperty("type").GetString()).IsEqualTo("tool_result");
        await Assert.That(block.GetProperty("tool_use_id").GetString()).IsEqualTo("toolu_123");
        await Assert.That(block.GetProperty("content").GetString()).IsEqualTo("result text");
        await Assert.That(block.GetProperty("is_error").GetBoolean()).IsFalse();
    }

    [Test]
    public async Task BuildUserMessageJson_WithToolResultBlock_OmitsOptionalFields()
    {
        IReadOnlyList<UserContentBlock> blocks = [new UserToolResultBlock("toolu_x")];

        string json = ClaudeClient.BuildUserMessageJson(blocks, "default");

        using var doc = JsonDocument.Parse(json);
        var block = doc.RootElement.GetProperty("message").GetProperty("content")[0];
        await Assert.That(block.TryGetProperty("content", out _)).IsFalse();
        await Assert.That(block.TryGetProperty("is_error", out _)).IsFalse();
    }

    [Test]
    public async Task BuildUserMessageJson_WithMixedBlocks_PreservesOrder()
    {
        IReadOnlyList<UserContentBlock> blocks =
        [
            new UserImageBlock("image/jpeg", "/9j/4AAQ"),
            new UserTextBlock("What's in this image?")
        ];

        string json = ClaudeClient.BuildUserMessageJson(blocks, "session-A");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        await Assert.That(root.GetProperty("type").GetString()).IsEqualTo("user");
        await Assert.That(root.GetProperty("session_id").GetString()).IsEqualTo("session-A");

        var content = root.GetProperty("message").GetProperty("content");
        await Assert.That(content.GetArrayLength()).IsEqualTo(2);
        await Assert.That(content[0].GetProperty("type").GetString()).IsEqualTo("image");
        await Assert.That(content[1].GetProperty("type").GetString()).IsEqualTo("text");
    }

    [Test]
    public async Task BuildUserMessageJson_WithEmptyList_Throws()
    {
        IReadOnlyList<UserContentBlock> blocks = [];

        await Assert.That(() => ClaudeClient.BuildUserMessageJson(blocks, "default"))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task SendAsync_WithContentBlocks_WritesArrayContentToTransport()
    {
        var transport = new Transport.MockTransport("");
        transport.OnWrite = json =>
        {
            if (json.Contains("\"type\":\"control_request\"") && json.Contains("initialize"))
            {
                using var doc = JsonDocument.Parse(json);
                string requestId = doc.RootElement.GetProperty("request_id").GetString()!;
                transport.EnqueueOutput(
                    "{\"type\":\"control_response\",\"response\":{\"subtype\":\"success\",\"request_id\":\""
                    + requestId + "\",\"response\":{}}}");
            }
            else if (json.Contains("\"type\":\"user\""))
            {
                transport.EnqueueOutput(
                    "{\"type\":\"result\",\"subtype\":\"success\",\"duration_ms\":1,\"duration_api_ms\":1,\"is_error\":false,\"num_turns\":1,\"session_id\":\"s\"}");
            }
        };

        await using var client = new ClaudeClient(transport: transport);
        IReadOnlyList<UserContentBlock> blocks =
        [
            new UserImageBlock("image/png", "ABC="),
            new UserTextBlock("describe")
        ];

        await foreach (var _ in client.SendAsync(blocks)) { }

        var userMsg = transport.WrittenMessages.Find(m => m.Contains("\"type\":\"user\""));
        await Assert.That(userMsg).IsNotNull();

        using var sent = JsonDocument.Parse(userMsg!);
        var content = sent.RootElement.GetProperty("message").GetProperty("content");
        await Assert.That(content.ValueKind).IsEqualTo(JsonValueKind.Array);
        await Assert.That(content.GetArrayLength()).IsEqualTo(2);
    }

    [Test]
    public async Task SendRawAsync_WritesRawJsonToTransport()
    {
        var transport = new Transport.MockTransport("");
        transport.OnWrite = json =>
        {
            if (json.Contains("\"type\":\"control_request\"") && json.Contains("initialize"))
            {
                using var doc = JsonDocument.Parse(json);
                string requestId = doc.RootElement.GetProperty("request_id").GetString()!;
                transport.EnqueueOutput(
                    "{\"type\":\"control_response\",\"response\":{\"subtype\":\"success\",\"request_id\":\""
                    + requestId + "\",\"response\":{}}}");
            }
            else if (json.Contains("\"role\":\"user\""))
            {
                transport.EnqueueOutput(
                    "{\"type\":\"result\",\"subtype\":\"success\",\"duration_ms\":1,\"duration_api_ms\":1,\"is_error\":false,\"num_turns\":1,\"session_id\":\"s\"}");
            }
        };

        await using var client = new ClaudeClient(transport: transport);
        const string raw =
            """{"type":"user","message":{"role":"user","content":"raw escape"},"session_id":"default"}""";

        await foreach (var _ in client.SendRawAsync(raw)) { }

        await Assert.That(transport.WrittenMessages.Contains(raw)).IsTrue();
    }

    [Test]
    public async Task SendRawAsync_WithInvalidJson_Throws()
    {
        await using var client = new ClaudeClient();

        await Assert.That(async () =>
        {
            await foreach (var _ in client.SendRawAsync("{not valid json")) { }
        }).Throws<JsonException>();
    }
}
