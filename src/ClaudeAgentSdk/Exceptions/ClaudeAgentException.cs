namespace ClaudeAgentSdk.Exceptions;

public class ClaudeAgentException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class CliNotFoundException(string message, Exception? innerException = null)
    : ClaudeAgentException(message, innerException)
{
    public string? SearchedPath { get; init; }
}

public sealed class CliConnectionException(string message, Exception? innerException = null)
    : ClaudeAgentException(message, innerException)
{
    public int? ExitCode { get; init; }
    public string? Stderr { get; init; }
}

public sealed class MessageParseException(string message, Exception? innerException = null)
    : ClaudeAgentException(message, innerException)
{
    public string? Line { get; init; }
}

public sealed class CliVersionException(string message, Exception? innerException = null)
    : ClaudeAgentException(message, innerException);
