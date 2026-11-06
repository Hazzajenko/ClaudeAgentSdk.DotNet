using System.Text;
using System.Text.Json;
using ClaudeAgentSdk.Mcp;

namespace ClaudeAgentSdk.Agents;

internal static class AgentSerializer
{
    public static string Serialize(IReadOnlyDictionary<string, AgentDefinition> agents)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            foreach (var (name, agent) in agents)
            {
                w.WriteStartObject(name);
                WriteAgent(w, agent);
                w.WriteEndObject();
            }
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static void WriteAgentsProperty(Utf8JsonWriter w, IReadOnlyDictionary<string, AgentDefinition> agents)
    {
        w.WriteStartObject("agents");
        foreach (var (name, agent) in agents)
        {
            w.WriteStartObject(name);
            WriteAgent(w, agent);
            w.WriteEndObject();
        }
        w.WriteEndObject();
    }

    private static void WriteAgent(Utf8JsonWriter w, AgentDefinition agent)
    {
        w.WriteString("description", agent.Description);
        w.WriteString("prompt", agent.Prompt);

        if (agent.Tools is { Count: > 0 })
        {
            w.WriteStartArray("tools");
            foreach (string tool in agent.Tools) w.WriteStringValue(tool);
            w.WriteEndArray();
        }

        if (agent.DisallowedTools is { Count: > 0 })
        {
            w.WriteStartArray("disallowedTools");
            foreach (string tool in agent.DisallowedTools) w.WriteStringValue(tool);
            w.WriteEndArray();
        }

        if (agent.Model is not null)
            w.WriteString("model", agent.Model);

        if (agent.Skills is { Count: > 0 })
        {
            w.WriteStartArray("skills");
            foreach (string skill in agent.Skills) w.WriteStringValue(skill);
            w.WriteEndArray();
        }

        if (agent.Memory is not null)
            w.WriteString("memory", agent.Memory);

        if (agent.McpServers is { Count: > 0 } || agent.InlineMcpServers is { Count: > 0 })
        {
            w.WriteStartArray("mcpServers");
            if (agent.McpServers is not null)
            {
                foreach (string server in agent.McpServers) w.WriteStringValue(server);
            }
            if (agent.InlineMcpServers is not null)
            {
                foreach (var (name, config) in agent.InlineMcpServers)
                {
                    if (config is McpSdkServerConfig)
                    {
                        throw new ArgumentException(
                            $"Agent MCP server '{name}' is an SDK server. An SDK server cannot be inline. Add it to ClaudeClientOptions.McpServers and name it in AgentDefinition.McpServers.",
                            nameof(agent));
                    }

                    w.WriteStartObject();
                    w.WriteStartObject(name);
                    McpConfigSerializer.WriteServerConfig(w, config);
                    w.WriteEndObject();
                    w.WriteEndObject();
                }
            }
            w.WriteEndArray();
        }

        if (agent.InitialPrompt is not null)
            w.WriteString("initialPrompt", agent.InitialPrompt);

        if (agent.MaxTurns is { } maxTurns)
            w.WriteNumber("maxTurns", maxTurns);

        if (agent.Background is { } background)
            w.WriteBoolean("background", background);

        if (agent.Effort is { } effort)
        {
            w.WriteString("effort", effort switch
            {
                EffortLevel.Low => "low",
                EffortLevel.Medium => "medium",
                EffortLevel.High => "high",
                EffortLevel.Max => "max",
                _ => throw new ArgumentOutOfRangeException(nameof(agent), effort, "Unknown effort level.")
            });
        }

        if (agent.PermissionMode is { } mode)
        {
            w.WriteString("permissionMode", mode switch
            {
                PermissionMode.Default => "default",
                PermissionMode.AcceptEdits => "acceptEdits",
                PermissionMode.Plan => "plan",
                PermissionMode.BypassPermissions => "bypassPermissions",
                _ => throw new ArgumentOutOfRangeException(nameof(agent), mode, "Unknown permission mode.")
            });
        }
    }
}
