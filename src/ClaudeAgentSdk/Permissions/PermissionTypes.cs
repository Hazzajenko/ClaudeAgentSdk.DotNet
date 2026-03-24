using System.Text.Json;

namespace ClaudeAgentSdk.Permissions;

public abstract record PermissionResult;

public sealed record PermissionResultAllow : PermissionResult
{
    public JsonElement? UpdatedInput { get; init; }
    public IReadOnlyList<PermissionUpdate>? UpdatedPermissions { get; init; }
}

public sealed record PermissionResultDeny : PermissionResult
{
    public string Message { get; init; } = "";
    public bool Interrupt { get; init; }
}

public sealed record ToolPermissionContext
{
    public IReadOnlyList<PermissionUpdate>? Suggestions { get; init; }
}

public enum PermissionUpdateType
{
    AddRules,
    ReplaceRules,
    RemoveRules,
    SetMode,
    AddDirectories,
    RemoveDirectories
}

public sealed record PermissionRuleValue
{
    public required string ToolName { get; init; }
    public required string RuleContent { get; init; }
    public required string Behavior { get; init; }
}

public sealed record PermissionUpdate
{
    public required PermissionUpdateType Type { get; init; }
    public IReadOnlyList<PermissionRuleValue>? Rules { get; init; }
    public string? Mode { get; init; }
    public IReadOnlyList<string>? Directories { get; init; }
    public string? Destination { get; init; }
}

/// <summary>
/// Callback for tool permission decisions.
/// Receives tool name, input, and context. Returns allow/deny.
/// </summary>
public delegate Task<PermissionResult> CanUseToolCallback(
    string toolName, JsonElement input, ToolPermissionContext context,
    CancellationToken cancellationToken);
