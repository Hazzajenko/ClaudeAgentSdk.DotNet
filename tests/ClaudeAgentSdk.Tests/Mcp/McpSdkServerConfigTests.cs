using System.Text.Json;
using ClaudeAgentSdk.Mcp;
using ClaudeAgentSdk.Mcp.Sdk;

namespace ClaudeAgentSdk.Tests.Mcp;

public class McpSdkServerConfigTests
{
    private static SdkMcpTool DummyTool(string name)
    {
        using var doc = JsonDocument.Parse("""{"type":"object"}""");
        return SdkMcpTool.Create(name, "desc", doc.RootElement.Clone(),
            (_, _) => Task.FromResult(McpToolResult.Text("ok")));
    }

    [Test]
    public async Task GetQualifiedToolNames_SingleTool_ReturnsQualifiedName()
    {
        var config = McpSdkServerConfig.Create("server", [DummyTool("add")]);

        var names = config.GetQualifiedToolNames("calc");

        await Assert.That(names).Count().IsEqualTo(1);
        await Assert.That(names[0]).IsEqualTo("mcp__calc__add");
    }

    [Test]
    public async Task GetQualifiedToolNames_MultipleTools_AllFormatted()
    {
        var config = McpSdkServerConfig.Create("server",
            [DummyTool("add"), DummyTool("subtract"), DummyTool("multiply")]);

        var names = config.GetQualifiedToolNames("math");

        await Assert.That(names).Count().IsEqualTo(3);
        await Assert.That(names[0]).IsEqualTo("mcp__math__add");
        await Assert.That(names[1]).IsEqualTo("mcp__math__subtract");
        await Assert.That(names[2]).IsEqualTo("mcp__math__multiply");
    }

    [Test]
    public async Task GetQualifiedToolNames_EmptyTools_ReturnsEmpty()
    {
        var config = McpSdkServerConfig.Create("server", []);

        var names = config.GetQualifiedToolNames("calc");

        await Assert.That(names).IsEmpty();
    }
}
