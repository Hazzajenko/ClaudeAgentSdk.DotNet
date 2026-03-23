using System.Text;
using System.Text.Json;

namespace ClaudeAgentSdk.Mcp.Sdk;

/// <summary>
/// AOT-safe JSON Schema builder for MCP tool input schemas.
/// </summary>
public static class McpJsonSchema
{
    public static JsonElement String(string? description = null) =>
        BuildPrimitive("string", description);

    public static JsonElement Number(string? description = null) =>
        BuildPrimitive("number", description);

    public static JsonElement Integer(string? description = null) =>
        BuildPrimitive("integer", description);

    public static JsonElement Boolean(string? description = null) =>
        BuildPrimitive("boolean", description);

    public static McpJsonProperty Property(string name, JsonElement schema) => new(name, schema);

    public static JsonElement Object(params McpJsonProperty[] properties)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("type", "object");

            w.WriteStartObject("properties");
            foreach (var prop in properties)
            {
                w.WritePropertyName(prop.Name);
                prop.Schema.WriteTo(w);
            }
            w.WriteEndObject();

            w.WriteStartArray("required");
            foreach (var prop in properties)
                w.WriteStringValue(prop.Name);
            w.WriteEndArray();

            w.WriteEndObject();
        }

        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        return doc.RootElement.Clone();
    }

    private static JsonElement BuildPrimitive(string type, string? description)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("type", type);
            if (description is not null)
                w.WriteString("description", description);
            w.WriteEndObject();
        }

        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
        return doc.RootElement.Clone();
    }
}

/// <summary>
/// Named property for JSON Schema object construction.
/// </summary>
public readonly record struct McpJsonProperty(string Name, JsonElement Schema);
