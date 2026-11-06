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

    /// <summary>Create an image result from raw image bytes.</summary>
    /// <param name="data">The image bytes. They are sent base64 encoded.</param>
    /// <param name="mimeType">The image MIME type, for example <c>image/png</c>.</param>
    public static McpToolResult Image(byte[] data, string mimeType) =>
        new McpImageToolResult { Data = Convert.ToBase64String(data), MimeType = mimeType };

    /// <summary>Create a result that points to a resource.</summary>
    public static McpToolResult ResourceLink(string uri, string name, string? description = null) =>
        new McpResourceLinkToolResult { Uri = uri, Name = name, Description = description };

    internal abstract void WriteContentTo(Utf8JsonWriter writer);

    private protected static void WriteTextContent(Utf8JsonWriter writer, string text)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "text");
        writer.WriteString("text", text);
        writer.WriteEndObject();
    }
}

public sealed record McpTextToolResult : McpToolResult
{
    public required string Value { get; init; }

    internal override void WriteContentTo(Utf8JsonWriter writer) => WriteTextContent(writer, Value);
}

/// <summary>
/// Image result, sent as MCP <c>image</c> content.
/// </summary>
public sealed record McpImageToolResult : McpToolResult
{
    /// <summary>The base64 encoded image data.</summary>
    public required string Data { get; init; }

    /// <summary>The image MIME type, for example <c>image/png</c>.</summary>
    public required string MimeType { get; init; }

    internal override void WriteContentTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("type", "image");
        writer.WriteString("data", Data);
        writer.WriteString("mimeType", MimeType);
        writer.WriteEndObject();
    }
}

/// <summary>
/// Result that points to a resource.
/// </summary>
/// <remarks>
/// The CLI shows tool results as text, so the link is sent as text content with the
/// name, the URI and the description on separate lines. The Python SDK does the same.
/// </remarks>
public sealed record McpResourceLinkToolResult : McpToolResult
{
    public required string Uri { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }

    internal override void WriteContentTo(Utf8JsonWriter writer)
    {
        string[] parts = new[] { Name, Uri, Description }
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => p!)
            .ToArray();
        WriteTextContent(writer, parts.Length > 0 ? string.Join('\n', parts) : "Resource link");
    }
}
