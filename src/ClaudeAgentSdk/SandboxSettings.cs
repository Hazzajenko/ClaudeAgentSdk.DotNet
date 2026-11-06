namespace ClaudeAgentSdk;

public sealed record SandboxNetworkConfig
{
    public IReadOnlyList<string>? AllowUnixSockets { get; init; }
    public bool? AllowAllUnixSockets { get; init; }
    public bool? AllowLocalBinding { get; init; }
    public int? HttpProxyPort { get; init; }
    public int? SocksProxyPort { get; init; }
}

public sealed record SandboxSettings
{
    public bool? Enabled { get; init; }
    public bool? AutoAllowBashIfSandboxed { get; init; }
    public IReadOnlyList<string>? ExcludedCommands { get; init; }
    public bool? AllowUnsandboxedCommands { get; init; }
    public SandboxNetworkConfig? Network { get; init; }

    /// <summary>
    /// Sandbox violations to leave unreported. Each key is a command pattern and each value is a list of strings.
    /// A violation is left unreported when the command text contains the pattern and the violation text
    /// contains one of the strings. The pattern <c>"*"</c> matches every command. Both checks are plain
    /// substring matches. This changes only which violations are reported, not what the sandbox blocks.
    /// </summary>
    /// <example><c>IgnoreViolations = new Dictionary&lt;string, IReadOnlyList&lt;string&gt;&gt; { ["*"] = ["/etc/hosts"] }</c></example>
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? IgnoreViolations { get; init; }

    public bool? EnableWeakerNestedSandbox { get; init; }
}
