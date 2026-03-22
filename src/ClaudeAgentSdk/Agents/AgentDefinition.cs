namespace ClaudeAgentSdk.Agents;

public sealed record AgentDefinition
{
    public required string Description { get; init; }
    public required string Prompt { get; init; }
    public IReadOnlyList<string>? Tools { get; init; }
    public AgentModel? Model { get; init; }
    public IReadOnlyList<string>? Skills { get; init; }
    public string? Memory { get; init; }
    public IReadOnlyList<string>? McpServers { get; init; }
}

public enum AgentModel
{
    Sonnet,
    Opus,
    Haiku,
    Inherit
}
