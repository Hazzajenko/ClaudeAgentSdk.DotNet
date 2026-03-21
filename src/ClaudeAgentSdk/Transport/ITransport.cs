using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Transport;

/// <summary>
/// Abstraction over the CLI subprocess communication.
/// Testable via mock implementations that feed synthetic JSON.
/// </summary>
internal interface ITransport : IAsyncDisposable
{
    /// <summary>
    /// Read messages from the CLI stdout stream.
    /// </summary>
    IAsyncEnumerable<Message> ReadMessagesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Write a JSON message to CLI stdin.
    /// </summary>
    Task WriteAsync(string json, CancellationToken cancellationToken = default);

    /// <summary>
    /// Close stdin to signal end of input.
    /// </summary>
    Task EndInputAsync(CancellationToken cancellationToken = default);
}
