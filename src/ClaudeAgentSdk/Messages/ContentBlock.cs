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

/// <summary>
/// A tool call that the API runs on the server for the model, for example web_search or advisor.
/// The caller does not send a result back. Branch on <see cref="Name"/> to find the tool.
/// </summary>
public sealed record ServerToolUseBlock : ContentBlock
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required JsonElement Input { get; init; }
}

/// <summary>
/// The result of a server tool call, for example advisor_tool_result or web_search_tool_result.
/// <see cref="Content"/> is the raw JSON from the API.
/// </summary>
public sealed record ServerToolResultBlock : ContentBlock
{
    public required string ToolUseId { get; init; }
    public required JsonElement Content { get; init; }
}
