using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    /// <summary>Text when content is a plain string, null otherwise.</summary>
    public string? TextContent => Content as string;

    /// <summary>Content blocks when structured, null otherwise.</summary>
    public IReadOnlyList<ContentBlock>? ContentBlocks => Content as IReadOnlyList<ContentBlock>;
}

public sealed record AssistantMessage : Message
{
    public required IReadOnlyList<ContentBlock> Content { get; init; }
    public required string Model { get; init; }
    public string? ParentToolUseId { get; init; }
    public string? Error { get; init; }
    public Usage? Usage { get; init; }

    /// <summary>
    /// Concatenated text from all <see cref="TextBlock"/> content. Null if no text blocks.
    /// </summary>
    public string? Text
    {
        get
        {
            string? first = null;
            StringBuilder? sb = null;
            foreach (var block in Content)
            {
                if (block is not TextBlock { Text: var t }) continue;
                if (first is null) { first = t; continue; }
                sb ??= new StringBuilder(first);
                sb.Append(t);
            }
            return sb?.ToString() ?? first;
        }
    }
}

public record SystemMessage : Message
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
    public Usage? Usage { get; init; }
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

/// <summary>
/// Token usage statistics from the Anthropic API.
/// All fields nullable — the CLI may emit a subset (e.g. result messages may only have input_tokens).
/// Unknown fields from future API versions are captured in <see cref="ExtensionData"/>.
/// </summary>
public sealed class Usage
{
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? CacheCreationInputTokens { get; set; }
    public int? CacheReadInputTokens { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
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

/// <summary>
/// The CLI sends this after <c>/clear</c> starts a new conversation in the same session.
/// </summary>
public sealed record ConversationResetMessage : Message
{
    public required string NewConversationId { get; init; }
    public required string Uuid { get; init; }
    public required string SessionId { get; init; }
}

// Internal control protocol messages — not yielded to public API consumers
internal sealed record ControlResponseMessage(JsonElement RawElement) : Message;
internal sealed record ControlRequestMessage(JsonElement RawElement) : Message;
internal sealed record ControlCancelRequestMessage(string RequestId) : Message;

public readonly record struct TaskUsage(int TotalTokens, int ToolUses, int DurationMs);

public sealed record TaskStartedMessage : SystemMessage
{
    public required string TaskId { get; init; }
    public required string Description { get; init; }
    public required string Uuid { get; init; }
    public required string SessionId { get; init; }
    public string? ToolUseId { get; init; }
    public string? TaskType { get; init; }
}

public sealed record TaskProgressMessage : SystemMessage
{
    public required string TaskId { get; init; }
    public required string Description { get; init; }
    public required TaskUsage Usage { get; init; }
    public required string Uuid { get; init; }
    public required string SessionId { get; init; }
    public string? ToolUseId { get; init; }
    public string? LastToolName { get; init; }
}

public sealed record TaskNotificationMessage : SystemMessage
{
    public required string TaskId { get; init; }
    public required string Status { get; init; }
    public required string OutputFile { get; init; }
    public required string Summary { get; init; }
    public required string Uuid { get; init; }
    public required string SessionId { get; init; }
    public string? ToolUseId { get; init; }
    public TaskUsage? Usage { get; init; }
}
