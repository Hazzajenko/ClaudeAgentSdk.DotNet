using System.Text.Json;

namespace ClaudeAgentSdk.Hooks;

public enum HookEvent
{
    PreToolUse,
    PostToolUse,
    PostToolUseFailure,
    UserPromptSubmit,
    Stop,
    SubagentStop,
    PreCompact,
    Notification,
    SubagentStart,
    PermissionRequest
}

/// <summary>
/// Hook callback. Receives raw JsonElement input for AOT safety.
/// Return a JsonElement representing the hook output (e.g., {"continue": true}).
/// </summary>
public delegate Task<JsonElement> HookCallback(
    JsonElement input, string? toolUseId, CancellationToken cancellationToken);

public sealed record HookMatcher
{
    public string? Matcher { get; init; }
    public required IReadOnlyList<HookCallback> Hooks { get; init; }
    public double? TimeoutSeconds { get; init; }
}

internal static class HookEventExtensions
{
    public static string ToWireString(this HookEvent evt) => evt switch
    {
        HookEvent.PreToolUse => "PreToolUse",
        HookEvent.PostToolUse => "PostToolUse",
        HookEvent.PostToolUseFailure => "PostToolUseFailure",
        HookEvent.UserPromptSubmit => "UserPromptSubmit",
        HookEvent.Stop => "Stop",
        HookEvent.SubagentStop => "SubagentStop",
        HookEvent.PreCompact => "PreCompact",
        HookEvent.Notification => "Notification",
        HookEvent.SubagentStart => "SubagentStart",
        HookEvent.PermissionRequest => "PermissionRequest",
        _ => evt.ToString()
    };
}
