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
    private Process? _process;
    private bool _disposed;

    public CliTransport(
        string cliPath,
        IReadOnlyList<string> args,
        string? workingDirectory = null,
        IDictionary<string, string>? environmentVariables = null,
        Action<string>? stderrCallback = null,
        bool redirectStdin = true)
    {
        _cliPath = cliPath;
        _args = args;
        _workingDirectory = workingDirectory;
        _environmentVariables = environmentVariables;
        _stderrCallback = stderrCallback;
        _redirectStdin = redirectStdin;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_process is not null)
        {
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _cliPath,
            RedirectStandardInput = _redirectStdin,
            RedirectStandardOutput = true,
            RedirectStandardError = _redirectStdin, // only redirect stderr when stdin is redirected (streaming mode)
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

        if (_environmentVariables is not null)
        {
            foreach (var (key, value) in _environmentVariables)
            {
                startInfo.Environment[key] = value;
            }
        }

        if (_redirectStdin)
            startInfo.Environment["CLAUDE_CODE_ENTRYPOINT"] = "sdk-dotnet";

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

        // Read stderr in background (only when stderr is redirected)
        if (_stderrCallback is not null && _redirectStdin)
        {
            _ = Task.Run(() => ReadStderrAsync(cancellationToken), cancellationToken);
        }
    }

    public async IAsyncEnumerable<Message> ReadMessagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_process is null)
        {
            throw new CliConnectionException("Not connected");
        }

        await foreach (var message in JsonStreamReader.ReadAllAsync(
            _process.StandardOutput, cancellationToken: cancellationToken))
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

        _process.Dispose();
        _process = null;
    }

    private async Task ReadStderrAsync(CancellationToken cancellationToken)
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string? line = await _process.StandardError.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }
                _stderrCallback?.Invoke(line);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch
        {
            // Ignore stderr read errors
        }
    }
}
