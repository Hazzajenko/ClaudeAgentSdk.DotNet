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
/// Typed hook callback. Receives a strongly-typed <see cref="HookInput"/> and returns a <see cref="HookOutput"/>.
/// </summary>
public delegate Task<HookOutput> HookCallback(HookInput input, CancellationToken cancellationToken);

public sealed record HookMatcher
{
    public string? Matcher { get; init; }
    public required IReadOnlyList<HookCallback> Hooks { get; init; }
    public double? TimeoutSeconds { get; init; }

    /// <summary>Create with async callback.</summary>
    public static HookMatcher Create(
        string? matcher, HookCallback callback, double? timeoutSeconds = null) => new()
    {
        Matcher = matcher,
        Hooks = [callback],
        TimeoutSeconds = timeoutSeconds
    };

    /// <summary>Create with sync callback (wraps in Task.FromResult).</summary>
    public static HookMatcher Create(
        string? matcher, Func<HookInput, HookOutput> syncCallback, double? timeoutSeconds = null) => new()
    {
        Matcher = matcher,
        Hooks = [(input, _) => Task.FromResult(syncCallback(input))],
        TimeoutSeconds = timeoutSeconds
    };
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
