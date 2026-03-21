using System.Runtime.CompilerServices;
using ClaudeAgentSdk.Json;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Transport;

/// <summary>
/// Reads newline-delimited JSON from a TextReader and yields parsed Messages.
/// Handles partial JSON lines by buffering until a complete object is received.
/// </summary>
internal static class JsonStreamReader
{
    private const int DefaultMaxBufferSize = 1024 * 1024; // 1MB

    public static async IAsyncEnumerable<Message> ReadAllAsync(
        TextReader reader,
        int maxBufferSize = DefaultMaxBufferSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string jsonBuffer = "";

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? line = await ReadLineAsync(reader, cancellationToken);
            if (line is null)
            {
                break;
            }

            line = line.Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            jsonBuffer += line;

            if (jsonBuffer.Length > maxBufferSize)
            {
                jsonBuffer = "";
                throw new InvalidOperationException(
                    $"JSON message exceeded maximum buffer size of {maxBufferSize} bytes");
            }

            Message? message;
            try
            {
                message = ClaudeJsonOptions.DeserializeMessage(jsonBuffer);
                jsonBuffer = "";
            }
            catch
            {
                // Partial JSON — keep buffering
                continue;
            }

            if (message is not null)
            {
                yield return message;
            }
        }
    }

    private static Task<string?> ReadLineAsync(TextReader reader, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return reader.ReadLineAsync(cancellationToken).AsTask();
    }
}
