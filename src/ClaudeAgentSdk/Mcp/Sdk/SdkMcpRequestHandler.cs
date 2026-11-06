using System.Text;
using System.Text.Json;

namespace ClaudeAgentSdk.Mcp.Sdk;

/// <summary>
/// Routes MCP JSONRPC messages to in-process SDK server tools.
/// </summary>
internal sealed class SdkMcpRequestHandler
{
    private readonly McpSdkServerConfig _config;
    private readonly Dictionary<string, SdkMcpTool> _toolMap;

    public SdkMcpRequestHandler(McpSdkServerConfig config)
    {
        _config = config;
        _toolMap = config.Tools.ToDictionary(t => t.Name);
    }

    public async Task<JsonElement> HandleAsync(JsonElement message, CancellationToken cancellationToken)
    {
        // JSON-RPC allows a string or a number id. Echo it back as the raw element.
        JsonElement? id = message.TryGetProperty("id", out var idProp) ? idProp : null;

        try
        {
            string? method = message.TryGetProperty("method", out var m) ? m.GetString() : null;

            return method switch
            {
                "initialize" => BuildInitializeResponse(id),
                "tools/list" => BuildToolsListResponse(id),
                "tools/call" => await HandleToolCallAsync(message, id, cancellationToken),
                "notifications/initialized" => BuildEmptyResult(id),
                _ => BuildMethodNotFoundError(id, method ?? "null")
            };
        }
        catch (Exception ex)
        {
            return BuildInternalError(id, ex.Message);
        }
    }

    private JsonElement BuildInitializeResponse(JsonElement? id)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("jsonrpc", "2.0");
            WriteId(w, id);
            w.WriteStartObject("result");
            w.WriteString("protocolVersion", "2024-11-05");
            w.WriteStartObject("capabilities");
            w.WriteStartObject("tools");
            w.WriteEndObject();
            w.WriteEndObject(); // capabilities
            w.WriteStartObject("serverInfo");
            w.WriteString("name", _config.Name);
            w.WriteString("version", _config.Version);
            w.WriteEndObject(); // serverInfo
            w.WriteEndObject(); // result
            w.WriteEndObject();
        }
        return ParseElement(stream);
    }

    private JsonElement BuildToolsListResponse(JsonElement? id)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("jsonrpc", "2.0");
            WriteId(w, id);
            w.WriteStartObject("result");
            w.WriteStartArray("tools");

            foreach (var tool in _config.Tools)
            {
                w.WriteStartObject();
                w.WriteString("name", tool.Name);
                w.WriteString("description", tool.Description);
                w.WritePropertyName("inputSchema");
                tool.InputSchema.WriteTo(w);

                if (tool.Annotations is { } annotations)
                {
                    WriteAnnotations(w, annotations);
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject(); // result
            w.WriteEndObject();
        }
        return ParseElement(stream);
    }

    private static void WriteAnnotations(Utf8JsonWriter w, McpToolAnnotations annotations)
    {
        w.WriteStartObject("annotations");
        if (annotations.ReadOnly is { } readOnly)
        {
            w.WriteBoolean("readOnlyHint", readOnly);
        }
        if (annotations.Destructive is { } destructive)
        {
            w.WriteBoolean("destructiveHint", destructive);
        }
        if (annotations.OpenWorld is { } openWorld)
        {
            w.WriteBoolean("openWorldHint", openWorld);
        }
        w.WriteEndObject();
    }

    private async Task<JsonElement> HandleToolCallAsync(
        JsonElement message, JsonElement? id, CancellationToken cancellationToken)
    {
        var @params = message.GetProperty("params");
        string toolName = @params.GetProperty("name").GetString()!;
        JsonElement arguments = @params.TryGetProperty("arguments", out var args)
            ? args : default;

        // Unknown tools and handler failures go back as isError results the model can read,
        // not as JSON-RPC errors. This matches the Python SDK.
        if (!_toolMap.TryGetValue(toolName, out var tool))
        {
            return BuildToolResult(id, McpToolResult.Error($"Tool '{toolName}' not found"));
        }

        McpToolResult result;
        try
        {
            result = await tool.Handler(arguments, cancellationToken);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            result = McpToolResult.Error(ex.Message);
        }

        return BuildToolResult(id, result);
    }

    private static JsonElement BuildToolResult(JsonElement? id, McpToolResult result)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("jsonrpc", "2.0");
            WriteId(w, id);
            w.WriteStartObject("result");
            w.WriteStartArray("content");
            result.WriteContentTo(w);
            w.WriteEndArray();
            if (result.IsError)
            {
                w.WriteBoolean("isError", true);
            }
            w.WriteEndObject(); // result
            w.WriteEndObject();
        }
        return ParseElement(stream);
    }

    private static JsonElement BuildEmptyResult(JsonElement? id)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("jsonrpc", "2.0");
            WriteId(w, id);
            w.WriteStartObject("result");
            w.WriteEndObject();
            w.WriteEndObject();
        }
        return ParseElement(stream);
    }

    private static JsonElement BuildMethodNotFoundError(JsonElement? id, string method)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("jsonrpc", "2.0");
            WriteId(w, id);
            w.WriteStartObject("error");
            w.WriteNumber("code", -32601);
            w.WriteString("message", $"Method '{method}' not found");
            w.WriteEndObject();
            w.WriteEndObject();
        }
        return ParseElement(stream);
    }

    private static JsonElement BuildInternalError(JsonElement? id, string message)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("jsonrpc", "2.0");
            WriteId(w, id);
            w.WriteStartObject("error");
            w.WriteNumber("code", -32603);
            w.WriteString("message", message);
            w.WriteEndObject();
            w.WriteEndObject();
        }
        return ParseElement(stream);
    }

    private static void WriteId(Utf8JsonWriter w, JsonElement? id)
    {
        if (id is { } value)
        {
            w.WritePropertyName("id");
            value.WriteTo(w);
        }
    }

    private static JsonElement ParseElement(MemoryStream stream)
    {
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        return doc.RootElement.Clone();
    }
}
