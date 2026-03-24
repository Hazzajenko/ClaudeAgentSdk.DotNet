using System.Text.Json.Serialization;
using ClaudeAgentSdk.Hooks;

namespace ClaudeAgentSdk.Json;

/// <summary>
/// Source-generated JSON context for hook output serialization (camelCase wire format).
/// Separate from ClaudeJsonContext which uses snake_case for CLI input parsing.
/// </summary>
[JsonSerializable(typeof(HookOutput))]
[JsonSerializable(typeof(HookSpecificOutput))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class HookJsonContext : JsonSerializerContext;
