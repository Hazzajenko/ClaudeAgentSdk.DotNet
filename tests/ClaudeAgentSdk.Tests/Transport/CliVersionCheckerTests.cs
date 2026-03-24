using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk.Tests.Transport;

public class CliVersionCheckerTests
{
    [Test]
    [Arguments("2.0.0", "2.0.0", true)]
    [Arguments("2.1.0", "2.0.0", true)]
    [Arguments("3.0.0", "2.0.0", true)]
    [Arguments("2.0.1", "2.0.0", true)]
    [Arguments("1.9.9", "2.0.0", false)]
    [Arguments("1.0.0", "2.0.0", false)]
    [Arguments("0.99.0", "2.0.0", false)]
    public async Task IsVersionSufficient_ComparesCorrectly(string version, string minimum, bool expected)
    {
        bool result = CliVersionChecker.IsVersionSufficient(version, minimum);
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    [Arguments("claude 2.1.0", "2.1.0")]
    [Arguments("2.0.0", "2.0.0")]
    [Arguments("claude v3.0.0", "3.0.0")]
    [Arguments("Claude Code CLI v2.5.1\nSome other text", "2.5.1")]
    public async Task ParseVersion_ExtractsVersionFromOutput(string output, string expected)
    {
        string? version = CliVersionChecker.ParseVersion(output);
        await Assert.That(version).IsEqualTo(expected);
    }

    [Test]
    public async Task ParseVersion_InvalidOutput_ReturnsNull()
    {
        string? version = CliVersionChecker.ParseVersion("no version here");
        await Assert.That(version).IsNull();
    }
}
