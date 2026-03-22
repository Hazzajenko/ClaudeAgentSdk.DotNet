using System.Text.Json;

namespace ClaudeAgentSdk.Mcp;

internal static class McpStatusParser
{
    public static McpStatusResponse Parse(JsonElement root)
    {
        var servers = new List<McpServerStatus>();

        if (root.TryGetProperty("mcpServers", out var serversArray) && serversArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var server in serversArray.EnumerateArray())
            {
                servers.Add(ParseServerStatus(server));
            }
        }

        return new McpStatusResponse(servers);
    }

    private static McpServerStatus ParseServerStatus(JsonElement el)
    {
        McpServerInfo? serverInfo = null;
        if (el.TryGetProperty("serverInfo", out var si) && si.ValueKind == JsonValueKind.Object)
        {
            serverInfo = new McpServerInfo(
                si.GetProperty("name").GetString()!,
                si.GetProperty("version").GetString()!);
        }

        IReadOnlyList<McpToolInfo>? tools = null;
        if (el.TryGetProperty("tools", out var toolsArr) && toolsArr.ValueKind == JsonValueKind.Array)
        {
            var toolList = new List<McpToolInfo>();
            foreach (var tool in toolsArr.EnumerateArray())
            {
                McpToolAnnotations? annotations = null;
                if (tool.TryGetProperty("annotations", out var ann) && ann.ValueKind == JsonValueKind.Object)
                {
                    annotations = new McpToolAnnotations
                    {
                        ReadOnly = ann.TryGetProperty("readOnly", out var ro) && ro.ValueKind == JsonValueKind.True ? true : null,
                        Destructive = ann.TryGetProperty("destructive", out var d) && d.ValueKind == JsonValueKind.True ? true : null,
                        OpenWorld = ann.TryGetProperty("openWorld", out var ow) && ow.ValueKind == JsonValueKind.True ? true : null
                    };
                }

                toolList.Add(new McpToolInfo(
                    tool.GetProperty("name").GetString()!,
                    tool.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                    annotations));
            }
            tools = toolList;
        }

        return new McpServerStatus
        {
            Name = el.GetProperty("name").GetString()!,
            Status = el.GetProperty("status").GetString()!,
            ServerInfo = serverInfo,
            Error = el.TryGetProperty("error", out var err) ? err.GetString() : null,
            Scope = el.TryGetProperty("scope", out var scope) ? scope.GetString() : null,
            Tools = tools
        };
    }
}
