using System.Text.Json.Serialization;

namespace ClaudeAgentSdk.Json.Wire;

internal sealed record RateLimitInfoWire
{
    public required string Status { get; init; }

    [JsonPropertyName("resetsAt")]
    public long? ResetsAt { get; init; }

    [JsonPropertyName("rateLimitType")]
    public string? RateLimitType { get; init; }

    public double? Utilization { get; init; }

    [JsonPropertyName("overageStatus")]
    public string? OverageStatus { get; init; }

    [JsonPropertyName("overageResetsAt")]
    public long? OverageResetsAt { get; init; }

    [JsonPropertyName("overageDisabledReason")]
    public string? OverageDisabledReason { get; init; }
}
