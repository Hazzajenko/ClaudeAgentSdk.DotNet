using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Json.Wire;

internal sealed record AssistantMessageWire
{
    public required AssistantMessageBodyWire Message { get; init; }
    public string? ParentToolUseId { get; init; }
    public string? Error { get; init; }
    public string? SessionId { get; init; }
    public string? Uuid { get; init; }
}

internal sealed record AssistantMessageBodyWire
{
    public required List<ContentBlockWire> Content { get; init; }
    public required string Model { get; init; }
    public Usage? Usage { get; init; }
    public string? Id { get; init; }
    public string? StopReason { get; init; }
}

internal sealed record UserMessageWire
{
    public required UserMessageBodyWire Message { get; init; }
    public string? Uuid { get; init; }
    public string? ParentToolUseId { get; init; }
    public JsonElement? ToolUseResult { get; init; }
    public JsonElement? Origin { get; init; }
}

internal sealed record UserMessageBodyWire
{
    public required JsonElement Content { get; init; }
}

internal sealed record SystemMessageWire
{
    public required string Subtype { get; init; }
    public JsonElement? Data { get; init; }
}

internal sealed record ResultMessageWire
{
    public required string Subtype { get; init; }
    public required int DurationMs { get; init; }
    public required int DurationApiMs { get; init; }
    public required bool IsError { get; init; }
    public required int NumTurns { get; init; }
    public required string SessionId { get; init; }
    public string? StopReason { get; init; }
    public double? TotalCostUsd { get; init; }
    public Usage? Usage { get; init; }
    public string? Result { get; init; }
    public JsonElement? StructuredOutput { get; init; }
    [JsonPropertyName("modelUsage")]
    public Dictionary<string, ModelUsage>? ModelUsage { get; init; }
    public JsonElement? PermissionDenials { get; init; }
    public DeferredToolUse? DeferredToolUse { get; init; }
    public List<string>? Errors { get; init; }
    public int? ApiErrorStatus { get; init; }
    public string? TerminalReason { get; init; }
    public string? Uuid { get; init; }
    public JsonElement? Origin { get; init; }
}

internal sealed record StreamEventWire
{
    public required string Uuid { get; init; }
    public required string SessionId { get; init; }
    public required JsonElement Event { get; init; }
    public string? ParentToolUseId { get; init; }
}

internal sealed record RateLimitEventWire
{
    public required string Uuid { get; init; }
    public required string SessionId { get; init; }
    // Kept as JSON so the mapper can give the whole object as RateLimitInfo.Raw.
    public required JsonElement RateLimitInfo { get; init; }
}
