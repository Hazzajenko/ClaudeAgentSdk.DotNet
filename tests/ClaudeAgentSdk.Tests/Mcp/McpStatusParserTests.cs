using System.Text.Json;
using ClaudeAgentSdk.Mcp;

namespace ClaudeAgentSdk.Tests.Mcp;

public class McpStatusParserTests
{
    [Test]
    public async Task Parse_ConnectedServer_ExtractsAllFields()
    {
        using var doc = JsonDocument.Parse("""
            {
                "mcpServers": [{
                    "name": "my-server",
                    "status": "connected",
                    "serverInfo": {"name": "My MCP Server", "version": "1.0.0"},
                    "scope": "project",
                    "tools": [
                        {"name": "search", "description": "Search stuff", "annotations": {"readOnly": true}},
                        {"name": "write", "description": "Write stuff", "annotations": {"destructive": true}}
                    ]
                }]
            }
            """);

        var result = McpStatusParser.Parse(doc.RootElement);

        await Assert.That(result.McpServers.Count).IsEqualTo(1);
        var server = result.McpServers[0];
        await Assert.That(server.Name).IsEqualTo("my-server");
        await Assert.That(server.Status).IsEqualTo("connected");
        await Assert.That(server.ServerInfo!.Name).IsEqualTo("My MCP Server");
        await Assert.That(server.ServerInfo.Version).IsEqualTo("1.0.0");
        await Assert.That(server.Scope).IsEqualTo("project");
        await Assert.That(server.Tools!.Count).IsEqualTo(2);
        await Assert.That(server.Tools[0].Annotations!.ReadOnly).IsTrue();
        await Assert.That(server.Tools[1].Annotations!.Destructive).IsTrue();
    }

    [Test]
    public async Task Parse_FailedServer_ExtractsError()
    {
        using var doc = JsonDocument.Parse("""
            {
                "mcpServers": [{
                    "name": "broken-server",
                    "status": "failed",
                    "error": "Connection refused"
                }]
            }
            """);

        var result = McpStatusParser.Parse(doc.RootElement);

        await Assert.That(result.McpServers[0].Status).IsEqualTo("failed");
        await Assert.That(result.McpServers[0].Error).IsEqualTo("Connection refused");
        await Assert.That(result.McpServers[0].ServerInfo).IsNull();
        await Assert.That(result.McpServers[0].Tools).IsNull();
    }

    [Test]
    public async Task Parse_EmptyServers_ReturnsEmptyList()
    {
        using var doc = JsonDocument.Parse("""{"mcpServers": []}""");

        var result = McpStatusParser.Parse(doc.RootElement);

        await Assert.That(result.McpServers.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Parse_NoMcpServersField_ReturnsEmptyList()
    {
        using var doc = JsonDocument.Parse("""{}""");

        var result = McpStatusParser.Parse(doc.RootElement);

        await Assert.That(result.McpServers.Count).IsEqualTo(0);
    }
}
