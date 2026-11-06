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
    public EffortLevel? Effort { get; init; }
    public ThinkingConfig? Thinking { get; init; }
    public bool ForkSession { get; init; }
    public IReadOnlyList<string>? Betas { get; init; }
    public bool EnableFileCheckpointing { get; init; }
    public IReadOnlyList<string>? SettingSources { get; init; }

    /// <summary>
    /// Skills to turn on for the main session. <see cref="SkillsConfigAll"/> adds the bare
    /// <c>Skill</c> rule to <c>--allowedTools</c>. <see cref="SkillsConfigList"/> adds one
    /// <c>Skill(name)</c> rule for each name and sends the names in the initialize request,
    /// so the CLI loads only those skills. When set and <see cref="SettingSources"/> is null,
    /// the setting sources become <c>user,project</c> so the CLI finds installed skills.
    /// Null adds nothing. An invalid name throws <see cref="ArgumentException"/> at connect.
    /// This is a context filter. The files of other skills stay readable through tools such as Read.
    /// </summary>
    public SkillsConfig? Skills { get; init; }
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

/// <summary>
/// The value of <see cref="ClaudeClientOptions.Skills"/>.
/// </summary>
public abstract record SkillsConfig;

/// <summary>
/// Turns on every skill that the CLI finds.
/// </summary>
public sealed record SkillsConfigAll : SkillsConfig;

/// <summary>
/// Turns on only the named skills. A name is the skill directory name, or
/// <c>plugin:skill</c> for a plugin skill. An empty list hides every skill.
/// </summary>
public sealed record SkillsConfigList(IReadOnlyList<string> Names) : SkillsConfig;

public sealed record SdkPluginConfig(string Type, string Path);
