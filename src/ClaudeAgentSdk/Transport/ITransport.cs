using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Transport;

/// <summary>
/// Abstraction over the CLI subprocess communication.
/// Implement to inject custom transports for testing or non-CLI backends.
/// </summary>
public interface ITransport : IAsyncDisposable
{
    /// <summary>
    /// Establish the connection. Called once before read/write.
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

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
