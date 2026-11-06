using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace ClaudeAgentSdk.Mcp.Sdk;

/// <summary>
/// Routes MCP JSONRPC messages to in-process SDK server tools.
/// </summary>
internal sealed class SdkMcpRequestHandler
{
    private const int RequestCancelledCode = -32800;

    private readonly McpSdkServerConfig _config;
    private readonly Dictionary<string, SdkMcpTool> _toolMap;

    // Running tools/call requests, keyed by the raw JSON text of the request id,
    // so that notifications/cancelled can cancel the handler token.
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningCalls = new();

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
                "ping" => BuildEmptyResult(id),
                "notifications/initialized" => BuildEmptyResult(id),
                "notifications/cancelled" => HandleCancelled(message),
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
        if (annotations.Title is { } title)
        {
            w.WriteString("title", title);
        }
        if (annotations.ReadOnly is { } readOnly)
        {
            w.WriteBoolean("readOnlyHint", readOnly);
        }
        if (annotations.Destructive is { } destructive)
        {
            w.WriteBoolean("destructiveHint", destructive);
        }
        if (annotations.Idempotent is { } idempotent)
        {
            w.WriteBoolean("idempotentHint", idempotent);
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

        using var callCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        string? callKey = id?.GetRawText();
        if (callKey is not null)
        {
            _runningCalls[callKey] = callCts;
        }

        McpToolResult result;
        try
        {
            result = await tool.Handler(arguments, callCts.Token);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && callCts.IsCancellationRequested))
        {
            result = McpToolResult.Error(ex.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Only notifications/cancelled can cancel callCts while the outer token is live.
            return BuildError(id, RequestCancelledCode, "Request cancelled");
        }
        finally
        {
            if (callKey is not null)
            {
                _runningCalls.TryRemove(new KeyValuePair<string, CancellationTokenSource>(callKey, callCts));
            }
        }

        // A cancelled call must not produce a result, even when the handler ignored its token.
        if (callCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return BuildError(id, RequestCancelledCode, "Request cancelled");
        }

        return BuildToolResult(id, result);
    }

    private JsonElement HandleCancelled(JsonElement message)
    {
        if (message.TryGetProperty("params", out var p)
            && p.ValueKind == JsonValueKind.Object
            && p.TryGetProperty("requestId", out var requestId)
            && _runningCalls.TryGetValue(requestId.GetRawText(), out var callCts))
        {
            try
            {
                callCts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The call ended between the lookup and the cancel.
            }
        }

        // A notification gets no JSON-RPC reply, but the control request that carried it needs an ack.
        return BuildEmptyResult(null);
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

    private static JsonElement BuildMethodNotFoundError(JsonElement? id, string method) =>
        BuildError(id, -32601, $"Method '{method}' not found");

    private static JsonElement BuildInternalError(JsonElement? id, string message) =>
        BuildError(id, -32603, message);

    private static JsonElement BuildError(JsonElement? id, int code, string message)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("jsonrpc", "2.0");
            WriteId(w, id);
            w.WriteStartObject("error");
            w.WriteNumber("code", code);
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
