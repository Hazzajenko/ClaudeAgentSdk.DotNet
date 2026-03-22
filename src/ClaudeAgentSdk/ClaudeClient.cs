using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using ClaudeAgentSdk.Agents;
using ClaudeAgentSdk.Control;
using ClaudeAgentSdk.Hooks;
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
    private JsonElement _initializationResult;
    private readonly Dictionary<string, HookCallback> _hookCallbacks = new();
    private int _nextCallbackId;
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

    public async Task InterruptAsync(CancellationToken cancellationToken = default) =>
        await SendSimpleControlRequestAsync("""{"subtype":"interrupt"}""", cancellationToken);

    public async Task SetPermissionModeAsync(PermissionMode mode, CancellationToken cancellationToken = default)
    {
        string modeStr = mode switch
        {
            PermissionMode.AcceptEdits => "acceptEdits",
            PermissionMode.Plan => "plan",
            PermissionMode.BypassPermissions => "bypassPermissions",
            _ => "default"
        };
        await SendSimpleControlRequestAsync($$$"""{"subtype":"set_permission_mode","mode":"{{{modeStr}}}"}""", cancellationToken);
    }

    public async Task SetModelAsync(string? model, CancellationToken cancellationToken = default)
    {
        string json = model is not null
            ? $$$"""{"subtype":"set_model","model":"{{{model}}}"}"""
            : """{"subtype":"set_model","model":null}""";
        await SendSimpleControlRequestAsync(json, cancellationToken);
    }

    public async Task RewindFilesAsync(string userMessageId, CancellationToken cancellationToken = default) =>
        await SendSimpleControlRequestAsync($$$"""{"subtype":"rewind_files","user_message_id":"{{{userMessageId}}}"}""", cancellationToken);

    public async Task ReconnectMcpServerAsync(string serverName, CancellationToken cancellationToken = default) =>
        await SendSimpleControlRequestAsync($$$"""{"subtype":"mcp_reconnect","serverName":"{{{serverName}}}"}""", cancellationToken);

    public async Task ToggleMcpServerAsync(string serverName, bool enabled, CancellationToken cancellationToken = default)
    {
        string enabledStr = enabled ? "true" : "false";
        await SendSimpleControlRequestAsync($$$"""{"subtype":"mcp_toggle","serverName":"{{{serverName}}}","enabled":{{{enabledStr}}}}""", cancellationToken);
    }

    public async Task StopTaskAsync(string taskId, CancellationToken cancellationToken = default) =>
        await SendSimpleControlRequestAsync($$$"""{"subtype":"stop_task","task_id":"{{{taskId}}}"}""", cancellationToken);

    public async Task<Mcp.McpStatusResponse> GetMcpStatusAsync(CancellationToken cancellationToken = default)
    {
        var result = await SendSimpleControlRequestAsync("""{"subtype":"mcp_status"}""", cancellationToken);
        return Mcp.McpStatusParser.Parse(result);
    }

    private async Task<JsonElement> SendSimpleControlRequestAsync(string requestJson, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_controlHandler is null) throw new InvalidOperationException("Not connected");

        using var doc = JsonDocument.Parse(requestJson);
        return await _controlHandler.SendControlRequestAsync(doc.RootElement.Clone(), cancellationToken: cancellationToken);
    }

    private async Task<JsonElement> HandleHookCallbackAsync(
        string callbackId, JsonElement input, string? toolUseId, CancellationToken ct)
    {
        if (!_hookCallbacks.TryGetValue(callbackId, out var callback))
            throw new InvalidOperationException($"No hook callback found for ID: {callbackId}");
        return await callback(input, toolUseId, ct);
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

        // Wire up hook callback dispatch
        _controlHandler.OnHookCallback = HandleHookCallbackAsync;

        // Start background reader
        _readerCts = new CancellationTokenSource();
        _readerTask = Task.Run(() => ReadMessagesBackgroundAsync(_readerCts.Token), cancellationToken);

        // Send initialize handshake
        await InitializeAsync(cancellationToken);
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(BuildInitializeRequestJson());
        var response = await _controlHandler!.SendControlRequestAsync(
            doc.RootElement.Clone(),
            TimeSpan.FromSeconds(120), // longer timeout — MCP servers may take time to start
            cancellationToken);
        _initializationResult = response;
    }

    private string BuildInitializeRequestJson()
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("subtype", "initialize");

            if (_options.Hooks is { Count: > 0 })
            {
                w.WriteStartObject("hooks");
                foreach (var (evt, matchers) in _options.Hooks)
                {
                    w.WriteStartArray(evt.ToWireString());
                    foreach (var matcher in matchers)
                    {
                        w.WriteStartObject();
                        if (matcher.Matcher is not null)
                            w.WriteString("matcher", matcher.Matcher);
                        else
                            w.WriteNull("matcher");

                        w.WriteStartArray("hookCallbackIds");
                        foreach (var callback in matcher.Hooks)
                        {
                            string callbackId = $"hook_{_nextCallbackId++}";
                            _hookCallbacks[callbackId] = callback;
                            w.WriteStringValue(callbackId);
                        }
                        w.WriteEndArray();

                        if (matcher.TimeoutSeconds is { } timeout)
                            w.WriteNumber("timeout", timeout);

                        w.WriteEndObject();
                    }
                    w.WriteEndArray();
                }
                w.WriteEndObject();
            }
            else
            {
                w.WriteNull("hooks");
            }

            if (_options.Agents is { Count: > 0 })
            {
                AgentSerializer.WriteAgentsProperty(w, _options.Agents);
            }

            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
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
