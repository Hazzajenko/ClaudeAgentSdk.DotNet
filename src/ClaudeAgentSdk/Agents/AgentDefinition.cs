namespace ClaudeAgentSdk.Agents;

public sealed record AgentDefinition
{
    public required string Description { get; init; }
    public required string Prompt { get; init; }
    public IReadOnlyList<string>? Tools { get; init; }

    /// <summary>
    /// A model alias from <see cref="AgentModel"/> or a full model id.
    /// </summary>
    public string? Model { get; init; }

    public IReadOnlyList<string>? Skills { get; init; }
    public string? Memory { get; init; }
    public IReadOnlyList<string>? McpServers { get; init; }
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
