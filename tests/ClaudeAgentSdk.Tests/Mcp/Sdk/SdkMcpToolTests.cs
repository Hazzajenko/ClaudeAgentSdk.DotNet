using System.Text.Json;
using ClaudeAgentSdk.Mcp;
using ClaudeAgentSdk.Mcp.Sdk;

namespace ClaudeAgentSdk.Tests.Mcp.Sdk;

public class SdkMcpToolTests
{
    [Test]
    public async Task Create_SetsAllProperties()
    {
        using var doc = JsonDocument.Parse("""{"type":"object","properties":{}}""");
        var schema = doc.RootElement.Clone();

        var tool = SdkMcpTool.Create("my-tool", "Does stuff", schema,
            async (args, ct) => McpToolResult.Text("ok"));

        await Assert.That(tool.Name).IsEqualTo("my-tool");
        await Assert.That(tool.Description).IsEqualTo("Does stuff");
        await Assert.That(tool.InputSchema.GetProperty("type").GetString()).IsEqualTo("object");
        await Assert.That(tool.Annotations).IsNull();
    }

    [Test]
    public async Task Create_WithAnnotations_SetsAnnotations()
    {
        using var doc = JsonDocument.Parse("""{"type":"object","properties":{}}""");

        var annotations = new McpToolAnnotations { ReadOnly = true };
        var tool = SdkMcpTool.Create("reader", "Reads stuff", doc.RootElement.Clone(),
            async (args, ct) => McpToolResult.Text("ok"),
            annotations);

        await Assert.That(tool.Annotations).IsNotNull();
        await Assert.That(tool.Annotations!.ReadOnly).IsTrue();
    }

    [Test]
    public async Task Handler_CanBeInvoked()
    {
        using var doc = JsonDocument.Parse("""{"type":"object","properties":{}}""");
        using var inputDoc = JsonDocument.Parse("""{"name":"test"}""");

        var tool = SdkMcpTool.Create("echo", "Echoes input", doc.RootElement.Clone(),
            async (args, ct) => McpToolResult.Text(args.GetProperty("name").GetString()!));

        var result = await tool.Handler(inputDoc.RootElement, CancellationToken.None);

        await Assert.That(result).IsTypeOf<McpTextToolResult>();
        await Assert.That(((McpTextToolResult)result).Value).IsEqualTo("test");
    }
}
