using System.Text.Json;
using ClaudeAgentSdk.Agents;
using ClaudeAgentSdk.Hooks;
using ClaudeAgentSdk.Mcp;
using ClaudeAgentSdk.Permissions;

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

    /// <summary>
    /// Sends <c>--include-hook-events</c>, so the CLI emits a <see cref="Messages.HookEventMessage"/> for every hook.
    /// The CLI sends SessionStart and Setup hook events without this option too.
    /// </summary>
    public bool IncludeHookEvents { get; init; }
    public EffortLevel? Effort { get; init; }
    public ThinkingConfig? Thinking { get; init; }
    public bool ForkSession { get; init; }
    public IReadOnlyList<string>? Betas { get; init; }
    public bool EnableFileCheckpointing { get; init; }
    public IReadOnlyList<string>? SettingSources { get; init; }
    public IReadOnlyList<SdkPluginConfig>? Plugins { get; init; }
    public string? Settings { get; init; }
    public SandboxSettings? Sandbox { get; init; }
    public IReadOnlyList<string>? AddDirectories { get; init; }
    public IReadOnlyDictionary<string, string?>? ExtraArgs { get; init; }
    public JsonElement? OutputFormatSchema { get; init; }
    public JsonElement? OutputFormat { get; init; }
    public int? MaxBufferSize { get; init; }
    public IReadOnlyDictionary<string, McpServerConfig>? McpServers { get; init; }
    public IReadOnlyDictionary<string, AgentDefinition>? Agents { get; init; }
    public IReadOnlyDictionary<HookEvent, IReadOnlyList<HookMatcher>>? Hooks { get; init; }
    public CanUseToolCallback? CanUseTool { get; init; }
    public IReadOnlyDictionary<string, string>? EnvironmentVariables { get; init; }
    public Action<string>? StderrCallback { get; init; }

    /// <summary>
    /// Called for every message received from the CLI, before it's yielded to the caller.
    /// Useful for debugging — e.g. <c>msg => Console.Error.WriteLine($"[debug] {msg.GetType().Name}")</c>
    /// </summary>
    public Action<Messages.Message>? MessageCallback { get; init; }
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

public sealed record SdkPluginConfig(string Type, string Path);
