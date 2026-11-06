using System.Text.Json;
using ClaudeAgentSdk.Control;

namespace ClaudeAgentSdk.Tests.Control;

public class ContextUsageTests
{
    private const string FullResponse = """
        {
            "categories": [
                {"name": "System prompt", "tokens": 3200, "color": "#abc"},
                {"name": "Deferred tools", "tokens": 900, "color": "#123", "isDeferred": true}
            ],
            "totalTokens": 98200,
            "maxTokens": 155000,
            "rawMaxTokens": 200000,
            "percentage": 49.1,
            "model": "claude-sonnet-4-5",
            "isAutoCompactEnabled": true,
            "memoryFiles": [{"path": "CLAUDE.md", "type": "project", "tokens": 512}],
            "mcpTools": [{"name": "search", "serverName": "ref", "tokens": 164, "isLoaded": true}],
            "agents": [{"agentType": "coder", "source": "sdk", "tokens": 299}],
            "gridRows": [[{"color": "#abc"}, {"color": "#def"}], []],
            "autoCompactThreshold": 140000,
            "deferredBuiltinTools": [{"name": "WebFetch", "tokens": 80}],
            "apiUsage": null
        }
        """;

    [Test]
    public async Task Parse_FullResponse_TypesScalarFields()
    {
        using var doc = JsonDocument.Parse(FullResponse);

        var usage = ContextUsageParser.Parse(doc.RootElement);

        await Assert.That(usage.TotalTokens).IsEqualTo(98200);
        await Assert.That(usage.MaxTokens).IsEqualTo(155000);
        await Assert.That(usage.RawMaxTokens).IsEqualTo(200000);
        await Assert.That(usage.Percentage).IsEqualTo(49.1);
        await Assert.That(usage.Model).IsEqualTo("claude-sonnet-4-5");
        await Assert.That(usage.IsAutoCompactEnabled).IsTrue();
        await Assert.That(usage.AutoCompactThreshold).IsEqualTo(140000);
    }

    [Test]
    public async Task Parse_FullResponse_TypesCategories()
    {
        using var doc = JsonDocument.Parse(FullResponse);

        var usage = ContextUsageParser.Parse(doc.RootElement);

        await Assert.That(usage.Categories.Count).IsEqualTo(2);
        await Assert.That(usage.Categories[0].Name).IsEqualTo("System prompt");
        await Assert.That(usage.Categories[0].Tokens).IsEqualTo(3200);
        await Assert.That(usage.Categories[0].Color).IsEqualTo("#abc");
        await Assert.That(usage.Categories[0].IsDeferred).IsNull();
        await Assert.That(usage.Categories[1].IsDeferred).IsTrue();
    }

    [Test]
    public async Task Parse_FullResponse_KeepsOpenListsAsJsonElement()
    {
        ContextUsageResponse usage;
        using (var doc = JsonDocument.Parse(FullResponse))
        {
            usage = ContextUsageParser.Parse(doc.RootElement);
        }

        // The document is disposed here, so these reads prove the elements were cloned.
        JsonElement memoryFile = usage.MemoryFiles[0];
        await Assert.That(memoryFile.GetProperty("path").GetString()).IsEqualTo("CLAUDE.md");
        await Assert.That(usage.McpTools[0].GetProperty("serverName").GetString()).IsEqualTo("ref");
        await Assert.That(usage.Agents[0].GetProperty("agentType").GetString()).IsEqualTo("coder");
        await Assert.That(usage.GridRows.Count).IsEqualTo(2);
        await Assert.That(usage.GridRows[0].Count).IsEqualTo(2);
        await Assert.That(usage.GridRows[1].Count).IsEqualTo(0);
        await Assert.That(usage.DeferredBuiltinTools!.Count).IsEqualTo(1);
        await Assert.That(usage.DeferredBuiltinTools![0].GetProperty("name").GetString()).IsEqualTo("WebFetch");
        await Assert.That(usage.Raw.GetProperty("apiUsage").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task Parse_WithoutOptionalFields_LeavesThemNull()
    {
        using var doc = JsonDocument.Parse("""
            {
                "categories": [],
                "totalTokens": 0,
                "maxTokens": 155000,
                "rawMaxTokens": 200000,
                "percentage": 0,
                "model": "m",
                "isAutoCompactEnabled": false,
                "memoryFiles": [],
                "mcpTools": [],
                "agents": [],
                "gridRows": []
            }
            """);

        var usage = ContextUsageParser.Parse(doc.RootElement);

        await Assert.That(usage.AutoCompactThreshold).IsNull();
        await Assert.That(usage.DeferredBuiltinTools).IsNull();
        await Assert.That(usage.IsAutoCompactEnabled).IsFalse();
        await Assert.That(usage.Categories.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetContextUsageAsync_SendsRequestAndParsesResponse()
    {
        var transport = new Transport.MockTransport("");
        string? contextRequestSubtype = null;

        transport.OnWrite = json =>
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string type = root.GetProperty("type").GetString()!;

            if (type == "control_request")
            {
                string requestId = root.GetProperty("request_id").GetString()!;
                string subtype = root.GetProperty("request").GetProperty("subtype").GetString()!;
                string payload = "{}";
                if (subtype == "get_context_usage")
                {
                    contextRequestSubtype = subtype;
                    payload = FullResponse;
                }

                transport.EnqueueOutput(
                    "{\"type\":\"control_response\",\"response\":{\"subtype\":\"success\",\"request_id\":\""
                    + requestId + "\",\"response\":" + payload.ReplaceLineEndings("") + "}}");
            }
            else if (type == "user")
            {
                transport.EnqueueOutput("""{"type":"result","subtype":"success","duration_ms":0,"duration_api_ms":0,"is_error":false,"num_turns":1,"session_id":"s1"}""");
            }
        };

        await using var client = new ClaudeClient(transport: transport);
        await foreach (var _ in client.SendAsync("hi"))
        {
        }

        var usage = await client.GetContextUsageAsync();

        await Assert.That(contextRequestSubtype).IsEqualTo("get_context_usage");
        await Assert.That(usage.TotalTokens).IsEqualTo(98200);
        await Assert.That(usage.Categories[0].Name).IsEqualTo("System prompt");
        await Assert.That(usage.MemoryFiles[0].GetProperty("tokens").GetInt32()).IsEqualTo(512);
    }

    [Test]
    public async Task GetContextUsageAsync_NotConnected_Throws()
    {
        await using var client = new ClaudeClient(transport: new Transport.MockTransport(""));

        await Assert.That(async () => await client.GetContextUsageAsync()).Throws<InvalidOperationException>();
    }
}
