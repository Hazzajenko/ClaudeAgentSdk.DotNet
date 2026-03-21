using System.Text.Json;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Json;

/// <summary>
/// AOT-safe JSON deserialization for the Claude CLI protocol.
/// Uses direct JsonDocument parsing with custom converters — no reflection.
/// </summary>
public static class ClaudeJsonOptions
{
    /// <summary>
    /// Deserialize a JSON line from CLI stdout into a Message.
    /// </summary>
    public static Message? DeserializeMessage(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return MessageConverter.Parse(doc.RootElement);
    }
}
