namespace ClaudeAgentSdk;

public sealed record SandboxNetworkConfig
{
    public IReadOnlyList<string>? AllowUnixSockets { get; init; }
    public bool? AllowAllUnixSockets { get; init; }
    public bool? AllowLocalBinding { get; init; }
    public int? HttpProxyPort { get; init; }
    public int? SocksProxyPort { get; init; }
}

public sealed record SandboxIgnoreViolations
{
    public IReadOnlyList<string>? File { get; init; }
    public IReadOnlyList<string>? Network { get; init; }
}

public sealed record SandboxSettings
{
    public bool? Enabled { get; init; }
    public bool? AutoAllowBashIfSandboxed { get; init; }
    public IReadOnlyList<string>? ExcludedCommands { get; init; }
    public bool? AllowUnsandboxedCommands { get; init; }
    public SandboxNetworkConfig? Network { get; init; }
    public SandboxIgnoreViolations? IgnoreViolations { get; init; }
    public bool? EnableWeakerNestedSandbox { get; init; }
}
