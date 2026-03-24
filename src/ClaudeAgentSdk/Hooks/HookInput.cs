using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeAgentSdk.Hooks;

/// <summary>
/// Base type for all hook inputs. Deserialized from CLI's snake_case JSON via ClaudeJsonContext.
/// Polymorphic dispatch on hook_event_name.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "hook_event_name")]
[JsonDerivedType(typeof(PreToolUseInput), "PreToolUse")]
[JsonDerivedType(typeof(PostToolUseInput), "PostToolUse")]
[JsonDerivedType(typeof(PostToolUseFailureInput), "PostToolUseFailure")]
[JsonDerivedType(typeof(UserPromptSubmitInput), "UserPromptSubmit")]
[JsonDerivedType(typeof(StopInput), "Stop")]
[JsonDerivedType(typeof(SubagentStopInput), "SubagentStop")]
[JsonDerivedType(typeof(PreCompactInput), "PreCompact")]
[JsonDerivedType(typeof(NotificationInput), "Notification")]
[JsonDerivedType(typeof(SubagentStartInput), "SubagentStart")]
[JsonDerivedType(typeof(PermissionRequestInput), "PermissionRequest")]
public abstract record HookInput
{
    public required string SessionId { get; init; }
    public required string TranscriptPath { get; init; }
    public required string Cwd { get; init; }
    public string? PermissionMode { get; init; }
}

// Tool-lifecycle inputs share optional subagent context fields
public sealed record PreToolUseInput : HookInput
{
    public required string ToolName { get; init; }
    public required JsonElement ToolInput { get; init; }
    public required string ToolUseId { get; init; }
    public string? AgentId { get; init; }
    public string? AgentType { get; init; }
}

public sealed record PostToolUseInput : HookInput
{
    public required string ToolName { get; init; }
    public required JsonElement ToolInput { get; init; }
    public required JsonElement ToolResponse { get; init; }
    public required string ToolUseId { get; init; }
    public string? AgentId { get; init; }
    public string? AgentType { get; init; }
}

public sealed record PostToolUseFailureInput : HookInput
{
    public required string ToolName { get; init; }
    public required JsonElement ToolInput { get; init; }
    public required string ToolUseId { get; init; }
    public required string Error { get; init; }
    public bool? IsInterrupt { get; init; }
    public string? AgentId { get; init; }
    public string? AgentType { get; init; }
}

public sealed record UserPromptSubmitInput : HookInput
{
    public required string Prompt { get; init; }
}

public sealed record StopInput : HookInput
{
    public required bool StopHookActive { get; init; }
}

public sealed record SubagentStopInput : HookInput
{
    public required bool StopHookActive { get; init; }
    public required string AgentId { get; init; }
    public required string AgentTranscriptPath { get; init; }
    public required string AgentType { get; init; }
}

public sealed record PreCompactInput : HookInput
{
    public required string Trigger { get; init; }
    public string? CustomInstructions { get; init; }
}

public sealed record NotificationInput : HookInput
{
    public required string Message { get; init; }
    public string? Title { get; init; }
    public required string NotificationType { get; init; }
}

public sealed record SubagentStartInput : HookInput
{
    public required string AgentId { get; init; }
    public required string AgentType { get; init; }
}

public sealed record PermissionRequestInput : HookInput
{
    public required string ToolName { get; init; }
    public required JsonElement ToolInput { get; init; }
    public JsonElement? PermissionSuggestions { get; init; }
    public string? AgentId { get; init; }
    public string? AgentType { get; init; }
}
