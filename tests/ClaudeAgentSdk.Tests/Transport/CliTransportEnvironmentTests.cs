using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk.Tests.Transport;

public class CliTransportEnvironmentTests
{
    [Test]
    public async Task ConnectAsync_PassesCleanEnvironmentToChild()
    {
        bool windows = OperatingSystem.IsWindows();
        string shell = windows ? "cmd.exe" : "/bin/sh";
        string command = windows
            ? "if defined CLAUDECODE (echo present 1>&2) else (echo missing 1>&2)"
            : "if [ -n \"${CLAUDECODE+x}\" ]; then echo present >&2; else echo missing >&2; fi";
        var output = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var variables = new Dictionary<string, string> { ["CLAUDECODE"] = "nested" };
        await using var transport = new CliTransport(shell, [windows ? "/c" : "-c", command],
            environmentVariables: variables, stderrCallback: line => output.TrySetResult(line));

        await transport.ConnectAsync();

        string marker = await output.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.That(marker.Trim()).IsEqualTo("missing");
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task StartInfo_RemovesNestedSessionMarkerAndHonorsOverrides(bool streaming)
    {
        var variables = new Dictionary<string, string>
        {
            ["CLAUDECODE"] = "nested",
            ["CLAUDE_CODE_ENTRYPOINT"] = "custom-entrypoint",
            ["PWD"] = "incorrect-directory",
            ["OTHER_SETTING"] = "preserved",
        };
        var transport = new CliTransport("claude", [], "work-directory", variables, redirectStdin: streaming);

        var info = transport.CreateStartInfo();

        await Assert.That(info.Environment.ContainsKey("CLAUDECODE")).IsFalse();
        await Assert.That(info.Environment["CLAUDE_CODE_ENTRYPOINT"]).IsEqualTo("custom-entrypoint");
        await Assert.That(info.Environment["PWD"]).IsEqualTo("work-directory");
        await Assert.That(info.WorkingDirectory).IsEqualTo("work-directory");
        await Assert.That(info.Environment["OTHER_SETTING"]).IsEqualTo("preserved");
    }

    [Test]
    public async Task StartInfo_StreamingUsesDefaultEntrypoint()
    {
        var transport = new CliTransport("claude", []);
        var info = transport.CreateStartInfo();

        await Assert.That(info.Environment["CLAUDE_CODE_ENTRYPOINT"]).IsEqualTo("sdk-dotnet");
        await Assert.That(info.Environment.ContainsKey("CLAUDECODE")).IsFalse();
    }
}
