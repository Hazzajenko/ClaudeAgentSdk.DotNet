using System.Text.Json;

namespace ClaudeAgentSdk.Control;

/// <summary>
/// Builds the request body of each outbound control request.
/// Every value goes through <see cref="Utf8JsonWriter"/>, so caller strings are escaped.
/// </summary>
internal static class ControlRequests
{
    public static JsonElement SubtypeOnly(string subtype) => Build(subtype, static _ => { });

    public static JsonElement SetPermissionMode(string mode) =>
        Build("set_permission_mode", w => w.WriteString("mode", mode));

    public static JsonElement SetModel(string? model) =>
        Build("set_model", w =>
        {
            if (model is null)
            {
                w.WriteNull("model");
            }
            else
            {
                w.WriteString("model", model);
            }
        });

    public static JsonElement RewindFiles(string userMessageId) =>
        Build("rewind_files", w => w.WriteString("user_message_id", userMessageId));

    public static JsonElement McpReconnect(string serverName) =>
        Build("mcp_reconnect", w => w.WriteString("serverName", serverName));

    public static JsonElement McpToggle(string serverName, bool enabled) =>
        Build("mcp_toggle", w =>
        {
            w.WriteString("serverName", serverName);
            w.WriteBoolean("enabled", enabled);
        });

    public static JsonElement StopTask(string taskId) =>
        Build("stop_task", w => w.WriteString("task_id", taskId));

    private static JsonElement Build(string subtype, Action<Utf8JsonWriter> writeFields)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("subtype", subtype);
            writeFields(w);
            w.WriteEndObject();
        }
        using var doc = JsonDocument.Parse(stream.ToArray());
        return doc.RootElement.Clone();
    }
}
