using System.Text.Json;

namespace ClaudeAgentSdk.Permissions;

public abstract record PermissionResult;

public sealed record PermissionResultAllow : PermissionResult
{
    public JsonElement? UpdatedInput { get; init; }
}

public sealed record PermissionResultDeny : PermissionResult
{
    public string Message { get; init; } = "";
    public bool Interrupt { get; init; }
}

public sealed record ToolPermissionContext
{
    public IReadOnlyList<JsonElement>? Suggestions { get; init; }
}

/// <summary>
/// Callback for tool permission decisions.
/// Receives tool name, input, and context. Returns allow/deny.
/// </summary>
public delegate Task<PermissionResult> CanUseToolCallback(
    string toolName, JsonElement input, ToolPermissionContext context,
    CancellationToken cancellationToken);
