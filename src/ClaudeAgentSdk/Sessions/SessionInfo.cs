using System.Text.Json;

namespace ClaudeAgentSdk.Sessions;

public sealed record SessionInfo
{
    public required string SessionId { get; init; }
    public required string Summary { get; init; }
    public required long LastModified { get; init; }
    public long? FileSize { get; init; }
    public string? CustomTitle { get; init; }
    public string? FirstPrompt { get; init; }
    public string? GitBranch { get; init; }
    public string? Cwd { get; init; }
    public string? Tag { get; init; }
    public long? CreatedAt { get; init; }
}

public sealed record SessionMessage
{
    public required string Type { get; init; }
    public required string Uuid { get; init; }
    public required string SessionId { get; init; }
    public required JsonElement Message { get; init; }
}
