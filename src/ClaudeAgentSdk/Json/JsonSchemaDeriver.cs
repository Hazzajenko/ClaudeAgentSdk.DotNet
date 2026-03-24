using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace ClaudeAgentSdk.Json;

/// <summary>
/// Derives a JSON Schema <see cref="JsonElement"/> from a source-generated <see cref="JsonTypeInfo"/>.
/// Uses .NET 9's <see cref="JsonSchemaExporter"/> — AOT-safe, no reflection.
/// </summary>
internal static class JsonSchemaDeriver
{
    public static JsonElement FromTypeInfo(JsonTypeInfo typeInfo)
    {
        JsonNode schemaNode = typeInfo.Options.GetJsonSchemaAsNode(typeInfo.Type);
        string json = schemaNode.ToJsonString();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
