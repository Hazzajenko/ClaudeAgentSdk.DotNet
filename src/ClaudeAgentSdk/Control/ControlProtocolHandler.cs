using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using ClaudeAgentSdk.Hooks;
using ClaudeAgentSdk.Json;
using ClaudeAgentSdk.Permissions;
using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk.Control;

/// <summary>
/// Handles bidirectional control protocol between SDK and CLI.
/// Manages request ID generation, pending response tracking, and incoming request dispatch.
/// </summary>
internal sealed class ControlProtocolHandler
{
    private readonly ITransport _transport;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pendingResponses = new();
    private int _requestCounter;

    /// <summary>
    /// Callback for incoming hook_callback control requests. Set during initialize.
    /// Receives callbackId and typed HookInput; returns typed HookOutput.
    /// </summary>
    public Func<string, HookInput, CancellationToken, Task<HookOutput>>? OnHookCallback { get; set; }

    /// <summary>
    /// Callback for incoming can_use_tool control requests. Set when CanUseTool is configured.
    /// Receives extracted tool name, input, and context; returns typed PermissionResult.
    /// </summary>
    public CanUseToolCallback? OnCanUseTool { get; set; }

    /// <summary>
    /// Callback for incoming mcp_message control requests. Routes to SDK MCP servers.
    /// </summary>
    public Func<string, JsonElement, CancellationToken, Task<JsonElement>>? OnMcpMessage { get; set; }

    public ControlProtocolHandler(ITransport transport)
    {
        _transport = transport;
    }

    /// <summary>
    /// Send a control request and wait for the CLI's response.
    /// </summary>
    public async Task<JsonElement> SendControlRequestAsync(
        JsonElement request, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        string requestId = GenerateRequestId();
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingResponses[requestId] = tcs;

        try
        {
            string json = BuildControlRequestJson(requestId, request);
            await WriteAsync(json, cancellationToken);

            TimeSpan effectiveTimeout = timeout ?? TimeSpan.FromSeconds(60);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(effectiveTimeout);

            using var reg = cts.Token.Register(() => tcs.TrySetCanceled(cts.Token));
            return await tcs.Task;
        }
        finally
        {
            _pendingResponses.TryRemove(requestId, out _);
        }
    }

    /// <summary>
    /// Route an incoming control_response to the matching pending request.
    /// Called by the background message reader.
    /// </summary>
    public void HandleControlResponse(JsonElement root)
    {
        if (!root.TryGetProperty("response", out var response)) return;
        if (!response.TryGetProperty("request_id", out var ridProp)) return;
        string? requestId = ridProp.GetString();
        if (requestId is null) return;

        if (!_pendingResponses.TryGetValue(requestId, out var tcs)) return;

        if (response.TryGetProperty("subtype", out var subtype) && subtype.GetString() == "error")
        {
            string error = response.TryGetProperty("error", out var errProp) ? errProp.GetString() ?? "Unknown error" : "Unknown error";
            tcs.TrySetException(new InvalidOperationException(error));
        }
        else
        {
            JsonElement responseData = response.TryGetProperty("response", out var resp) ? resp.Clone() : default;
            tcs.TrySetResult(responseData);
        }
    }

    /// <summary>
    /// Handle an incoming control_request from CLI (hooks, permissions).
    /// Called by the background message reader.
    /// </summary>
    public async Task HandleControlRequestAsync(JsonElement root, CancellationToken cancellationToken)
    {
        string? requestId = root.TryGetProperty("request_id", out var rid) ? rid.GetString() : null;
        if (requestId is null) return;

        if (!root.TryGetProperty("request", out var request)) return;
        string? subtype = request.TryGetProperty("subtype", out var st) ? st.GetString() : null;

        try
        {
            JsonElement responseData;

            if (subtype == "hook_callback" && OnHookCallback is not null)
            {
                string callbackId = request.TryGetProperty("callback_id", out var cb) ? cb.GetString() ?? "" : "";
                JsonElement inputElement = request.TryGetProperty("input", out var inp) ? inp.Clone() : default;

                var typedInput = inputElement.Deserialize(ClaudeJsonContext.Default.HookInput)
                    ?? throw new JsonException("Failed to deserialize hook input");

                HookOutput output = await OnHookCallback(callbackId, typedInput, cancellationToken);
                responseData = output.ToJsonElement();
            }
            else if (subtype == "can_use_tool" && OnCanUseTool is not null)
            {
                string toolName = request.TryGetProperty("tool_name", out var tn) ? tn.GetString() ?? "" : "";
                JsonElement input = request.TryGetProperty("input", out var inp) ? inp.Clone() : default;

                var suggestions = new List<PermissionUpdate>();
                if (request.TryGetProperty("permission_suggestions", out var ps) && ps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in ps.EnumerateArray())
                        suggestions.Add(ParsePermissionUpdate(s));
                }

                var context = new ToolPermissionContext { Suggestions = suggestions };
                PermissionResult result = await OnCanUseTool(toolName, input, context, cancellationToken);
                responseData = SerializePermissionResult(result, input);
            }
            else if (subtype == "mcp_message" && OnMcpMessage is not null)
            {
                string serverName = request.GetProperty("server_name").GetString()!;
                JsonElement message = request.GetProperty("message").Clone();
                var mcpResponse = await OnMcpMessage(serverName, message, cancellationToken);
                responseData = BuildMcpResponseWrapper(mcpResponse);
            }
            else
            {
                responseData = default;
            }

            await WriteControlResponseAsync(requestId, responseData, cancellationToken);
        }
        catch (Exception ex)
        {
            await WriteControlErrorAsync(requestId, ex.Message, cancellationToken);
        }
    }

    /// <summary>
    /// Write JSON to the transport, guarded by write lock.
    /// </summary>
    public async Task WriteAsync(string json, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            await _transport.WriteAsync(json, cancellationToken);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Signal all pending requests as failed (called on transport shutdown).
    /// </summary>
    public void FailAllPending(Exception ex)
    {
        foreach (var kvp in _pendingResponses)
        {
            kvp.Value.TrySetException(ex);
        }
        _pendingResponses.Clear();
    }

    private string GenerateRequestId()
    {
        int counter = Interlocked.Increment(ref _requestCounter);
        return $"req_{counter}_{Guid.NewGuid():N}"[..24];
    }

    private static string BuildControlRequestJson(string requestId, JsonElement request)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("type", "control_request");
            w.WriteString("request_id", requestId);
            w.WritePropertyName("request");
            request.WriteTo(w);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private async Task WriteControlResponseAsync(string requestId, JsonElement responseData, CancellationToken ct)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("type", "control_response");
            w.WriteStartObject("response");
            w.WriteString("subtype", "success");
            w.WriteString("request_id", requestId);
            w.WritePropertyName("response");
            if (responseData.ValueKind == JsonValueKind.Undefined)
            {
                w.WriteStartObject();
                w.WriteEndObject();
            }
            else
            {
                responseData.WriteTo(w);
            }
            w.WriteEndObject(); // response wrapper
            w.WriteEndObject(); // root
        }
        await WriteAsync(Encoding.UTF8.GetString(stream.ToArray()), ct);
    }

    private async Task WriteControlErrorAsync(string requestId, string error, CancellationToken ct)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("type", "control_response");
            w.WriteStartObject("response");
            w.WriteString("subtype", "error");
            w.WriteString("request_id", requestId);
            w.WriteString("error", error);
            w.WriteEndObject();
            w.WriteEndObject();
        }
        await WriteAsync(Encoding.UTF8.GetString(stream.ToArray()), ct);
    }

    private static JsonElement SerializePermissionResult(PermissionResult result, JsonElement originalInput)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            switch (result)
            {
                case PermissionResultAllow allow:
                    w.WriteString("behavior", "allow");
                    w.WritePropertyName("updatedInput");
                    if (allow.UpdatedInput is { } updatedInput)
                        updatedInput.WriteTo(w);
                    else
                        originalInput.WriteTo(w);
                    if (allow.UpdatedPermissions is { Count: > 0 } perms)
                    {
                        WritePermissionUpdates(w, perms);
                    }
                    break;

                case PermissionResultDeny deny:
                    w.WriteString("behavior", "deny");
                    w.WriteString("message", deny.Message);
                    if (deny.Interrupt)
                        w.WriteBoolean("interrupt", true);
                    break;
            }
            w.WriteEndObject();
        }
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        return doc.RootElement.Clone();
    }

    private static JsonElement BuildMcpResponseWrapper(JsonElement mcpResponse)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WritePropertyName("mcp_response");
            mcpResponse.WriteTo(w);
            w.WriteEndObject();
        }
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        return doc.RootElement.Clone();
    }

    private static PermissionUpdate ParsePermissionUpdate(JsonElement element)
    {
        string typeStr = element.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
        var type = typeStr switch
        {
            "addRules" => PermissionUpdateType.AddRules,
            "replaceRules" => PermissionUpdateType.ReplaceRules,
            "removeRules" => PermissionUpdateType.RemoveRules,
            "setMode" => PermissionUpdateType.SetMode,
            "addDirectories" => PermissionUpdateType.AddDirectories,
            "removeDirectories" => PermissionUpdateType.RemoveDirectories,
            _ => throw new JsonException($"Unknown permission update type: '{typeStr}'")
        };

        List<PermissionRuleValue>? rules = null;
        if (element.TryGetProperty("rules", out var rulesEl) && rulesEl.ValueKind == JsonValueKind.Array)
        {
            rules = [];
            foreach (var r in rulesEl.EnumerateArray())
            {
                rules.Add(new PermissionRuleValue
                {
                    ToolName = r.TryGetProperty("tool_name", out var tn) ? tn.GetString() ?? "" : "",
                    RuleContent = r.TryGetProperty("rule_content", out var rc) ? rc.GetString() ?? "" : "",
                    Behavior = r.TryGetProperty("behavior", out var b) ? b.GetString() ?? "" : ""
                });
            }
        }

        string? mode = element.TryGetProperty("mode", out var m) ? m.GetString() : null;
        string? destination = element.TryGetProperty("destination", out var d) ? d.GetString() : null;

        List<string>? directories = null;
        if (element.TryGetProperty("directories", out var dirsEl) && dirsEl.ValueKind == JsonValueKind.Array)
        {
            directories = [];
            foreach (var dir in dirsEl.EnumerateArray())
                directories.Add(dir.GetString() ?? "");
        }

        return new PermissionUpdate
        {
            Type = type,
            Rules = rules,
            Mode = mode,
            Directories = directories,
            Destination = destination
        };
    }

    private static void WritePermissionUpdates(Utf8JsonWriter w, IReadOnlyList<PermissionUpdate> updates)
    {
        w.WriteStartArray("updatedPermissions");
        foreach (var update in updates)
        {
            w.WriteStartObject();
            w.WriteString("type", update.Type switch
            {
                PermissionUpdateType.AddRules => "addRules",
                PermissionUpdateType.ReplaceRules => "replaceRules",
                PermissionUpdateType.RemoveRules => "removeRules",
                PermissionUpdateType.SetMode => "setMode",
                PermissionUpdateType.AddDirectories => "addDirectories",
                PermissionUpdateType.RemoveDirectories => "removeDirectories",
                _ => throw new ArgumentOutOfRangeException()
            });

            if (update.Rules is { Count: > 0 })
            {
                w.WriteStartArray("rules");
                foreach (var rule in update.Rules)
                {
                    w.WriteStartObject();
                    w.WriteString("tool_name", rule.ToolName);
                    w.WriteString("rule_content", rule.RuleContent);
                    w.WriteString("behavior", rule.Behavior);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
            }

            if (update.Mode is not null)
                w.WriteString("mode", update.Mode);

            if (update.Directories is { Count: > 0 })
            {
                w.WriteStartArray("directories");
                foreach (string dir in update.Directories)
                    w.WriteStringValue(dir);
                w.WriteEndArray();
            }

            if (update.Destination is not null)
                w.WriteString("destination", update.Destination);

            w.WriteEndObject();
        }
        w.WriteEndArray();
    }
}
