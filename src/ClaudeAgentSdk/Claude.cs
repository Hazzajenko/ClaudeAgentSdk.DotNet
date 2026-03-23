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
    /// </summary>
    public static async IAsyncEnumerable<Message> QueryAsync(
        string prompt,
        ClaudeClientOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        options ??= new ClaudeClientOptions();
        string cliPath = CliFinder.Find(options.CliPath);
        var args = CliArgsBuilder.Build(options);
        var envVars = CliArgsBuilder.BuildEnvironmentVariables(options);

        var transport = new CliTransport(
            cliPath,
            args,
            options.WorkingDirectory,
            envVars,
            options.StderrCallback);

        await using (transport)
        {
            await transport.ConnectAsync(cancellationToken);

            await foreach (var message in QueryCoreAsync(prompt, transport, cancellationToken))
            {
                yield return message;
            }
        }
    }

    /// <summary>
    /// Core query logic extracted for testability. Sends prompt and streams messages.
    /// </summary>
    internal static async IAsyncEnumerable<Message> QueryCoreAsync(
        string prompt,
        ITransport transport,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string json = BuildUserMessageJson(prompt);
        await transport.WriteAsync(json, cancellationToken);

        await foreach (var message in transport.ReadMessagesAsync(cancellationToken))
        {
            yield return message;
        }
    }

    /// <summary>
    /// Send a query and return the final text result, discarding intermediate messages.
    /// </summary>
    public static async Task<string> QueryTextAsync(
        string prompt,
        ClaudeClientOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        string? resultText = null;

        await foreach (var message in QueryAsync(prompt, options, cancellationToken))
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
    /// Requires <see cref="ClaudeClientOptions.OutputFormatSchema"/> to be set.
    /// </summary>
    public static async Task<T?> QueryAsync<T>(
        string prompt,
        JsonTypeInfo<T> jsonTypeInfo,
        ClaudeClientOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await foreach (var message in QueryAsync(prompt, options, cancellationToken))
        {
            if (message is ResultMessage { StructuredOutput: { } output })
            {
                return output.Deserialize(jsonTypeInfo);
            }
        }

        return default;
    }

    internal static string BuildUserMessageJson(string prompt)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "user");
            writer.WriteStartObject("message");
            writer.WriteString("role", "user");
            writer.WriteString("content", prompt);
            writer.WriteEndObject();
            writer.WriteString("session_id", "default");
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
