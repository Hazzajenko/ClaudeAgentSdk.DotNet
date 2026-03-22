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
