using ClaudeAgentSdk.Messages;
using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk.Tests.Transport;

/// <summary>
/// Test double for ITransport that returns predefined CLI output
/// and tracks stdin lifecycle calls.
/// </summary>
internal sealed class MockTransport : ITransport
{
    private readonly string _outputJson;

    public bool EndInputCalled { get; private set; }
    public int WriteCount { get; private set; }
    public string? LastWrittenJson { get; private set; }

    public MockTransport(string outputJson) => _outputJson = outputJson;

    public Task WriteAsync(string json, CancellationToken cancellationToken = default)
    {
        WriteCount++;
        LastWrittenJson = json;
        return Task.CompletedTask;
    }

    public Task EndInputAsync(CancellationToken cancellationToken = default)
    {
        EndInputCalled = true;
        return Task.CompletedTask;
    }

    public IAsyncEnumerable<Message> ReadMessagesAsync(CancellationToken cancellationToken = default)
        => JsonStreamReader.ReadAllAsync(new StringReader(_outputJson), cancellationToken: cancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
