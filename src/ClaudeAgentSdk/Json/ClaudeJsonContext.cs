using System.Text.Json.Serialization;
using ClaudeAgentSdk.Json.Wire;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Json;

[JsonSerializable(typeof(ContentBlockWire))]
[JsonSerializable(typeof(List<ContentBlockWire>))]
[JsonSerializable(typeof(TextBlockWire))]
[JsonSerializable(typeof(ThinkingBlockWire))]
[JsonSerializable(typeof(ToolUseBlockWire))]
[JsonSerializable(typeof(ToolResultBlockWire))]
[JsonSerializable(typeof(AssistantMessageWire))]
[JsonSerializable(typeof(UserMessageWire))]
[JsonSerializable(typeof(SystemMessageWire))]
[JsonSerializable(typeof(ResultMessageWire))]
[JsonSerializable(typeof(StreamEventWire))]
[JsonSerializable(typeof(RateLimitEventWire))]
[JsonSerializable(typeof(TaskStartedDataWire))]
[JsonSerializable(typeof(TaskProgressDataWire))]
[JsonSerializable(typeof(TaskNotificationDataWire))]
[JsonSerializable(typeof(TaskUsage))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class ClaudeJsonContext : JsonSerializerContext;
