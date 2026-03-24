using System.Diagnostics;
using System.Text.RegularExpressions;
using ClaudeAgentSdk.Exceptions;

namespace ClaudeAgentSdk.Transport;

/// <summary>
/// Checks that the Claude CLI meets the minimum required version.
/// </summary>
internal static partial class CliVersionChecker
{
    internal const string MinimumVersion = "2.0.0";

    /// <summary>
    /// Run <c>claude --version</c> and verify it meets the minimum.
    /// Skipped when <c>CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK=1</c>.
    /// </summary>
    public static async Task CheckAsync(string cliPath, CancellationToken cancellationToken)
    {
        if (Environment.GetEnvironmentVariable("CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK") == "1")
            return;

        string output;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = cliPath,
                ArgumentList = { "--version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo)
                ?? throw new CliNotFoundException($"Failed to start '{cliPath}' for version check");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            output = await process.StandardOutput.ReadToEndAsync(cts.Token);
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            return; // Don't block startup if version check times out
        }
        catch (Exception ex) when (ex is not CliNotFoundException)
        {
            throw new CliNotFoundException(
                $"Claude Code CLI not found at '{cliPath}'. Install with: npm install -g @anthropic-ai/claude-code",
                ex);
        }

        string? version = ParseVersion(output);
        if (version is null)
            return; // Can't parse — don't block

        if (!IsVersionSufficient(version, MinimumVersion))
        {
            throw new CliVersionException(
                $"Claude Code CLI version {version} is below minimum required {MinimumVersion}. " +
                "Update with: npm install -g @anthropic-ai/claude-code");
        }
    }

    /// <summary>
    /// Parse a semver version string from CLI output.
    /// </summary>
    internal static string? ParseVersion(string output)
    {
        var match = VersionPattern().Match(output);
        return match.Success ? match.Value : null;
    }

    /// <summary>
    /// Compare two semver version strings (major.minor.patch).
    /// </summary>
    internal static bool IsVersionSufficient(string version, string minimum)
    {
        var vParts = version.Split('.').Select(int.Parse).ToArray();
        var mParts = minimum.Split('.').Select(int.Parse).ToArray();

        for (int i = 0; i < 3; i++)
        {
            int v = i < vParts.Length ? vParts[i] : 0;
            int m = i < mParts.Length ? mParts[i] : 0;
            if (v > m) return true;
            if (v < m) return false;
        }
        return true; // Equal
    }

    [GeneratedRegex(@"\d+\.\d+\.\d+")]
    private static partial Regex VersionPattern();
}
