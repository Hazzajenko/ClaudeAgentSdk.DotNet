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

    private sealed class ThrowingReader : TextReader
    {
        public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken) =>
            throw new IOException("pipe broken");
    }
}
