using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeAgentSdk.Hooks;
using ClaudeAgentSdk.Json.Wire;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Json;

[JsonSerializable(typeof(Usage))]
[JsonSerializable(typeof(HookInput))]
[JsonSerializable(typeof(TextBlockWire))]
[JsonSerializable(typeof(ThinkingBlockWire))]
[JsonSerializable(typeof(ToolUseBlockWire))]
[JsonSerializable(typeof(ToolResultBlockWire))]
[JsonSerializable(typeof(List<JsonElement>))]
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
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    AllowOutOfOrderMetadataProperties = true)]
internal partial class ClaudeJsonContext : JsonSerializerContext;
