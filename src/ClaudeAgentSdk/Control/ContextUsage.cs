using System.Text.Json;

namespace ClaudeAgentSdk.Control;

/// <summary>
/// One context usage category, for example the system prompt, tools or messages.
/// </summary>
public sealed record ContextUsageCategory(string Name, int Tokens, string Color, bool? IsDeferred = null);

/// <summary>
/// Response from <see cref="ClaudeClient.GetContextUsageAsync"/>. It has the same data as the
/// CLI <c>/context</c> command. The CLI gives the list items in <see cref="MemoryFiles"/>,
/// <see cref="McpTools"/>, <see cref="Agents"/>, <see cref="GridRows"/> and
/// <see cref="DeferredBuiltinTools"/> no fixed shape, so they stay as cloned <see cref="JsonElement"/> values.
/// </summary>
public sealed record ContextUsageResponse
{
    /// <summary>Token usage by category.</summary>
    public required IReadOnlyList<ContextUsageCategory> Categories { get; init; }

    /// <summary>Total tokens in the context window now.</summary>
    public required int TotalTokens { get; init; }

    /// <summary>Effective maximum tokens. The autocompact buffer can make it less than <see cref="RawMaxTokens"/>.</summary>
    public required int MaxTokens { get; init; }

    /// <summary>Raw context window size of the model.</summary>
    public required int RawMaxTokens { get; init; }

    /// <summary>Percent of the context window in use, from 0 to 100.</summary>
    public required double Percentage { get; init; }

    /// <summary>Model that the usage is calculated for.</summary>
    public required string Model { get; init; }

    /// <summary>True when autocompact is on for this session.</summary>
    public required bool IsAutoCompactEnabled { get; init; }

    /// <summary>CLAUDE.md and memory files, with path, type and token counts.</summary>
    public required IReadOnlyList<JsonElement> MemoryFiles { get; init; }

    /// <summary>MCP tools, with name, serverName, tokens and isLoaded.</summary>
    public required IReadOnlyList<JsonElement> McpTools { get; init; }

    /// <summary>Agent definitions, with agentType, source and token counts.</summary>
    public required IReadOnlyList<JsonElement> Agents { get; init; }

    /// <summary>Grid rows that the CLI context display draws.</summary>
    public required IReadOnlyList<IReadOnlyList<JsonElement>> GridRows { get; init; }

    /// <summary>Token count at which autocompact starts. Null when the CLI does not send it.</summary>
    public int? AutoCompactThreshold { get; init; }

    /// <summary>Built-in tools deferred from the first tool list. Null when the CLI does not send it.</summary>
    public IReadOnlyList<JsonElement>? DeferredBuiltinTools { get; init; }

    /// <summary>The full response object, cloned. Use it for fields that this type does not have.</summary>
    public required JsonElement Raw { get; init; }
}
