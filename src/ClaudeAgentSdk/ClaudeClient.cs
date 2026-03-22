using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using ClaudeAgentSdk.Control;
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
    private ControlProtocolHandler? _controlHandler;
    private Channel<Message>? _messageChannel;
    private Task? _readerTask;
    private CancellationTokenSource? _readerCts;
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

        // Send user message via control handler (write-locked)
        string json = BuildUserMessageJson(prompt, "default");
        await _controlHandler!.WriteAsync(json, cancellationToken);

        // Read from the channel until ResultMessage
        await foreach (var message in _messageChannel!.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;

            if (message is ResultMessage)
                yield break;
        }
    }

    /// <summary>
    /// Receive all messages from the conversation stream.
    /// Use after calling SendAsync or for continuous monitoring.
    /// </summary>
    public async IAsyncEnumerable<Message> ReceiveMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_messageChannel is null) yield break;

        await foreach (var message in _messageChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return message;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        // Stop background reader
        if (_readerCts is not null)
        {
            await _readerCts.CancelAsync();
            _readerCts.Dispose();
            _readerCts = null;
        }

        if (_readerTask is not null)
        {
            try { await _readerTask; } catch { }
            _readerTask = null;
        }

        // Fail any pending control requests
        _controlHandler?.FailAllPending(new ObjectDisposedException(nameof(ClaudeClient)));

        _messageChannel?.Writer.TryComplete();

        if (_transport is not null)
        {
            await _transport.DisposeAsync();
            _transport = null;
        }
    }

    internal ControlProtocolHandler? ControlHandler => _controlHandler;

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

        _controlHandler = new ControlProtocolHandler(_transport);
        _messageChannel = Channel.CreateBounded<Message>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        // Start background reader
        _readerCts = new CancellationTokenSource();
        _readerTask = Task.Run(() => ReadMessagesBackgroundAsync(_readerCts.Token), cancellationToken);
    }

    private async Task ReadMessagesBackgroundAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var message in _transport!.ReadMessagesAsync(cancellationToken))
            {
                switch (message)
                {
                    case ControlResponseMessage controlResponse:
                        _controlHandler!.HandleControlResponse(controlResponse.RawElement);
                        break;

                    case ControlRequestMessage controlRequest:
                        // Fire-and-forget — don't block the reader
                        _ = _controlHandler!.HandleControlRequestAsync(controlRequest.RawElement, cancellationToken);
                        break;

                    default:
                        await _messageChannel!.Writer.WriteAsync(message, cancellationToken);
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _controlHandler?.FailAllPending(ex);
        }
        finally
        {
            _messageChannel?.Writer.TryComplete();
        }
    }

    internal static string BuildUserMessageJson(string prompt, string sessionId)
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
