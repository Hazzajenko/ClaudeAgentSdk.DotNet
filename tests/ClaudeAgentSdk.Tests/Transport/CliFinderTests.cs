using ClaudeAgentSdk.Exceptions;
using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk.Tests.Transport;

public class CliFinderTests
{
    private const string Home = @"C:\Users\someone";

    private static string NoSuchHome() => Path.Combine(Path.GetTempPath(), "cli-finder-" + Guid.NewGuid().ToString("N"));

    [Test]
    [Arguments(@"C:\npm\claude.cmd")]
    [Arguments(@"C:\npm\claude.bat")]
    [Arguments(@"C:\npm\claude.cmd.")]
    [Arguments(@"C:\npm\claude.CMD ")]
    [Arguments(@"C:\npm\claude.Bat. . ")]
    public async Task Find_BatchFileFromCliPath_OnWindows_Throws(string path)
    {
        var ex = await Assert.That(
            () => CliFinder.Find(path, cliPathVariable: null, pathVariable: null, isWindows: true, home: Home))
            .Throws<CliConnectionException>();

        await Assert.That(ex!.Message).Contains("batch");
    }

    [Test]
    [Arguments(@"C:\npm\claude.cmd")]
    [Arguments(@"C:\npm\claude.CMD ")]
    [Arguments(@"C:\npm\claude.cmd\x\..")]
    [Arguments(@"C:\npm\claude.cmd:stream")]
    public async Task Find_BatchFileFromEnvVar_OnWindows_Throws(string path)
    {
        await Assert.That(
            () => CliFinder.Find(explicitPath: null, cliPathVariable: path, pathVariable: null, isWindows: true, home: Home))
            .Throws<CliConnectionException>();
    }

    [Test]
    public async Task WellKnownPaths_OnWindows_IsOnlyLocalBinClaudeExe()
    {
        string[] paths = CliFinder.WellKnownPaths(Home, isWindows: true);

        await Assert.That(paths).IsEquivalentTo([Path.Combine(Home, ".local", "bin", "claude.exe")]);
    }

    [Test]
    public async Task WellKnownPaths_OffWindows_KeepsUnixLocations()
    {
        string[] paths = CliFinder.WellKnownPaths("/home/someone", isWindows: false);

        await Assert.That(paths).Contains("/usr/local/bin/claude");
        await Assert.That(paths).Contains(Path.Combine("/home/someone", ".local", "bin", "claude"));
    }

    [Test]
    public async Task Find_NotFound_OnWindows_DoesNotRecommendNpm()
    {
        var ex = await Assert.That(
            () => CliFinder.Find(explicitPath: null, cliPathVariable: null, pathVariable: null, isWindows: true, home: NoSuchHome()))
            .Throws<CliNotFoundException>();

        await Assert.That(ex!.Message).DoesNotContain("npm install");
        await Assert.That(ex.Message).Contains("claude.exe");
    }

    [Test]
    public async Task Find_CmdPath_OffWindows_IsNotRefused()
    {
        await Assert.That(
            () => CliFinder.Find(@"/nowhere/claude.cmd", cliPathVariable: null, pathVariable: null, isWindows: false, home: Home))
            .Throws<CliNotFoundException>();
    }
}
