using System.Text.Json;

namespace ClaudeAgentSdk.Mcp.Sdk;

/// <summary>
/// Async handler for an SDK MCP tool invocation.
/// </summary>
public delegate Task<McpToolResult> SdkMcpToolHandler(
    JsonElement input, CancellationToken cancellationToken);

/// <summary>
/// Definition of an in-process MCP tool.
/// </summary>
public sealed record SdkMcpTool
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required JsonElement InputSchema { get; init; }
    public required SdkMcpToolHandler Handler { get; init; }
    public McpToolAnnotations? Annotations { get; init; }

    public static SdkMcpTool Create(
        string name,
        string description,
        JsonElement inputSchema,
        SdkMcpToolHandler handler,
        McpToolAnnotations? annotations = null) => new()
    {
        Name = name,
        Description = description,
        InputSchema = inputSchema,
        Handler = handler,
        Annotations = annotations
    };
}
