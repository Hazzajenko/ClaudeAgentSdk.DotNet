using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using ClaudeAgentSdk.Agents;
using ClaudeAgentSdk.Control;
using ClaudeAgentSdk.Hooks;
using ClaudeAgentSdk.Mcp;
using ClaudeAgentSdk.Mcp.Sdk;
using ClaudeAgentSdk.Messages;
using ClaudeAgentSdk.Permissions;
using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk;

/// <summary>
/// Stateful client for bidirectional, multi-turn conversations with Claude Code.
/// For simple one-shot queries, use <see cref="Claude.QueryAsync"/> instead.
/// </summary>
public sealed class ClaudeClient : IAsyncDisposable
{
    private readonly ClaudeClientOptions _options;
    private readonly ITransport? _injectedTransport;
    private ITransport? _transport;
    private ControlProtocolHandler? _controlHandler;
    private Channel<Message>? _messageChannel;
    private Task? _readerTask;
    private CancellationTokenSource? _readerCts;
    private JsonElement _initializationResult;
    private readonly Dictionary<string, HookCallback> _hookCallbacks = new();
    private Dictionary<string, SdkMcpRequestHandler>? _sdkMcpHandlers;
    private int _nextCallbackId;
    private bool _disposed;

    public ClaudeClient(ClaudeClientOptions? options = null, ITransport? transport = null)
    {
        _options = options ?? new ClaudeClientOptions();
        _injectedTransport = transport;
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

        await foreach (var message in DrainUntilResultAsync(cancellationToken))
            yield return message;
    }

    /// <summary>
    /// Send a multimodal user message (text + images, tool results) and stream back messages.
    /// Use for image input, tool result responses, or any structured user content.
    /// </summary>
    public async IAsyncEnumerable<Message> SendAsync(
        IReadOnlyList<UserContentBlock> content,
        string sessionId = "default",
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(content);

        if (_transport is null)
        {
            await ConnectAsync(cancellationToken);
        }

        string json = BuildUserMessageJson(content, sessionId);
        await _controlHandler!.WriteAsync(json, cancellationToken);

        await foreach (var message in DrainUntilResultAsync(cancellationToken))
            yield return message;
    }

    /// <summary>
    /// Escape hatch: write a raw user-message JSON line to the CLI and stream back messages.
    /// Mirrors the Python SDK's <c>AsyncIterable[dict]</c> input mode for content shapes
    /// not modeled by <see cref="UserContentBlock"/>. The JSON must be a complete user message
    /// (e.g. <c>{"type":"user","message":{...},"session_id":"..."}</c>) — validated as well-formed JSON
    /// before write but not against any schema.
    /// </summary>
    public async IAsyncEnumerable<Message> SendRawAsync(
        string messageJson,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(messageJson);

        // Fail fast on malformed JSON before opening the transport
        using (JsonDocument.Parse(messageJson)) { }

        if (_transport is null)
        {
            await ConnectAsync(cancellationToken);
        }

        await _controlHandler!.WriteAsync(messageJson, cancellationToken);

        await foreach (var message in DrainUntilResultAsync(cancellationToken))
            yield return message;
    }

    private async IAsyncEnumerable<Message> DrainUntilResultAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var message in _messageChannel!.Reader.ReadAllAsync(cancellationToken))
        {
            _options.MessageCallback?.Invoke(message);
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

    /// <summary>
    /// Receive messages from the conversation stream, stopping after the first ResultMessage.
    /// Use after writing a user message directly for multi-turn flow.
    /// </summary>
    public async IAsyncEnumerable<Message> ReceiveResponseAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_messageChannel is null)
            throw new InvalidOperationException("Not connected");

        await foreach (var message in _messageChannel.Reader.ReadAllAsync(cancellationToken))
        {
            _options.MessageCallback?.Invoke(message);
            yield return message;

            if (message is ResultMessage)
                yield break;
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
        await SendControlRequestAsync(ControlRequests.SubtypeOnly("interrupt"), cancellationToken);

    public async Task SetPermissionModeAsync(PermissionMode mode, CancellationToken cancellationToken = default)
    {
        string modeStr = mode switch
        {
            PermissionMode.AcceptEdits => "acceptEdits",
            PermissionMode.Plan => "plan",
            PermissionMode.BypassPermissions => "bypassPermissions",
            _ => "default"
        };
        await SendControlRequestAsync(ControlRequests.SetPermissionMode(modeStr), cancellationToken);
    }

    public async Task SetModelAsync(string? model, CancellationToken cancellationToken = default) =>
        await SendControlRequestAsync(ControlRequests.SetModel(model), cancellationToken);

    public async Task RewindFilesAsync(string userMessageId, CancellationToken cancellationToken = default) =>
        await SendControlRequestAsync(ControlRequests.RewindFiles(userMessageId), cancellationToken);

    public async Task ReconnectMcpServerAsync(string serverName, CancellationToken cancellationToken = default) =>
        await SendControlRequestAsync(ControlRequests.McpReconnect(serverName), cancellationToken);

    public async Task ToggleMcpServerAsync(string serverName, bool enabled, CancellationToken cancellationToken = default) =>
        await SendControlRequestAsync(ControlRequests.McpToggle(serverName, enabled), cancellationToken);

    public async Task StopTaskAsync(string taskId, CancellationToken cancellationToken = default) =>
        await SendControlRequestAsync(ControlRequests.StopTask(taskId), cancellationToken);

    public async Task<Mcp.McpStatusResponse> GetMcpStatusAsync(CancellationToken cancellationToken = default)
    {
        var result = await SendControlRequestAsync(ControlRequests.SubtypeOnly("mcp_status"), cancellationToken);
        return Mcp.McpStatusParser.Parse(result);
    }

    private async Task<JsonElement> SendControlRequestAsync(JsonElement request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_controlHandler is null) throw new InvalidOperationException("Not connected");

        return await _controlHandler.SendControlRequestAsync(request, cancellationToken: cancellationToken);
    }

    private async Task<HookOutput> HandleHookCallbackAsync(
        string callbackId, HookInput input, CancellationToken ct)
    {
        if (!_hookCallbacks.TryGetValue(callbackId, out var callback))
            throw new InvalidOperationException($"No hook callback found for ID: {callbackId}");

        return await callback(input, ct);
    }

    /// <summary>
    /// Returns the initialization handshake result from the CLI.
    /// Must be connected first (call SendAsync or connect explicitly).
    /// </summary>
    public JsonElement GetServerInfo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_transport is null)
            throw new InvalidOperationException("Not connected");
        return _initializationResult;
    }

    internal ControlProtocolHandler? ControlHandler => _controlHandler;

    private void WireSdkMcpServers()
    {
        if (_options.McpServers is null) return;

        var sdkServers = _options.McpServers
            .Where(kvp => kvp.Value is McpSdkServerConfig)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => new SdkMcpRequestHandler((McpSdkServerConfig)kvp.Value));

        if (sdkServers.Count == 0) return;

        _sdkMcpHandlers = sdkServers;
        _controlHandler!.OnMcpMessage = HandleMcpMessageAsync;
    }

    private async Task<JsonElement> HandleMcpMessageAsync(
        string serverName, JsonElement message, CancellationToken ct)
    {
        if (_sdkMcpHandlers is null || !_sdkMcpHandlers.TryGetValue(serverName, out var handler))
            throw new InvalidOperationException($"SDK MCP server '{serverName}' not found");

        return await handler.HandleAsync(message, ct);
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (_injectedTransport is not null)
        {
            _transport = _injectedTransport;
            await _transport.ConnectAsync(cancellationToken);
        }
        else
        {
            string cliPath = CliFinder.Find(_options.CliPath);
            await CliVersionChecker.CheckAsync(cliPath, cancellationToken);

            var args = CliArgsBuilder.Build(_options);
            var envVars = CliArgsBuilder.BuildEnvironmentVariables(_options);

            var cliTransport = new CliTransport(
                cliPath,
                args,
                _options.WorkingDirectory,
                envVars,
                _options.StderrCallback,
                maxBufferSize: _options.MaxBufferSize);

            await cliTransport.ConnectAsync(cancellationToken);
            _transport = cliTransport;
        }

        _controlHandler = new ControlProtocolHandler(_transport);
        _messageChannel = Channel.CreateBounded<Message>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        // Wire up hook callback dispatch
        _controlHandler.OnHookCallback = HandleHookCallbackAsync;

        // Wire up can_use_tool dispatch
        if (_options.CanUseTool is not null)
            _controlHandler.OnCanUseTool = _options.CanUseTool;

        // Wire up SDK MCP server routing
        WireSdkMcpServers();

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
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    internal static string BuildUserMessageJson(IReadOnlyList<UserContentBlock> blocks, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        if (blocks.Count == 0)
            throw new ArgumentException("Content block list must not be empty.", nameof(blocks));

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "user");
            writer.WriteStartObject("message");
            writer.WriteString("role", "user");
            writer.WriteStartArray("content");
            foreach (var block in blocks)
                WriteUserContentBlock(writer, block);
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteString("session_id", sessionId);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteUserContentBlock(Utf8JsonWriter w, UserContentBlock block)
    {
        switch (block)
        {
            case UserTextBlock text:
                w.WriteStartObject();
                w.WriteString("type", "text");
                w.WriteString("text", text.Text);
                w.WriteEndObject();
                break;

            case UserImageBlock image:
                w.WriteStartObject();
                w.WriteString("type", "image");
                w.WriteStartObject("source");
                w.WriteString("type", "base64");
                w.WriteString("media_type", image.MediaType);
                w.WriteString("data", image.Base64Data);
                w.WriteEndObject();
                w.WriteEndObject();
                break;

            case UserImageUrlBlock imageUrl:
                w.WriteStartObject();
                w.WriteString("type", "image");
                w.WriteStartObject("source");
                w.WriteString("type", "url");
                w.WriteString("url", imageUrl.Url);
                w.WriteEndObject();
                w.WriteEndObject();
                break;

            case UserToolResultBlock toolResult:
                w.WriteStartObject();
                w.WriteString("type", "tool_result");
                w.WriteString("tool_use_id", toolResult.ToolUseId);
                if (toolResult.Content is not null)
                    w.WriteString("content", toolResult.Content);
                if (toolResult.IsError is { } isError)
                    w.WriteBoolean("is_error", isError);
                w.WriteEndObject();
                break;

            default:
                throw new ArgumentException($"Unsupported UserContentBlock type: {block.GetType().Name}", nameof(block));
        }
    }
}
