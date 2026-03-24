using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ClaudeAgentSdk.Messages;
using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk.Tests.Transport;

/// <summary>
/// Test double for ITransport that supports bidirectional message flow.
/// Queues output lines via Channel so writes can trigger additional output.
/// </summary>
internal sealed class MockTransport : ITransport
{
    private readonly Channel<string> _outputLines = Channel.CreateUnbounded<string>();

    public bool EndInputCalled { get; private set; }
    public int WriteCount { get; private set; }
    public List<string> WrittenMessages { get; } = [];

    /// <summary>
    /// Called after each WriteAsync — use to enqueue follow-up output that simulates CLI responses.
    /// </summary>
    public Action<string>? OnWrite { get; set; }

    public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public MockTransport(string initialOutput)
    {
        foreach (var line in initialOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.Length > 0)
                _outputLines.Writer.TryWrite(line);
        }
    }

    /// <summary>
    /// Enqueue additional output (simulates CLI responding to a write).
    /// </summary>
    public void EnqueueOutput(string json) => _outputLines.Writer.TryWrite(json);

    /// <summary>
    /// Signal no more output will come. Call after enqueuing all messages.
    /// </summary>
    public void Complete() => _outputLines.Writer.TryComplete();

    public Task WriteAsync(string json, CancellationToken cancellationToken = default)
    {
        WriteCount++;
        WrittenMessages.Add(json);
        OnWrite?.Invoke(json);
        return Task.CompletedTask;
    }

    public Task EndInputAsync(CancellationToken cancellationToken = default)
    {
        EndInputCalled = true;
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<Message> ReadMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var line in _outputLines.Reader.ReadAllAsync(cancellationToken))
        {
            await foreach (var msg in JsonStreamReader.ReadAllAsync(
                new StringReader(line), cancellationToken: cancellationToken))
            {
                yield return msg;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        _outputLines.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
