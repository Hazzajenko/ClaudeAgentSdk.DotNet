using System.Text.Json;

namespace ClaudeAgentSdk.Json.Wire;

// Each record is read from one element of a "content" array.
// WireMapper.MapContentBlocks reads the "type" field by hand and picks the record,
// because STJ polymorphism throws on an unknown discriminator and cannot skip it.
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
