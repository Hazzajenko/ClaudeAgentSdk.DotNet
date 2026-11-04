using ClaudeAgentSdk.Exceptions;

namespace ClaudeAgentSdk.Transport;

/// <summary>
/// Locates the Claude Code CLI binary.
/// Search order: explicit path → CLAUDE_CLI_PATH env → PATH → well-known locations.
/// </summary>
internal static class CliFinder
{
    public static string Find(string? explicitPath = null) =>
        Find(
            explicitPath,
            Environment.GetEnvironmentVariable("CLAUDE_CLI_PATH"),
            Environment.GetEnvironmentVariable("PATH"),
            OperatingSystem.IsWindows(),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    internal static string Find(string? explicitPath, string? envPath, string? pathEnv, bool isWindows, string home)
    {
        if (explicitPath is not null)
        {
            if (File.Exists(explicitPath))
            {
                return explicitPath;
            }
            throw new CliNotFoundException($"Claude Code CLI not found at specified path: '{explicitPath}'")
                { SearchedPath = explicitPath };
        }

        // Check env var
        if (envPath is not null && File.Exists(envPath))
        {
            return envPath;
        }

        // Search PATH
        string? pathResult = FindOnPath(pathEnv, isWindows);
        if (pathResult is not null)
        {
            return pathResult;
        }

        foreach (string path in WellKnownPaths(home, isWindows))
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        if (isWindows)
        {
            throw new CliNotFoundException(
                "Claude Code CLI not found. Install the native claude.exe with (PowerShell):\n" +
                "  irm https://claude.ai/install.ps1 | iex\n" +
                "\nOr set CLAUDE_CLI_PATH environment variable to a claude.exe.\n" +
                "Or provide the path via ClaudeClientOptions { CliPath = @\"C:\\path\\to\\claude.exe\" }\n" +
                "\nThe npm package installs a claude.cmd file, which this SDK refuses to run on Windows.");
        }

        throw new CliNotFoundException(
            "Claude Code CLI not found. Install with:\n" +
            "  npm install -g @anthropic-ai/claude-code\n" +
            "\nOr set CLAUDE_CLI_PATH environment variable.\n" +
            "Or provide the path via ClaudeClientOptions { CliPath = \"/path/to/claude\" }");
    }

    /// <summary>
    /// Fallback locations to probe when the CLI is not on PATH. On Windows only the native
    /// installer's claude.exe is probed. An extensionless file there is not a Windows
    /// executable, and the driveless "/usr/local/bin/claude" resolves to a folder on the
    /// current drive that any local user can create.
    /// </summary>
    internal static string[] WellKnownPaths(string home, bool isWindows) => isWindows
        ? [Path.Combine(home, ".local", "bin", "claude.exe")]
        :
        [
            Path.Combine(home, ".npm-global", "bin", "claude"),
            "/usr/local/bin/claude",
            Path.Combine(home, ".local", "bin", "claude"),
            Path.Combine(home, "node_modules", ".bin", "claude"),
            Path.Combine(home, ".yarn", "bin", "claude"),
            Path.Combine(home, ".claude", "local", "claude"),
        ];

    private static string? FindOnPath(string? pathEnv, bool isWindows)
    {
        string cliName = isWindows ? "claude.exe" : "claude";

        if (pathEnv is null)
        {
            return null;
        }

        char separator = isWindows ? ';' : ':';
        foreach (string dir in pathEnv.Split(separator))
        {
            string fullPath = Path.Combine(dir, cliName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }
}
