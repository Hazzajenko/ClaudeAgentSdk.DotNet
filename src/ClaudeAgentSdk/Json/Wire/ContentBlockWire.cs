using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeAgentSdk.Json.Wire;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextBlockWire), "text")]
[JsonDerivedType(typeof(ThinkingBlockWire), "thinking")]
[JsonDerivedType(typeof(ToolUseBlockWire), "tool_use")]
[JsonDerivedType(typeof(ToolResultBlockWire), "tool_result")]
internal abstract record ContentBlockWire;

internal sealed record TextBlockWire : ContentBlockWire
{
    public required string Text { get; init; }
}

internal sealed record ThinkingBlockWire : ContentBlockWire
{
    public required string Thinking { get; init; }
    public required string Signature { get; init; }
}

internal sealed record ToolUseBlockWire : ContentBlockWire
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required JsonElement Input { get; init; }
}

internal sealed record ToolResultBlockWire : ContentBlockWire
{
    public required string ToolUseId { get; init; }
    public JsonElement? Content { get; init; }
    public bool? IsError { get; init; }
}
