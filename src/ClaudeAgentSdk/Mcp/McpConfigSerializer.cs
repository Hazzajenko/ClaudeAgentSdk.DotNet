using System.Text;
using System.Text.Json;

namespace ClaudeAgentSdk.Mcp;

internal static class McpConfigSerializer
{
    public static string Serialize(IReadOnlyDictionary<string, McpServerConfig> servers)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteStartObject("mcpServers");

            foreach (var (name, config) in servers)
            {
                w.WriteStartObject(name);
                WriteServerConfig(w, config);
                w.WriteEndObject();
            }

            w.WriteEndObject(); // mcpServers
            w.WriteEndObject(); // root
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteServerConfig(Utf8JsonWriter w, McpServerConfig config)
    {
        switch (config)
        {
            case McpStdioServerConfig stdio:
                w.WriteString("type", "stdio");
                w.WriteString("command", stdio.Command);
                if (stdio.Args is { Count: > 0 })
                {
                    w.WriteStartArray("args");
                    foreach (string arg in stdio.Args)
                        w.WriteStringValue(arg);
                    w.WriteEndArray();
                }
                if (stdio.Env is { Count: > 0 })
                {
                    w.WriteStartObject("env");
                    foreach (var (key, value) in stdio.Env)
                        w.WriteString(key, value);
                    w.WriteEndObject();
                }
                break;

            case McpSseServerConfig sse:
                w.WriteString("type", "sse");
                w.WriteString("url", sse.Url);
                WriteHeaders(w, sse.Headers);
                break;

            case McpHttpServerConfig http:
                w.WriteString("type", "http");
                w.WriteString("url", http.Url);
                WriteHeaders(w, http.Headers);
                break;
        }
    }

    private static void WriteHeaders(Utf8JsonWriter w, IReadOnlyDictionary<string, string>? headers)
    {
        if (headers is not { Count: > 0 }) return;

        w.WriteStartObject("headers");
        foreach (var (key, value) in headers)
            w.WriteString(key, value);
        w.WriteEndObject();
    }
}
