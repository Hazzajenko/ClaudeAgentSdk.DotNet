using ClaudeAgentSdk.Mcp;

namespace ClaudeAgentSdk.Agents;

public sealed record AgentDefinition
{
    public required string Description { get; init; }
    public required string Prompt { get; init; }
    public IReadOnlyList<string>? Tools { get; init; }
    public IReadOnlyList<string>? DisallowedTools { get; init; }

    /// <summary>
    /// A model alias from <see cref="AgentModel"/> or a full model id.
    /// </summary>
    public string? Model { get; init; }

    public IReadOnlyList<string>? Skills { get; init; }
    public string? Memory { get; init; }

    /// <summary>
    /// Names of MCP servers that the agent can use.
    /// </summary>
    public IReadOnlyList<string>? McpServers { get; init; }

    /// <summary>
    /// MCP servers that are defined only for this agent. They go into the same
    /// <c>mcpServers</c> array as <see cref="McpServers"/>. An SDK server cannot be inline.
    /// </summary>
    public IReadOnlyDictionary<string, McpServerConfig>? InlineMcpServers { get; init; }

    public string? InitialPrompt { get; init; }
    public int? MaxTurns { get; init; }
    public bool? Background { get; init; }
    public EffortLevel? Effort { get; init; }
    public PermissionMode? PermissionMode { get; init; }
}

/// <summary>
/// Model aliases for <see cref="AgentDefinition.Model"/>.
/// </summary>
public static class AgentModel
{
    public const string Sonnet = "sonnet";
    public const string Opus = "opus";
    public const string Haiku = "haiku";
    public const string Inherit = "inherit";
}
