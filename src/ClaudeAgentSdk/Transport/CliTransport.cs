using System.Diagnostics;
using System.Runtime.CompilerServices;
using ClaudeAgentSdk.Exceptions;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Transport;

/// <summary>
/// Subprocess transport that spawns the Claude Code CLI and communicates via stdin/stdout JSON lines.
/// </summary>
internal sealed class CliTransport : ITransport
{
    private readonly string _cliPath;
    private readonly IReadOnlyList<string> _args;
    private readonly string? _workingDirectory;
    private readonly IDictionary<string, string>? _environmentVariables;
    private readonly Action<string>? _stderrCallback;
    private readonly bool _redirectStdin;
    private readonly int? _maxBufferSize;
    // The stderr pump lives as long as the process. The connect token is only for one call.
    private readonly CancellationTokenSource _stderrCts = new();
    private Process? _process;
    private bool _disposed;

    public CliTransport(
        string cliPath,
        IReadOnlyList<string> args,
        string? workingDirectory = null,
        IDictionary<string, string>? environmentVariables = null,
        Action<string>? stderrCallback = null,
        bool redirectStdin = true,
        int? maxBufferSize = null)
    {
        _cliPath = cliPath;
        _args = args;
        _workingDirectory = workingDirectory;
        _environmentVariables = environmentVariables;
        _stderrCallback = stderrCallback;
        _redirectStdin = redirectStdin;
        _maxBufferSize = maxBufferSize;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_process is not null)
        {
            return;
        }

        var startInfo = BuildStartInfo();

        try
        {
            _process = Process.Start(startInfo)
                ?? throw new CliConnectionException("Failed to start CLI process");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new CliNotFoundException(
                $"Claude Code CLI not found at '{_cliPath}'. Install with: npm install -g @anthropic-ai/claude-code",
                ex)
                { SearchedPath = _cliPath };
        }

        if (_stderrCallback is not null)
        {
            var stderr = _process.StandardError;
            var callback = _stderrCallback;
            var stderrToken = _stderrCts.Token;
            _ = Task.Run(() => PumpStderrAsync(stderr, callback, stderrToken), stderrToken);
        }
    }

    /// <summary>
    /// Builds the process start info for the CLI. Internal so tests can check it without a process.
    /// </summary>
    internal ProcessStartInfo BuildStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _cliPath,
            RedirectStandardInput = _redirectStdin,
            RedirectStandardOutput = true,
            // Nothing reads stderr without a callback. A redirected pipe that nobody reads fills and blocks the CLI.
            RedirectStandardError = _stderrCallback is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string arg in _args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        if (_workingDirectory is not null)
        {
            startInfo.WorkingDirectory = _workingDirectory;
        }

        // The parent may run inside Claude Code. With CLAUDECODE set, the child CLI acts as a nested session.
        startInfo.Environment.Remove("CLAUDECODE");

        // Set before the user values, so a user CLAUDE_CODE_ENTRYPOINT wins.
        if (_redirectStdin)
        {
            startInfo.Environment["CLAUDE_CODE_ENTRYPOINT"] = "sdk-dotnet";
        }

        if (_environmentVariables is not null)
        {
            foreach (var (key, value) in _environmentVariables)
            {
                startInfo.Environment[key] = value;
            }
        }

        return startInfo;
    }

    public async IAsyncEnumerable<Message> ReadMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_process is null)
        {
            throw new CliConnectionException("Not connected");
        }

        int bufferSize = _maxBufferSize ?? 1024 * 1024;
        await foreach (var message in JsonStreamReader.ReadAllAsync(
            _process.StandardOutput, bufferSize, cancellationToken))
        {
            yield return message;
        }
    }

    public async Task WriteAsync(string json, CancellationToken cancellationToken = default)
    {
        if (_process is null || _process.HasExited)
        {
            throw new CliConnectionException("Not connected or process has exited");
        }

        await _process.StandardInput.WriteLineAsync(json.AsMemory(), cancellationToken);
    }

    public Task EndInputAsync(CancellationToken cancellationToken = default)
    {
        if (_redirectStdin)
            _process?.StandardInput.Close();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        if (_process is null)
        {
            _stderrCts.Dispose();
            return;
        }

        // Close stdin to signal end of input
        if (_redirectStdin)
        {
            try
            {
                _process.StandardInput.Close();
            }
            catch
            {
                // Ignore — process may have already exited
            }
        }

        // Wait for graceful shutdown, then kill
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await _process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _process.Kill();
        }

        // Stop the stderr pump only after the process is gone, so its last lines still reach the callback.
        await _stderrCts.CancelAsync();
        _stderrCts.Dispose();
        _process.Dispose();
        _process = null;
    }

    /// <summary>
    /// Reads stderr lines until the stream ends and passes each one to the callback.
    /// An exception from the callback skips that line only. A read error stops the loop.
    /// </summary>
    internal static async Task PumpStderrAsync(
        TextReader stderr, Action<string> callback, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await stderr.ReadLineAsync(cancellationToken);
            }
            catch
            {
                // Cancellation, or the pipe closed or broke on shutdown.
                return;
            }

            if (line is null)
            {
                return;
            }

            try
            {
                callback(line);
            }
            catch
            {
                // A callback error must not stop later stderr lines.
            }
        }
    }
}
