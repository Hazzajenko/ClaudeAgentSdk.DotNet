using ClaudeAgentSdk.Mcp.Sdk;

namespace ClaudeAgentSdk.Mcp;

public abstract record McpServerConfig;

public sealed record McpStdioServerConfig(
    string Command,
    IReadOnlyList<string>? Args = null,
    IReadOnlyDictionary<string, string>? Env = null) : McpServerConfig;

public sealed record McpSseServerConfig(
    string Url,
    IReadOnlyDictionary<string, string>? Headers = null) : McpServerConfig;

public sealed record McpHttpServerConfig(
    string Url,
    IReadOnlyDictionary<string, string>? Headers = null) : McpServerConfig;

/// <summary>
/// In-process MCP server that runs tools within the SDK process.
/// Not serialized to CLI args — handled via control protocol mcp_message routing.
/// </summary>
public sealed record McpSdkServerConfig : McpServerConfig
{
    public required string Name { get; init; }
    public required IReadOnlyList<SdkMcpTool> Tools { get; init; }
    public string Version { get; init; } = "1.0.0";

    public static McpSdkServerConfig Create(
        string name, IReadOnlyList<SdkMcpTool> tools, string version = "1.0.0") => new()
    {
        Name = name,
        Tools = tools,
        Version = version
    };

    /// <summary>
    /// Qualified tool names for <see cref="ClaudeClientOptions.AllowedTools"/>.
    /// Format: <c>mcp__{serverKey}__{toolName}</c>
    /// </summary>
    public IReadOnlyList<string> GetQualifiedToolNames(string serverKey) =>
        Tools.Select(t => $"mcp__{serverKey}__{t.Name}").ToList().AsReadOnly();
}
