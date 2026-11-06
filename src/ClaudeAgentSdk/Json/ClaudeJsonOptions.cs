using System.Text.Json;
using ClaudeAgentSdk.Json.Wire;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Json;

/// <summary>
/// AOT-safe JSON deserialization for the Claude CLI protocol.
/// Uses STJ source generation via <see cref="ClaudeJsonContext"/> — no reflection.
/// </summary>
public static class ClaudeJsonOptions
{
    /// <summary>
    /// Deserialize a JSON line from CLI stdout into a Message.
    /// Returns null for a message type that this SDK does not know.
    /// </summary>
    public static Message? DeserializeMessage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("type", out var typeProp))
            throw new JsonException("Message missing 'type' field");

        return typeProp.GetString() switch
        {
            "assistant" => WireMapper.MapAssistantMessage(
                root.Deserialize(ClaudeJsonContext.Default.AssistantMessageWire)!),
            "user" => WireMapper.MapUserMessage(
                root.Deserialize(ClaudeJsonContext.Default.UserMessageWire)!),
            "system" => WireMapper.MapSystemMessage(
                root.Deserialize(ClaudeJsonContext.Default.SystemMessageWire)!,
                root.Clone()),
            "result" => WireMapper.MapResultMessage(
                root.Deserialize(ClaudeJsonContext.Default.ResultMessageWire)!),
            "stream_event" => WireMapper.MapStreamEvent(
                root.Deserialize(ClaudeJsonContext.Default.StreamEventWire)!),
            "rate_limit_event" => WireMapper.MapRateLimitEvent(
                root.Deserialize(ClaudeJsonContext.Default.RateLimitEventWire)!),
            "control_response" => new ControlResponseMessage(root.Clone()),
            "control_request" => new ControlRequestMessage(root.Clone()),
            "conversation_reset" => new ConversationResetMessage
            {
                NewConversationId = GetRequiredString(root, "new_conversation_id", "conversation_reset"),
                Uuid = GetRequiredString(root, "uuid", "conversation_reset"),
                SessionId = GetRequiredString(root, "session_id", "conversation_reset"),
            },
            // A newer CLI can send types this SDK does not know. Skip them so the session continues.
            _ => null
        };
    }

    private static string GetRequiredString(JsonElement root, string name, string messageType)
    {
        if (root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString()!;
        }

        throw new JsonException($"Missing required field '{name}' in {messageType} message");
    }
}
