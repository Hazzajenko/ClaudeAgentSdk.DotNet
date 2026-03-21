using System.Text.Json;

namespace ClaudeAgentSdk.Messages;

/// <summary>
/// Base type for all messages from the Claude CLI.
/// Discriminated by the "type" field in JSON: "user", "assistant", "system", "result", "stream_event", "rate_limit_event".
/// </summary>
public abstract record Message;

public sealed record UserMessage : Message
{
    public required object Content { get; init; } // string or IReadOnlyList<ContentBlock>
    public string? Uuid { get; init; }
    public string? ParentToolUseId { get; init; }
    public JsonElement? ToolUseResult { get; init; }
}

public sealed record AssistantMessage : Message
{
    public required IReadOnlyList<ContentBlock> Content { get; init; }
    public required string Model { get; init; }
    public string? ParentToolUseId { get; init; }
    public string? Error { get; init; }
    public JsonElement? Usage { get; init; }
}

public sealed record SystemMessage : Message
{
    public required string Subtype { get; init; }
    public required JsonElement Data { get; init; }
}

public sealed record ResultMessage : Message
{
    public required string Subtype { get; init; }
    public required int DurationMs { get; init; }
    public required int DurationApiMs { get; init; }
    public required bool IsError { get; init; }
    public required int NumTurns { get; init; }
    public required string SessionId { get; init; }
    public string? StopReason { get; init; }
    public double? TotalCostUsd { get; init; }
    public JsonElement? Usage { get; init; }
    public string? Result { get; init; }
    public JsonElement? StructuredOutput { get; init; }
}

public sealed record StreamEvent : Message
{
    public required string Uuid { get; init; }
    public required string SessionId { get; init; }
    public required JsonElement Event { get; init; }
    public string? ParentToolUseId { get; init; }
}

public sealed record RateLimitInfo
{
    public required string Status { get; init; }
    public long? ResetsAt { get; init; }
    public string? RateLimitType { get; init; }
    public double? Utilization { get; init; }
    public string? OverageStatus { get; init; }
    public long? OverageResetsAt { get; init; }
    public string? OverageDisabledReason { get; init; }
}

public sealed record RateLimitEvent : Message
{
    public required RateLimitInfo RateLimitInfo { get; init; }
    public required string Uuid { get; init; }
    public required string SessionId { get; init; }
}
