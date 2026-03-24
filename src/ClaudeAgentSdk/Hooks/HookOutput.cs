using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeAgentSdk.Json;

namespace ClaudeAgentSdk.Hooks;

/// <summary>
/// Typed hook callback response. Serialized to camelCase JSON for the CLI.
/// </summary>
public sealed record HookOutput
{
    public static readonly HookOutput Empty = new();

    [JsonPropertyName("continue")]
    public bool? Continue { get; init; }

    public bool? SuppressOutput { get; init; }
    public string? StopReason { get; init; }
    public string? Decision { get; init; }
    public string? SystemMessage { get; init; }
    public string? Reason { get; init; }
    public HookSpecificOutput? HookSpecificOutput { get; init; }

    /// <summary>
    /// Serialize to JsonElement for the control protocol response.
    /// </summary>
    public JsonElement ToJsonElement() =>
        JsonSerializer.SerializeToElement(this, HookJsonContext.Default.HookOutput);
}

/// <summary>
/// Base type for hook-specific output. The hookEventName discriminator is auto-written by STJ
/// polymorphic serialization — users never set it manually.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "hookEventName")]
[JsonDerivedType(typeof(PreToolUseOutput), "PreToolUse")]
[JsonDerivedType(typeof(PostToolUseOutput), "PostToolUse")]
[JsonDerivedType(typeof(PostToolUseFailureOutput), "PostToolUseFailure")]
[JsonDerivedType(typeof(UserPromptSubmitOutput), "UserPromptSubmit")]
[JsonDerivedType(typeof(StopOutput), "Stop")]
[JsonDerivedType(typeof(SubagentStopOutput), "SubagentStop")]
[JsonDerivedType(typeof(PreCompactOutput), "PreCompact")]
[JsonDerivedType(typeof(NotificationOutput), "Notification")]
[JsonDerivedType(typeof(SubagentStartOutput), "SubagentStart")]
[JsonDerivedType(typeof(PermissionRequestOutput), "PermissionRequest")]
public abstract record HookSpecificOutput;

public sealed record PreToolUseOutput : HookSpecificOutput
{
    public string? PermissionDecision { get; init; }
    public string? PermissionDecisionReason { get; init; }
    public JsonElement? UpdatedInput { get; init; }
    public string? AdditionalContext { get; init; }
}

public sealed record PostToolUseOutput : HookSpecificOutput
{
    public string? AdditionalContext { get; init; }
}

public sealed record PostToolUseFailureOutput : HookSpecificOutput
{
    public string? AdditionalContext { get; init; }
}

public sealed record UserPromptSubmitOutput : HookSpecificOutput
{
    public string? AdditionalContext { get; init; }
}

public sealed record StopOutput : HookSpecificOutput
{
    public string? AdditionalContext { get; init; }
}

public sealed record SubagentStopOutput : HookSpecificOutput
{
    public string? AdditionalContext { get; init; }
}

public sealed record PreCompactOutput : HookSpecificOutput
{
    public string? AdditionalContext { get; init; }
}

public sealed record NotificationOutput : HookSpecificOutput
{
    public string? AdditionalContext { get; init; }
}

public sealed record SubagentStartOutput : HookSpecificOutput
{
    public string? AdditionalContext { get; init; }
}

public sealed record PermissionRequestOutput : HookSpecificOutput
{
    public required JsonElement Decision { get; init; }
}
