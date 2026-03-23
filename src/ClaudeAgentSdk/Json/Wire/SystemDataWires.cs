using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Json.Wire;

internal sealed record TaskStartedDataWire
{
    public required string TaskId { get; init; }
    public required string Description { get; init; }
    public required string Uuid { get; init; }
    public required string SessionId { get; init; }
    public string? ToolUseId { get; init; }
    public string? TaskType { get; init; }
}

internal sealed record TaskProgressDataWire
{
    public required string TaskId { get; init; }
    public required string Description { get; init; }
    public required string Uuid { get; init; }
    public required string SessionId { get; init; }
    public required TaskUsage Usage { get; init; }
    public string? ToolUseId { get; init; }
    public string? LastToolName { get; init; }
}

internal sealed record TaskNotificationDataWire
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
