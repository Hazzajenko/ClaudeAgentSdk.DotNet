using ClaudeAgentSdk;
using ClaudeAgentSdk.Messages;
using ClaudeAgentSdk.Transport;

namespace TestingWithTransport;

/// <summary>
/// Example application service that wraps the Claude SDK.
/// Accepts an optional ITransport for testability — in production it's null (uses CLI),
/// in tests you pass a FakeTransport with canned responses.
/// </summary>
public sealed class SummaryService
{
    private readonly ClaudeClientOptions _options = new()
    {
        SystemPrompt = "You are a concise summarizer. Respond with only the summary.",
        MaxTurns = 1
    };

    public async Task<string> SummarizeAsync(
        string text, ITransport? transport = null, CancellationToken ct = default)
    {
        return await Claude.QueryTextAsync($"Summarize this: {text}", _options, transport, ct);
    }

    public async Task<SummaryResult> SummarizeWithMetadataAsync(
        string text, ITransport? transport = null, CancellationToken ct = default)
    {
        string? resultText = null;
        int turns = 0;
        bool isError = false;

        await foreach (var message in Claude.QueryAsync($"Summarize this: {text}", _options, transport, ct))
        {
            switch (message)
            {
                case AssistantMessage { Text: { } t }:
                    resultText = t;
                    break;
                case ResultMessage result:
                    turns = result.NumTurns;
                    isError = result.IsError;
                    if (result.Result is not null)
                        resultText = result.Result;
                    break;
            }
        }

        return new SummaryResult(resultText ?? "", turns, isError);
    }
}

public sealed record SummaryResult(string Text, int Turns, bool IsError);
