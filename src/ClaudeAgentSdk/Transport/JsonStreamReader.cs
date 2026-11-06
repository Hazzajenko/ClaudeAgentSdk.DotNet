using System.Runtime.CompilerServices;
using ClaudeAgentSdk.Exceptions;
using ClaudeAgentSdk.Json;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Transport;

/// <summary>
/// Reads newline-delimited JSON from a TextReader and yields parsed Messages.
/// Each line is parsed on its own. Blank lines and lines that do not start with
/// <c>{</c> are skipped, so debug output from the CLI on stdout does not stop the stream.
/// </summary>
internal static class JsonStreamReader
{
    private const int DefaultMaxBufferSize = 1024 * 1024; // 1MB
    private const int MaxLineInException = 100;

    public static async IAsyncEnumerable<Message> ReadAllAsync(
        TextReader reader,
        int maxBufferSize = DefaultMaxBufferSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? line = await ReadLineAsync(reader, cancellationToken);
            if (line is null)
            {
                break;
            }

            line = line.Trim();
            if (!line.StartsWith('{'))
            {
                // Blank line or non-JSON output such as "[SandboxDebug] ...". Skip it.
                continue;
            }

            if (line.Length > maxBufferSize)
            {
                throw new InvalidOperationException(
                    $"JSON message exceeded maximum buffer size of {maxBufferSize} bytes");
            }

            Message? message;
            try
            {
                message = ClaudeJsonOptions.DeserializeMessage(line);
            }
            catch (Exception ex)
            {
                string truncated = line.Length > MaxLineInException ? line[..MaxLineInException] : line;
                throw new MessageParseException(
                    $"Failed to parse CLI JSON: {ex.Message}", ex)
                    { Line = truncated };
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
