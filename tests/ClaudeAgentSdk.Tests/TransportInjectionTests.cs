using System.Text.Json;
using ClaudeAgentSdk.Messages;
using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk.Tests;

public class TransportInjectionTests
{
    /// <summary>
    /// Creates a mock transport that responds to control protocol init + user message flow.
    /// Extracts request_id dynamically from ControlProtocolHandler's generated IDs.
    /// </summary>
    private static Transport.MockTransport CreateMockWithFlow(string contentMessages)
    {
        var transport = new Transport.MockTransport("");

        transport.OnWrite = json =>
        {
            if (json.Contains("\"type\":\"control_request\"") && json.Contains("initialize"))
            {
                // Extract the dynamic request_id
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
                transport.Complete();
            }
        };

        return transport;
    }

    [Test]
    public async Task ClaudeClient_WithInjectedTransport_UsesItDirectly()
    {
        var contentMessages = """
            {"type":"assistant","message":{"content":[{"type":"text","text":"Hello"}],"model":"m"}}
            {"type":"result","subtype":"success","duration_ms":100,"duration_api_ms":90,"is_error":false,"num_turns":1,"session_id":"s1","result":"Hello"}
            """;

        var transport = CreateMockWithFlow(contentMessages);
        await using var client = new ClaudeClient(transport: transport);

        var messages = new List<Message>();
        await foreach (var msg in client.SendAsync("test"))
        {
            messages.Add(msg);
        }

        await Assert.That(messages.Count).IsGreaterThanOrEqualTo(1);
        await Assert.That(messages[^1]).IsTypeOf<ResultMessage>();
        await Assert.That(transport.WriteCount).IsGreaterThanOrEqualTo(2); // init + user message
    }

    [Test]
    public async Task ClaudeClient_WithInjectedTransport_DoesNotRequireCliPath()
    {
        var contentMessages = """
            {"type":"result","subtype":"success","duration_ms":0,"duration_api_ms":0,"is_error":false,"num_turns":0,"session_id":"s1"}
            """;

        var transport = CreateMockWithFlow(contentMessages);
        await using var client = new ClaudeClient(
            options: new ClaudeClientOptions { CliPath = "/nonexistent/path" },
            transport: transport);

        var messages = new List<Message>();
        await foreach (var msg in client.SendAsync("test"))
        {
            messages.Add(msg);
        }

        await Assert.That(messages.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task Claude_QueryAsync_WithInjectedTransport_Works()
    {
        var contentMessages = """
            {"type":"assistant","message":{"content":[{"type":"text","text":"injected"}],"model":"m"}}
            {"type":"result","subtype":"success","duration_ms":50,"duration_api_ms":40,"is_error":false,"num_turns":1,"session_id":"s1","result":"injected"}
            """;

        var transport = CreateMockWithFlow(contentMessages);
        var messages = new List<Message>();
        await foreach (var msg in Claude.QueryAsync("test", transport: transport))
        {
            messages.Add(msg);
        }

        await Assert.That(messages.Count).IsGreaterThanOrEqualTo(1);
        var result = messages.OfType<ResultMessage>().First();
        await Assert.That(result.Result).IsEqualTo("injected");
    }
}
