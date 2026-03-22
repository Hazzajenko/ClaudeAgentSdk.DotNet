namespace ClaudeAgentSdk;

/// <summary>
/// Configuration for Claude CLI queries and sessions.
/// Immutable record — use <c>with</c> expressions to configure.
/// </summary>
public sealed record ClaudeClientOptions
{
    public string? Model { get; init; }
    public string? FallbackModel { get; init; }
    public string? SystemPrompt { get; init; }
    public int? MaxTurns { get; init; }
    public double? MaxBudgetUsd { get; init; }
    public PermissionMode PermissionMode { get; init; } = PermissionMode.Default;
    public string? WorkingDirectory { get; init; }
    public string? CliPath { get; init; }
    public IReadOnlyList<string>? AllowedTools { get; init; }
    public IReadOnlyList<string>? DisallowedTools { get; init; }
    public IReadOnlyList<string>? Tools { get; init; }
    public int? MaxThinkingTokens { get; init; }
    public bool ContinueConversation { get; init; }
    public string? Resume { get; init; }
    public bool IncludePartialMessages { get; init; }
    public EffortLevel? Effort { get; init; }
    public ThinkingConfig? Thinking { get; init; }
    public bool ForkSession { get; init; }
    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }
    public Action<string>? StderrCallback { get; init; }
}

public enum PermissionMode
{
    Default,
    AcceptEdits,
    Plan,
    BypassPermissions
}

public enum EffortLevel
{
    Low,
    Medium,
    High,
    Max
}

public abstract record ThinkingConfig;
public sealed record ThinkingConfigAdaptive : ThinkingConfig;
public sealed record ThinkingConfigEnabled(int BudgetTokens) : ThinkingConfig;
public sealed record ThinkingConfigDisabled : ThinkingConfig;
