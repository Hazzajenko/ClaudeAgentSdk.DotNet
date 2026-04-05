using System.Text.Json;
using ClaudeAgentSdk;
using ClaudeAgentSdk.Messages;

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

    [Test]
    public async Task ReceiveResponseAsync_StopsAfterFirstResultMessage()
    {
        var transport = CreateMockWithFlow("""
            {"type":"assistant","message":{"content":[{"type":"text","text":"Hello"}],"model":"m"}}
            {"type":"result","subtype":"success","duration_ms":100,"duration_api_ms":90,"is_error":false,"num_turns":1,"session_id":"s1","result":"Hello"}
            """);

        await using var client = new ClaudeClient(transport: transport);

        // SendAsync to establish connection and get first response
        var firstMessages = new List<Message>();
        await foreach (var msg in client.SendAsync("first"))
        {
            firstMessages.Add(msg);
        }

        // Now set up a second turn response and write user message directly
        SetupSecondTurn(transport, """
            {"type":"assistant","message":{"content":[{"type":"text","text":"Second"}],"model":"m"}}
            {"type":"result","subtype":"success","duration_ms":50,"duration_api_ms":40,"is_error":false,"num_turns":2,"session_id":"s1","result":"Second"}
            """);

        await client.ControlHandler!.WriteAsync(
            ClaudeClient.BuildUserMessageJson("second", "default"), CancellationToken.None);

        var messages = new List<Message>();
        await foreach (var msg in client.ReceiveResponseAsync())
        {
            messages.Add(msg);
        }

        await Assert.That(messages.Count).IsEqualTo(2);
        await Assert.That(messages[0]).IsTypeOf<AssistantMessage>();
        await Assert.That(messages[1]).IsTypeOf<ResultMessage>();
    }

    [Test]
    public async Task ReceiveResponseAsync_WhenNotConnected_Throws()
    {
        var client = new ClaudeClient();

        await Assert.That(async () =>
        {
            await foreach (var _ in client.ReceiveResponseAsync())
            {
            }
        }).ThrowsException();

        await client.DisposeAsync();
    }

    [Test]
    public async Task ReceiveResponseAsync_AfterDispose_Throws()
    {
        var client = new ClaudeClient();
        await client.DisposeAsync();

        await Assert.That(async () =>
        {
            await foreach (var _ in client.ReceiveResponseAsync())
            {
            }
        }).ThrowsException();
    }

    [Test]
    public async Task GetServerInfo_WhenNotConnected_Throws()
    {
        var client = new ClaudeClient();

        Assert.Throws<InvalidOperationException>(() => client.GetServerInfo());

        await client.DisposeAsync();
    }

    [Test]
    public async Task GetServerInfo_AfterDispose_Throws()
    {
        var client = new ClaudeClient();
        await client.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => client.GetServerInfo());
    }

    [Test]
    public async Task GetServerInfo_AfterConnect_ReturnsInitResult()
    {
        var transport = CreateMockWithFlow("""
            {"type":"result","subtype":"success","duration_ms":0,"duration_api_ms":0,"is_error":false,"num_turns":0,"session_id":"s1"}
            """);

        await using var client = new ClaudeClient(transport: transport);
        await foreach (var _ in client.SendAsync("test")) { }

        var info = client.GetServerInfo();
        await Assert.That(info.ValueKind).IsEqualTo(JsonValueKind.Object);
    }

    private static Transport.MockTransport CreateMockWithFlow(string contentMessages)
    {
        var transport = new Transport.MockTransport("");

        transport.OnWrite = json =>
        {
            if (json.Contains("\"type\":\"control_request\"") && json.Contains("initialize"))
            {
                using var doc = JsonDocument.Parse(json);
                string requestId = doc.RootElement.GetProperty("request_id").GetString()!;
                string response = "{\"type\":\"control_response\",\"response\":{\"subtype\":\"success\",\"request_id\":\"" + requestId + "\",\"response\":{}}}";
                transport.EnqueueOutput(response);
            }
            else if (json.Contains("\"type\":\"user\""))
            {
                foreach (var line in contentMessages.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (line.Length > 0)
                        transport.EnqueueOutput(line);
                }
            }
        };

        return transport;
    }

    private static void SetupSecondTurn(Transport.MockTransport transport, string contentMessages)
    {
        transport.OnWrite = json =>
        {
            if (json.Contains("\"type\":\"user\""))
            {
                foreach (var line in contentMessages.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (line.Length > 0)
                        transport.EnqueueOutput(line);
                }
            }
        };
    }
}
