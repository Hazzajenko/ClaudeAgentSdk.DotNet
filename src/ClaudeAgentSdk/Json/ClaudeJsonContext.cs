using System.Text.Json.Serialization;
using ClaudeAgentSdk.Hooks;
using ClaudeAgentSdk.Json.Wire;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Json;

[JsonSerializable(typeof(Usage))]
[JsonSerializable(typeof(HookInput))]
[JsonSerializable(typeof(ContentBlockWire))]
[JsonSerializable(typeof(List<ContentBlockWire>))]
[JsonSerializable(typeof(AssistantMessageWire))]
[JsonSerializable(typeof(UserMessageWire))]
[JsonSerializable(typeof(SystemMessageWire))]
[JsonSerializable(typeof(ResultMessageWire))]
[JsonSerializable(typeof(StreamEventWire))]
[JsonSerializable(typeof(RateLimitEventWire))]
[JsonSerializable(typeof(RateLimitInfoWire))]
[JsonSerializable(typeof(TaskStartedDataWire))]
[JsonSerializable(typeof(TaskProgressDataWire))]
[JsonSerializable(typeof(TaskNotificationDataWire))]
[JsonSerializable(typeof(TaskUsage))]
[JsonSerializable(typeof(ModelUsage))]
[JsonSerializable(typeof(DeferredToolUse))]
[JsonSerializable(typeof(MessageOrigin))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    AllowOutOfOrderMetadataProperties = true)]
internal partial class ClaudeJsonContext : JsonSerializerContext;
