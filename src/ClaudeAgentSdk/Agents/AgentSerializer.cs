using System.Text;
using System.Text.Json;

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

        if (agent.Model is { } model)
        {
            w.WriteString("model", model switch
            {
                AgentModel.Sonnet => "sonnet",
                AgentModel.Opus => "opus",
                AgentModel.Haiku => "haiku",
                AgentModel.Inherit => "inherit",
                _ => "inherit"
            });
        }

        if (agent.Skills is { Count: > 0 })
        {
            w.WriteStartArray("skills");
            foreach (string skill in agent.Skills) w.WriteStringValue(skill);
            w.WriteEndArray();
        }

        if (agent.Memory is not null)
            w.WriteString("memory", agent.Memory);

        if (agent.McpServers is { Count: > 0 })
        {
            w.WriteStartArray("mcpServers");
            foreach (string server in agent.McpServers) w.WriteStringValue(server);
            w.WriteEndArray();
        }
    }
}
