namespace ClaudeAgentSdk.Mcp;

public sealed record McpServerInfo(string Name, string Version);

public sealed record McpToolAnnotations
{
    public bool? ReadOnly { get; init; }
    public bool? Destructive { get; init; }
    public bool? OpenWorld { get; init; }
}

public sealed record McpToolInfo(
    string Name,
    string? Description = null,
    McpToolAnnotations? Annotations = null);

public sealed record McpServerStatus
{
    public required string Name { get; init; }
    public required string Status { get; init; }
    public McpServerInfo? ServerInfo { get; init; }
    public string? Error { get; init; }
    public string? Scope { get; init; }
    public IReadOnlyList<McpToolInfo>? Tools { get; init; }
}

public sealed record McpStatusResponse(IReadOnlyList<McpServerStatus> McpServers);
