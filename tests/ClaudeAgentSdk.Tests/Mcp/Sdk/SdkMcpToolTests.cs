using System.Text.Json;
using System.Text.Json.Serialization;
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

    // --- New: sync overload ---

    [Test]
    public async Task CreateSync_WrapsCorrectly()
    {
        using var doc = JsonDocument.Parse("""{"type":"object","properties":{}}""");
        using var inputDoc = JsonDocument.Parse("""{"x":42}""");

        var tool = SdkMcpTool.Create("sync-tool", "Sync", doc.RootElement.Clone(),
            (input, _) => McpToolResult.Text(input.GetProperty("x").GetInt32().ToString()));

        var result = await tool.Handler(inputDoc.RootElement, CancellationToken.None);

        await Assert.That(((McpTextToolResult)result).Value).IsEqualTo("42");
    }

    // --- New: generic Create<T> ---

    [Test]
    public async Task CreateGenericAsync_DerivesSchemaAndDeserializesInput()
    {
        using var inputDoc = JsonDocument.Parse("""{"a":10,"b":3}""");

        var tool = SdkMcpTool.Create<TwoNumbers>("add", "Add",
            ToolTestContext.Default.TwoNumbers,
            (input, _) => Task.FromResult(McpToolResult.Text($"{input.A + input.B}")));

        // Schema was auto-derived
        await Assert.That(tool.InputSchema.TryGetProperty("properties", out _)).IsTrue();

        // Handler deserializes to typed input
        var result = await tool.Handler(inputDoc.RootElement, CancellationToken.None);
        await Assert.That(((McpTextToolResult)result).Value).IsEqualTo("13");
    }

    [Test]
    public async Task CreateGenericSync_DerivesSchemaAndDeserializesInput()
    {
        using var inputDoc = JsonDocument.Parse("""{"a":7,"b":2}""");

        var tool = SdkMcpTool.Create<TwoNumbers>("sub", "Subtract",
            ToolTestContext.Default.TwoNumbers,
            (input, _) => McpToolResult.Text($"{input.A - input.B}"));

        var result = await tool.Handler(inputDoc.RootElement, CancellationToken.None);
        await Assert.That(((McpTextToolResult)result).Value).IsEqualTo("5");
    }

    [Test]
    public async Task CreateGeneric_SchemaHasCorrectPropertyTypes()
    {
        var tool = SdkMcpTool.Create<TwoNumbers>("test", "Test",
            ToolTestContext.Default.TwoNumbers,
            (input, _) => McpToolResult.Text("ok"));

        var props = tool.InputSchema.GetProperty("properties");
        await Assert.That(props.GetProperty("a").GetProperty("type").GetString()).IsEqualTo("number");
        await Assert.That(props.GetProperty("b").GetProperty("type").GetString()).IsEqualTo("number");
    }

    [Test]
    public async Task CreateGeneric_MissingProperties_DefaultsToZero()
    {
        using var inputDoc = JsonDocument.Parse("""{"wrong":"data"}""");

        var tool = SdkMcpTool.Create<TwoNumbers>("defaults", "Defaults",
            ToolTestContext.Default.TwoNumbers,
            (input, _) => McpToolResult.Text($"{input.A + input.B}"));

        // STJ deserializes missing doubles as 0
        var result = await tool.Handler(inputDoc.RootElement, CancellationToken.None);
        await Assert.That(((McpTextToolResult)result).Value).IsEqualTo("0");
    }
}

public sealed record TwoNumbers
{
    [JsonPropertyName("a")]
    public double A { get; init; }

    [JsonPropertyName("b")]
    public double B { get; init; }
}

[JsonSerializable(typeof(TwoNumbers))]
internal sealed partial class ToolTestContext : JsonSerializerContext;
