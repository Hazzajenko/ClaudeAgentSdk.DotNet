using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeAgentSdk.Messages;

/// <summary>
/// Token usage and cost for one model in a run. The CLI sends it in the camelCase
/// <c>modelUsage</c> map of a result frame, keyed by the model string.
/// Keys that this type does not name are kept in <see cref="ExtensionData"/>.
/// </summary>
public sealed record ModelUsage
{
    [JsonPropertyName("inputTokens")]
    public int InputTokens { get; init; }

    [JsonPropertyName("outputTokens")]
    public int OutputTokens { get; init; }

    [JsonPropertyName("cacheReadInputTokens")]
    public int CacheReadInputTokens { get; init; }

    [JsonPropertyName("cacheCreationInputTokens")]
    public int CacheCreationInputTokens { get; init; }

    [JsonPropertyName("webSearchRequests")]
    public int WebSearchRequests { get; init; }

    [JsonPropertyName("costUSD")]
    public double CostUsd { get; init; }

    [JsonPropertyName("contextWindow")]
    public int ContextWindow { get; init; }

    [JsonPropertyName("maxOutputTokens")]
    public int MaxOutputTokens { get; init; }

    /// <summary>Model id used for the price lookup. It can differ from the key of this entry.</summary>
    [JsonPropertyName("canonicalModel")]
    public string? CanonicalModel { get; init; }

    /// <summary>API provider that served the model, for example "firstParty" or "bedrock".</summary>
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

/// <summary>
/// A tool call that a PreToolUse hook deferred. The run stops and the result frame
/// carries the call, so the caller can look at it and decide to resume or not.
/// </summary>
public sealed record DeferredToolUse
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required JsonElement Input { get; init; }
}

/// <summary>
/// Where a user-role message came from. On a <see cref="ResultMessage"/> it is the origin
/// of the user message that started the turn. Only <see cref="Kind"/> is always present.
/// The other keys depend on the kind. Keys that this type does not name are kept in
/// <see cref="ExtensionData"/>.
/// </summary>
public sealed record MessageOrigin
{
    /// <summary>
    /// For example "human", "channel", "peer", "task-notification", "coordinator",
    /// "unclassified", "observer", "auto-continuation" or "observer-activity".
    /// A newer CLI can send other kinds.
    /// </summary>
    [JsonPropertyName("kind")]
    public required string Kind { get; init; }

    /// <summary>Name of the MCP server that the message came in on. Set for kind "channel".</summary>
    [JsonPropertyName("server")]
    public string? Server { get; init; }

    /// <summary>Address of the sender. Set for kind "peer" and "observer". The sender sets it, so do not use it as proof of identity.</summary>
    [JsonPropertyName("from")]
    public string? From { get; init; }

    /// <summary>Display name of the sender. Set for kind "peer".</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>Session id of the sender, if its host gave one. Set for kind "peer".</summary>
    [JsonPropertyName("fromSession")]
    public string? FromSession { get; init; }

    /// <summary>Task id of the background subagent that sent the message. Set for kind "peer" and "observer".</summary>
    [JsonPropertyName("senderTaskId")]
    public string? SenderTaskId { get; init; }

    /// <summary>Message body without the peer envelope. Set for kind "peer".</summary>
    [JsonPropertyName("body")]
    public string? Body { get; init; }

    /// <summary>Process id of the process that connected to the local messaging socket. Set for kind "peer".</summary>
    [JsonPropertyName("verifiedPeerPid")]
    public int? VerifiedPeerPid { get; init; }

    /// <summary>"scheduled-trigger" or "peer-send-message". Set for kind "task-notification" when a scheduled task or another session sent the message.</summary>
    [JsonPropertyName("subkind")]
    public string? Subkind { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
