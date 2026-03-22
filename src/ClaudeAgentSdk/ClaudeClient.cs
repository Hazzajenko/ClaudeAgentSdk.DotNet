using System.Runtime.CompilerServices;
using System.Text.Json;
using ClaudeAgentSdk.Messages;
using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk;

/// <summary>
/// Stateful client for bidirectional, multi-turn conversations with Claude Code.
/// For simple one-shot queries, use <see cref="Claude.QueryAsync"/> instead.
/// </summary>
public sealed class ClaudeClient : IAsyncDisposable
{
    private readonly ClaudeClientOptions _options;
    private CliTransport? _transport;
    private bool _disposed;

    public ClaudeClient(ClaudeClientOptions? options = null)
    {
        _options = options ?? new ClaudeClientOptions();
    }

    /// <summary>
    /// Send a prompt and stream back messages as they arrive.
    /// Can be called multiple times for multi-turn conversations.
    /// </summary>
    public async IAsyncEnumerable<Message> SendAsync(
        string prompt,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_transport is null)
        {
            await ConnectAsync(cancellationToken);
        }

        // Send user message
        string json = BuildUserMessageJson(prompt, "default");
        await _transport!.WriteAsync(json, cancellationToken);

        await foreach (var message in _transport.ReadMessagesAsync(cancellationToken))
        {
            yield return message;

            // ResultMessage signals end of this turn
            if (message is ResultMessage)
            {
                yield break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        if (_transport is not null)
        {
            await _transport.DisposeAsync();
            _transport = null;
        }
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        string cliPath = CliFinder.Find(_options.CliPath);
        var args = CliArgsBuilder.Build(_options);
        var envVars = CliArgsBuilder.BuildEnvironmentVariables(_options);

        _transport = new CliTransport(
            cliPath,
            args,
            _options.WorkingDirectory,
            envVars,
            _options.StderrCallback);

        await _transport.ConnectAsync(cancellationToken);
    }

    private static string BuildUserMessageJson(string prompt, string sessionId)
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
            writer.WriteString("session_id", sessionId);
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }
}
