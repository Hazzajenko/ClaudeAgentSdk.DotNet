using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using ClaudeAgentSdk.Json;
using ClaudeAgentSdk.Messages;
using ClaudeAgentSdk.Transport;

namespace TestingWithTransport;

/// <summary>
/// A reusable ITransport implementation for testing code that uses the Claude SDK.
/// Handles the control protocol init handshake automatically and returns canned content messages.
///
/// Usage:
///   var transport = FakeTransport.WithMessages("""
///       {"type":"assistant","message":{"content":[{"type":"text","text":"Hello"}],"model":"m"}}
///       {"type":"result","subtype":"success","duration_ms":100,"duration_api_ms":90,"is_error":false,"num_turns":1,"session_id":"s1","result":"Hello"}
///       """);
///
///   await foreach (var msg in Claude.QueryAsync("test", transport: transport)) { ... }
/// </summary>
public sealed class FakeTransport : ITransport
{
    private readonly Channel<string> _lines = Channel.CreateUnbounded<string>();
    private readonly string _contentMessages;

    /// <summary>
    /// All JSON strings written to the transport (init request, user messages, etc.).
    /// Useful for asserting that the correct prompt was sent.
    /// </summary>
    public List<string> WrittenMessages { get; } = [];

    private FakeTransport(string contentMessages)
    {
        _contentMessages = contentMessages;
    }

    /// <summary>
    /// Create a FakeTransport that returns the given JSON lines as content messages.
    /// The init handshake is handled automatically.
    /// </summary>
    public static FakeTransport WithMessages(string contentJsonLines) => new(contentJsonLines);

    public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task WriteAsync(string json, CancellationToken cancellationToken = default)
    {
        WrittenMessages.Add(json);

        if (json.Contains("\"type\":\"control_request\"") && json.Contains("initialize"))
        {
            // Auto-respond to init handshake with matching request_id
            using var doc = JsonDocument.Parse(json);
            string requestId = doc.RootElement.GetProperty("request_id").GetString()!;
            string response = "{\"type\":\"control_response\",\"response\":{\"subtype\":\"success\",\"request_id\":\"" + requestId + "\",\"response\":{}}}";
            _lines.Writer.TryWrite(response);
        }
        else if (json.Contains("\"type\":\"user\""))
        {
            // Enqueue canned content messages after user prompt
            foreach (string line in _contentMessages.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (line.Length > 0)
                    _lines.Writer.TryWrite(line);
            }
            _lines.Writer.TryComplete();
        }

        return Task.CompletedTask;
    }

    public Task EndInputAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async IAsyncEnumerable<Message> ReadMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (string line in _lines.Reader.ReadAllAsync(cancellationToken))
        {
            // ClaudeJsonOptions.DeserializeMessage is public and AOT-safe —
            // it parses a JSON line into the correct Message subtype.
            var message = ClaudeJsonOptions.DeserializeMessage(line);
            if (message is not null)
            {
                yield return message;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _lines.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
