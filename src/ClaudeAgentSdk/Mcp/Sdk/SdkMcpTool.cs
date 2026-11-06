using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ClaudeAgentSdk.Json;

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

    /// <summary>
    /// Size in characters up to which Claude Code keeps this tool's result inline.
    /// A larger result is saved to a file and the model sees a preview.
    /// Null uses the Claude Code default.
    /// </summary>
    /// <remarks>
    /// Sent in <c>tools/list</c> as <c>_meta: {"anthropic/maxResultSizeChars": N}</c>.
    /// </remarks>
    public int? MaxResultSizeChars { get; init; }

    /// <summary>Create with async handler taking raw <see cref="JsonElement"/> input.</summary>
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

    /// <summary>Create with sync handler taking raw <see cref="JsonElement"/> input.</summary>
    public static SdkMcpTool Create(
        string name,
        string description,
        JsonElement inputSchema,
        Func<JsonElement, CancellationToken, McpToolResult> syncHandler,
        McpToolAnnotations? annotations = null) => new()
    {
        Name = name,
        Description = description,
        InputSchema = inputSchema,
        Handler = (input, ct) => Task.FromResult(syncHandler(input, ct)),
        Annotations = annotations
    };

    /// <summary>
    /// Create with async typed handler. Schema auto-derived from <paramref name="inputTypeInfo"/>.
    /// </summary>
    public static SdkMcpTool Create<T>(
        string name,
        string description,
        JsonTypeInfo<T> inputTypeInfo,
        Func<T, CancellationToken, Task<McpToolResult>> handler,
        McpToolAnnotations? annotations = null) => new()
    {
        Name = name,
        Description = description,
        InputSchema = JsonSchemaDeriver.FromTypeInfo(inputTypeInfo),
        Handler = (input, ct) =>
        {
            var typed = input.Deserialize(inputTypeInfo)
                ?? throw new JsonException($"Failed to deserialize input for tool '{name}'");
            return handler(typed, ct);
        },
        Annotations = annotations
    };

    /// <summary>
    /// Create with sync typed handler. Schema auto-derived from <paramref name="inputTypeInfo"/>.
    /// </summary>
    public static SdkMcpTool Create<T>(
        string name,
        string description,
        JsonTypeInfo<T> inputTypeInfo,
        Func<T, CancellationToken, McpToolResult> syncHandler,
        McpToolAnnotations? annotations = null) => new()
    {
        Name = name,
        Description = description,
        InputSchema = JsonSchemaDeriver.FromTypeInfo(inputTypeInfo),
        Handler = (input, ct) =>
        {
            var typed = input.Deserialize(inputTypeInfo)
                ?? throw new JsonException($"Failed to deserialize input for tool '{name}'");
            return Task.FromResult(syncHandler(typed, ct));
        },
        Annotations = annotations
    };
}
