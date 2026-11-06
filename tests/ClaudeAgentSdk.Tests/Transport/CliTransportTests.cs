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
    public async Task BuildStartInfo_ParentHasClaudeCode_ChildEnvironmentHasNoClaudeCode()
    {
        string? before = Environment.GetEnvironmentVariable("CLAUDECODE");
        Environment.SetEnvironmentVariable("CLAUDECODE", "1");
        try
        {
            var transport = new CliTransport("claude", []);

            var startInfo = transport.BuildStartInfo();

            await Assert.That(startInfo.Environment.ContainsKey("CLAUDECODE")).IsFalse();
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLAUDECODE", before);
        }
    }

    [Test]
    public async Task BuildStartInfo_UserSetsClaudeCode_UserValueGoesToChild()
    {
        var env = new Dictionary<string, string> { ["CLAUDECODE"] = "1" };
        var transport = new CliTransport("claude", [], environmentVariables: env);

        var startInfo = transport.BuildStartInfo();

        await Assert.That(startInfo.Environment["CLAUDECODE"]).IsEqualTo("1");
    }

    [Test]
    public async Task BuildStartInfo_UserSetsEntrypoint_UserValueWins()
    {
        var env = new Dictionary<string, string> { ["CLAUDE_CODE_ENTRYPOINT"] = "my-app" };
        var transport = new CliTransport("claude", [], environmentVariables: env);

        var startInfo = transport.BuildStartInfo();

        await Assert.That(startInfo.Environment["CLAUDE_CODE_ENTRYPOINT"]).IsEqualTo("my-app");
    }

    [Test]
    public async Task BuildStartInfo_NoUserEntrypoint_SetsSdkEntrypoint()
    {
        var transport = new CliTransport("claude", []);

        var startInfo = transport.BuildStartInfo();

        await Assert.That(startInfo.Environment["CLAUDE_CODE_ENTRYPOINT"]).IsEqualTo("sdk-dotnet");
    }

    [Test]
    public async Task BuildStartInfo_WithWorkingDirectory_SetsPwdToWorkingDirectory()
    {
        string dir = Path.GetTempPath();
        var env = new Dictionary<string, string> { ["PWD"] = "/somewhere/else" };
        var transport = new CliTransport("claude", [], workingDirectory: dir, environmentVariables: env);

        var startInfo = transport.BuildStartInfo();

        await Assert.That(startInfo.Environment["PWD"]).IsEqualTo(dir);
    }

    [Test]
    public async Task BuildStartInfo_RelativeWorkingDirectory_SetsPwdToFullPath()
    {
        var transport = new CliTransport("claude", [], workingDirectory: "sub");

        var startInfo = transport.BuildStartInfo();

        await Assert.That(startInfo.Environment["PWD"]).IsEqualTo(Path.GetFullPath("sub"));
    }

    [Test]
    public async Task BuildStartInfo_EmptyWorkingDirectory_KeepsUserPwd()
    {
        var env = new Dictionary<string, string> { ["PWD"] = "/from/user" };
        var transport = new CliTransport("claude", [], workingDirectory: "", environmentVariables: env);

        var startInfo = transport.BuildStartInfo();

        await Assert.That(startInfo.Environment["PWD"]).IsEqualTo("/from/user");
    }

    [Test]
    public async Task BuildStartInfo_NoWorkingDirectory_KeepsUserPwd()
    {
        var env = new Dictionary<string, string> { ["PWD"] = "/from/user" };
        var transport = new CliTransport("claude", [], environmentVariables: env);

        var startInfo = transport.BuildStartInfo();

        await Assert.That(startInfo.Environment["PWD"]).IsEqualTo("/from/user");
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
