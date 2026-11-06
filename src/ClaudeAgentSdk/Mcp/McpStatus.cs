namespace ClaudeAgentSdk.Mcp;

public sealed record McpServerInfo(string Name, string Version);

/// <summary>
/// Hints about a tool's behavior.
/// </summary>
/// <remarks>
/// An SDK MCP server writes these in <c>tools/list</c> with the MCP names <c>title</c>,
/// <c>readOnlyHint</c>, <c>destructiveHint</c>, <c>idempotentHint</c> and <c>openWorldHint</c>.
/// The CLI <c>mcp_status</c> response uses <c>readOnly</c>, <c>destructive</c> and <c>openWorld</c>,
/// and has no title or idempotent value, so <see cref="Title"/> and <see cref="Idempotent"/> stay null there.
/// </remarks>
public sealed record McpToolAnnotations
{
    /// <summary>Display name for the tool.</summary>
    public string? Title { get; init; }

    /// <summary>The tool does not change its environment.</summary>
    public bool? ReadOnly { get; init; }

    /// <summary>The tool can make changes that destroy data.</summary>
    public bool? Destructive { get; init; }

    /// <summary>A second call with the same arguments has no more effect than the first.</summary>
    public bool? Idempotent { get; init; }

    /// <summary>The tool can reach entities outside a closed domain, such as the web.</summary>
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
