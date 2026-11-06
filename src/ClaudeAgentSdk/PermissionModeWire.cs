namespace ClaudeAgentSdk;

/// <summary>
/// Maps <see cref="PermissionMode"/> to the string the CLI reads in
/// <c>--permission-mode</c> and in the <c>set_permission_mode</c> control request.
/// </summary>
internal static class PermissionModeWire
{
    public static string ToWire(PermissionMode mode) => mode switch
    {
        PermissionMode.AcceptEdits => "acceptEdits",
        PermissionMode.Plan => "plan",
        PermissionMode.BypassPermissions => "bypassPermissions",
        PermissionMode.DontAsk => "dontAsk",
        PermissionMode.Auto => "auto",
        _ => "default"
    };
}
