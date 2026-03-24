using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ClaudeAgentSdk.Messages;
using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk;

/// <summary>
/// One-shot query interface for Claude Code CLI.
/// For stateful multi-turn conversations, use <see cref="ClaudeClient"/>.
/// </summary>
public static class Claude
{
    /// <summary>
    /// Send a one-shot query and stream back messages as they arrive.
    /// Uses streaming mode with initialize handshake internally (matching Python SDK).
    /// </summary>
    public static async IAsyncEnumerable<Message> QueryAsync(
        string prompt,
        ClaudeClientOptions? options = null,
        ITransport? transport = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new ClaudeClientOptions();

        await using var client = new ClaudeClient(options, transport);
        await foreach (var message in client.SendAsync(prompt, cancellationToken))
        {
            yield return message;
        }
    }

    /// <summary>
    /// Core query logic extracted for testability. Sends initialize, writes user message,
    /// then yields content messages until ResultMessage.
    /// </summary>
    internal static async IAsyncEnumerable<Message> QueryCoreAsync(
        ITransport transport,
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const string initializeRequestJson =
            """{"type":"control_request","request_id":"init_1","request":{"subtype":"initialize","hooks":null}}""";

        // Send initialize control request
        await transport.WriteAsync(initializeRequestJson, cancellationToken);

        bool userMessageSent = false;

        await foreach (var message in transport.ReadMessagesAsync(cancellationToken))
        {
            switch (message)
            {
                case ControlResponseMessage when !userMessageSent:
                    userMessageSent = true;
                    string userJson = ClaudeClient.BuildUserMessageJson(prompt, "");
                    await transport.WriteAsync(userJson, cancellationToken);
                    continue;

                case ControlResponseMessage:
                case ControlRequestMessage:
                    continue; // skip control messages

                case ResultMessage:
                    yield return message;
                    yield break;

                default:
                    yield return message;
                    break;
            }
        }
    }

    /// <summary>
    /// Send a query and return the final text result, discarding intermediate messages.
    /// </summary>
    public static async Task<string> QueryTextAsync(
        string prompt,
        ClaudeClientOptions? options = null,
        ITransport? transport = null,
        CancellationToken cancellationToken = default)
    {
        string? resultText = null;

        await foreach (var message in QueryAsync(prompt, options, transport, cancellationToken))
        {
            switch (message)
            {
                case ResultMessage { Result: not null } result:
                    resultText = result.Result;
                    break;
                case AssistantMessage assistant:
                    // Collect text blocks as fallback
                    foreach (var block in assistant.Content)
                    {
                        if (block is TextBlock textBlock)
                        {
                            resultText = textBlock.Text;
                        }
                    }
                    break;
            }
        }

        return resultText ?? "";
    }

    /// <summary>
    /// Send a query and return a deserialized structured output of type <typeparamref name="T"/>.
    /// Auto-derives the JSON schema from <paramref name="jsonTypeInfo"/> when
    /// <see cref="ClaudeClientOptions.OutputFormatSchema"/> is not explicitly set.
    /// </summary>
    public static async Task<T?> QueryAsync<T>(
        string prompt,
        JsonTypeInfo<T> jsonTypeInfo,
        ClaudeClientOptions? options = null,
        ITransport? transport = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ClaudeClientOptions();

        if (options.OutputFormatSchema is null)
        {
            options = options with
            {
                OutputFormatSchema = Json.JsonSchemaDeriver.FromTypeInfo(jsonTypeInfo)
            };
        }

        await foreach (var message in QueryAsync(prompt, options, transport, cancellationToken))
        {
            if (message is ResultMessage { StructuredOutput: { } output })
            {
                return output.Deserialize(jsonTypeInfo);
            }
        }

        return default;
    }

    /// <summary>
    /// Returns the full CLI command that would be executed, for debugging.
    /// </summary>
    public static string GetCliCommand(string prompt, ClaudeClientOptions? options = null)
    {
        options ??= new ClaudeClientOptions();
        string cliPath = CliFinder.Find(options.CliPath);
        var args = CliArgsBuilder.Build(options);

        static string ShellEscape(string a)
        {
            if (a.Length == 0) return "\"\"";
            if (a.Contains('\'') || a.Contains('"') || a.Contains(' ') || a.Contains('{'))
                return "'" + a.Replace("'", "'\\''") + "'";
            return a;
        }

        string escapedArgs = string.Join(" ", args.Select(ShellEscape));

        return $"{cliPath} {escapedArgs}";
    }

    /// <summary>
    /// Format a message as a concise debug string. Use with <see cref="ClaudeClientOptions.MessageCallback"/>:
    /// <c>MessageCallback = msg => Console.Error.WriteLine(Claude.FormatMessage(msg))</c>
    /// </summary>
    public static string FormatMessage(Message message) => message switch
    {
        TaskStartedMessage t => $"[task_started] {t.TaskId}: {t.Description}",
        TaskProgressMessage t => $"[task_progress] {t.TaskId} tokens={t.Usage.TotalTokens}",
        TaskNotificationMessage t => $"[task_notification] {t.TaskId}: {t.Status}",
        SystemMessage s => $"[system] {s.Subtype}",
        AssistantMessage a => $"[assistant] {FormatBlocks(a.Content)}",
        UserMessage u => $"[user] {FormatUserContent(u.Content)}",
        ResultMessage r => $"[result] turns={r.NumTurns} error={r.IsError} cost=${r.TotalCostUsd:F4}",
        RateLimitEvent r => $"[rate_limit] {r.RateLimitInfo.Status}",
        StreamEvent => "[stream_event]",
        _ => $"[{message.GetType().Name}]"
    };

    private static string FormatBlocks(IReadOnlyList<ContentBlock> blocks) =>
        $"{blocks.Count} blocks: {string.Join(", ", blocks.Select(b => b switch
        {
            TextBlock t => $"text({t.Text.Length}ch)",
            ThinkingBlock t => $"thinking({t.Thinking.Length}ch)",
            ToolUseBlock t => $"tool_use:{t.Name}",
            ToolResultBlock t => $"tool_result:{t.ToolUseId}",
            _ => b.GetType().Name
        }))}";

    private static string FormatUserContent(object content) => content switch
    {
        string s => $"text({s.Length}ch)",
        IReadOnlyList<ContentBlock> blocks => FormatBlocks(blocks),
        _ => content.GetType().Name
    };
}
