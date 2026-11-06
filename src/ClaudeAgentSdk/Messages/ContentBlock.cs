using System.Text.Json;

namespace ClaudeAgentSdk.Messages;

/// <summary>
/// Base type for content blocks within messages.
/// Mapped from the "type" field in JSON. A block with an unknown type is dropped.
/// </summary>
public abstract record ContentBlock;

public sealed record TextBlock : ContentBlock
{
    public required string Text { get; init; }
}

public sealed record ThinkingBlock : ContentBlock
{
    public required string Thinking { get; init; }
    public required string Signature { get; init; }
}

public sealed record ToolUseBlock : ContentBlock
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required JsonElement Input { get; init; }
}

public sealed record ToolResultBlock : ContentBlock
{
    public required string ToolUseId { get; init; }
    public JsonElement? Content { get; init; }
    public bool? IsError { get; init; }
}
