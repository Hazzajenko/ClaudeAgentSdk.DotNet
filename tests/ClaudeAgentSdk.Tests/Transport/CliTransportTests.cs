using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk.Tests.Transport;

public class CliTransportTests
{
    [Test]
    public async Task BuildStartInfo_NoStderrCallback_DoesNotRedirectStderr()
    {
        var transport = new CliTransport("claude", []);

        var startInfo = transport.BuildStartInfo();

        await Assert.That(startInfo.RedirectStandardError).IsFalse();
    }

    [Test]
    public async Task BuildStartInfo_WithStderrCallback_RedirectsStderr()
    {
        var transport = new CliTransport("claude", [], stderrCallback: _ => { });

        var startInfo = transport.BuildStartInfo();

        await Assert.That(startInfo.RedirectStandardError).IsTrue();
    }

    [Test]
    public async Task PumpStderrAsync_CallbackThrowsOnOneLine_StillReceivesNextLine()
    {
        var received = new List<string>();
        using var reader = new StringReader("first\nsecond\nthird\n");

        await CliTransport.PumpStderrAsync(reader, line =>
        {
            received.Add(line);
            if (line == "first")
            {
                throw new InvalidOperationException("callback failed");
            }
        }, CancellationToken.None);

        await Assert.That(received).IsEquivalentTo(["first", "second", "third"]);
    }

    [Test]
    public async Task PumpStderrAsync_ReaderThrows_StopsWithoutThrowing()
    {
        var received = new List<string>();

        await CliTransport.PumpStderrAsync(new ThrowingReader(), received.Add, CancellationToken.None);

        await Assert.That(received).IsEmpty();
    }

    [Test]
    public async Task ConnectAsync_ConnectTokenCancelled_StillReadsLaterStderr()
    {
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<string> callback = line => received.TrySetResult(line.Trim());
        // The child writes one stderr line about 2 seconds after it starts.
        await using var transport = OperatingSystem.IsWindows()
            ? new CliTransport("cmd.exe", ["/c", "ping -n 3 127.0.0.1 >nul & echo late 1>&2"], stderrCallback: callback)
            : new CliTransport("/bin/sh", ["-c", "sleep 2; echo late >&2"], stderrCallback: callback);
        using var connectCts = new CancellationTokenSource();

        await transport.ConnectAsync(connectCts.Token);
        await connectCts.CancelAsync();

        string line = await received.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await Assert.That(line).IsEqualTo("late");
    }

    private sealed class ThrowingReader : TextReader
    {
        public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) =>
            throw new IOException("pipe broken");
    }
}
