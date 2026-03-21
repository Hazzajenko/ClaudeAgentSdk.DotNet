using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Json;

internal sealed class ContentBlockConverter : JsonConverter<ContentBlock>
{
    public override ContentBlock? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return Parse(doc.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, ContentBlock value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Serialization of ContentBlock is not supported");
    }

    /// <summary>
    /// Parse a JsonElement into a ContentBlock. Used directly by MessageConverter
    /// to avoid AOT-unsafe JsonSerializer.Deserialize calls.
    /// </summary>
    internal static ContentBlock Parse(JsonElement element)
    {
        if (!element.TryGetProperty("type", out var typeProp))
        {
            throw new JsonException("Content block missing 'type' field");
        }

        return typeProp.GetString() switch
        {
            "text" => new TextBlock
            {
                Text = element.GetProperty("text").GetString()!
            },
            "thinking" => new ThinkingBlock
            {
                Thinking = element.GetProperty("thinking").GetString()!,
                Signature = element.GetProperty("signature").GetString()!
            },
            "tool_use" => new ToolUseBlock
            {
                Id = element.GetProperty("id").GetString()!,
                Name = element.GetProperty("name").GetString()!,
                Input = element.GetProperty("input").Clone()
            },
            "tool_result" => new ToolResultBlock
            {
                ToolUseId = element.GetProperty("tool_use_id").GetString()!,
                Content = element.TryGetProperty("content", out var content) ? content.Clone() : null,
                IsError = element.TryGetProperty("is_error", out var isErr) ? isErr.GetBoolean() : null
            },
            var unknown => throw new JsonException($"Unknown content block type: '{unknown}'")
        };
    }
}
