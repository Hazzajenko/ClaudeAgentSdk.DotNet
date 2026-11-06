using System.Text.Json;
using ClaudeAgentSdk.Agents;
using ClaudeAgentSdk.Mcp;
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

    [Test]
    public async Task ClaudeClient_Initialize_SendsAgentDefinitionFields()
    {
        var contentMessages = """
            {"type":"result","subtype":"success","duration_ms":0,"duration_api_ms":0,"is_error":false,"num_turns":0,"session_id":"s1"}
            """;

        var transport = CreateMockWithFlow(contentMessages);
        string? initJson = null;
        var respond = transport.OnWrite;
        transport.OnWrite = json =>
        {
            if (json.Contains("\"subtype\":\"initialize\""))
            {
                initJson = json;
            }
            respond!(json);
        };

        var options = new ClaudeClientOptions
        {
            Agents = new Dictionary<string, AgentDefinition>
            {
                ["reviewer"] = new()
                {
                    Description = "Reviews code",
                    Prompt = "Review",
                    Model = "claude-opus-4-5-20251101",
                    DisallowedTools = ["Bash"],
                    MaxTurns = 3,
                    InitialPrompt = "Begin",
                    Background = true,
                    Effort = EffortLevel.Low,
                    PermissionMode = PermissionMode.Plan,
                    McpServers = ["shared"],
                    InlineMcpServers = new Dictionary<string, McpServerConfig>
                    {
                        ["local"] = new McpStdioServerConfig("node")
                    }
                }
            }
        };

        await using var client = new ClaudeClient(options: options, transport: transport);
        await foreach (var _ in client.SendAsync("test"))
        {
        }

        await Assert.That(initJson).IsNotNull();
        using var doc = JsonDocument.Parse(initJson!);
        JsonElement agent = doc.RootElement.GetProperty("request").GetProperty("agents").GetProperty("reviewer");
        await Assert.That(agent.GetProperty("model").GetString()).IsEqualTo("claude-opus-4-5-20251101");
        await Assert.That(agent.GetProperty("disallowedTools")[0].GetString()).IsEqualTo("Bash");
        await Assert.That(agent.GetProperty("maxTurns").GetInt32()).IsEqualTo(3);
        await Assert.That(agent.GetProperty("initialPrompt").GetString()).IsEqualTo("Begin");
        await Assert.That(agent.GetProperty("background").GetBoolean()).IsTrue();
        await Assert.That(agent.GetProperty("effort").GetString()).IsEqualTo("low");
        await Assert.That(agent.GetProperty("permissionMode").GetString()).IsEqualTo("plan");
        JsonElement servers = agent.GetProperty("mcpServers");
        await Assert.That(servers[0].GetString()).IsEqualTo("shared");
        await Assert.That(servers[1].GetProperty("local").GetProperty("command").GetString()).IsEqualTo("node");
    }
}
