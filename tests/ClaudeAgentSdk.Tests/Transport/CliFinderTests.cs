using ClaudeAgentSdk.Exceptions;
using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk.Tests.Transport;

public class CliFinderTests
{
    private const string Home = @"C:\Users\someone";

    private static string NoSuchHome() => Path.Combine(Path.GetTempPath(), "cli-finder-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Makes an empty file with the given name in a new temp folder. On Windows the OS trims
    /// trailing dots and spaces from the name, and the untrimmed path still opens the file.
    /// </summary>
    private sealed class TempFile : IDisposable
    {
        private readonly string _folder = NoSuchHome();

        public TempFile(string name)
        {
            Directory.CreateDirectory(_folder);
            Path = System.IO.Path.Combine(_folder, name);
            File.WriteAllText(Path, "");
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(_folder, recursive: true);
    }

    [Test]
    [Arguments("claude.cmd")]
    [Arguments("claude.bat")]
    [Arguments("claude.cmd.")]
    [Arguments("claude.CMD ")]
    [Arguments("claude.Bat. . ")]
    public async Task Find_BatchFileFromCliPath_OnWindows_Throws(string name)
    {
        using var file = new TempFile(name);

        var ex = await Assert.That(
            () => CliFinder.Find(file.Path, cliPathVariable: null, pathVariable: null, isWindows: true, home: Home))
            .Throws<CliConnectionException>();

        await Assert.That(ex!.Message).Contains("batch");
    }

    [Test]
    [Arguments("claude.cmd")]
    [Arguments("claude.cmd.")]
    [Arguments("claude.CMD ")]
    public async Task Find_BatchFileFromEnvVar_OnWindows_Throws(string name)
    {
        using var file = new TempFile(name);

        await Assert.That(
            () => CliFinder.Find(explicitPath: null, cliPathVariable: file.Path, pathVariable: null, isWindows: true, home: Home))
            .Throws<CliConnectionException>();
    }

    [Test]
    [Arguments(@"C:\npm\claude.cmd\x\..", true)]
    [Arguments(@"C:\npm\claude.cmd:stream", true)]
    [Arguments(@"C:\npm\claude:x.cmd", true)]
    [Arguments(@"C:\tools.cmd\claude.exe", true)]
    [Arguments(@"C:\npm\claude.exe", false)]
    [Arguments(@"C:\cmd\claude.exe", false)]
    public async Task IsWindowsBatchFile_ChecksEveryPathComponent(string path, bool expected)
    {
        await Assert.That(CliFinder.IsWindowsBatchFile(path)).IsEqualTo(expected);
    }

    [Test]
    public async Task Find_MissingBatchFileFromCliPath_OnWindows_ThrowsNotFoundWithSearchedPath()
    {
        string path = Path.Combine(NoSuchHome(), "claude.cmd");

        var ex = await Assert.That(
            () => CliFinder.Find(path, cliPathVariable: null, pathVariable: null, isWindows: true, home: NoSuchHome()))
            .Throws<CliNotFoundException>();

        await Assert.That(ex!.SearchedPath).IsEqualTo(path);
    }

    [Test]
    public async Task Find_MissingBatchFileFromEnvVar_OnWindows_FallsThroughToSearch()
    {
        string path = Path.Combine(NoSuchHome(), "claude.cmd");

        await Assert.That(
            () => CliFinder.Find(explicitPath: null, cliPathVariable: path, pathVariable: null, isWindows: true, home: NoSuchHome()))
            .Throws<CliNotFoundException>();
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
        using var file = new TempFile("claude.cmd");

        string found = CliFinder.Find(file.Path, cliPathVariable: null, pathVariable: null, isWindows: false, home: Home);

        await Assert.That(found).IsEqualTo(file.Path);
    }
}
