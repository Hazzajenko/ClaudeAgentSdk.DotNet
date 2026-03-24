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

        // Normalize nullable type arrays: ["object", "null"] → "object"
        // Reference types produce nullable schemas, but Claude CLI expects plain types.
        NormalizeNullableTypes(schemaNode);

        string json = schemaNode.ToJsonString();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static void NormalizeNullableTypes(JsonNode? node)
    {
        if (node is not JsonObject obj) return;

        if (obj["type"] is JsonArray typeArray)
        {
            var nonNull = new List<JsonNode?>();
            foreach (var item in typeArray)
            {
                if (item?.GetValue<string>() != "null")
                    nonNull.Add(item);
            }

            if (nonNull.Count == 1)
            {
                obj["type"] = JsonValue.Create(nonNull[0]!.GetValue<string>());
            }
        }

        // Recurse into properties
        if (obj["properties"] is JsonObject props)
        {
            foreach (var (_, value) in props)
                NormalizeNullableTypes(value);
        }
    }
}
