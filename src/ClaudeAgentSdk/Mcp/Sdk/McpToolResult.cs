using System.Text.Json;

namespace ClaudeAgentSdk.Mcp.Sdk;

/// <summary>
/// Result returned by an SDK MCP tool handler.
/// </summary>
public abstract record McpToolResult
{
    public bool IsError { get; init; }

    public static McpToolResult Text(string text) => new McpTextToolResult { Value = text };

    public static McpToolResult Error(string text) => new McpTextToolResult { Value = text, IsError = true };

    internal abstract void WriteContentTo(Utf8JsonWriter writer);
}

public sealed record McpTextToolResult : McpToolResult
{
    public required string Value { get; init; }

    internal override void WriteContentTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "text");
        writer.WriteString("text", Value);
        writer.WriteEndObject();
    }
}
