using ClaudeAgentSdk.Exceptions;

namespace ClaudeAgentSdk.Transport;

/// <summary>
/// Locates the Claude Code CLI binary.
/// Search order: explicit path → CLAUDE_CLI_PATH env → PATH → well-known locations.
/// </summary>
internal static class CliFinder
{
    public static string Find(string? explicitPath = null)
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
        string? envPath = Environment.GetEnvironmentVariable("CLAUDE_CLI_PATH");
        if (envPath is not null && File.Exists(envPath))
        {
            return envPath;
        }

        // Search PATH
        string? pathResult = FindOnPath();
        if (pathResult is not null)
        {
            return pathResult;
        }

        // Check well-known locations
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] wellKnownPaths =
        [
            Path.Combine(home, ".npm-global", "bin", "claude"),
            "/usr/local/bin/claude",
            Path.Combine(home, ".local", "bin", "claude"),
            Path.Combine(home, "node_modules", ".bin", "claude"),
            Path.Combine(home, ".yarn", "bin", "claude"),
            Path.Combine(home, ".claude", "local", "claude"),
        ];

        foreach (string path in wellKnownPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new CliNotFoundException(
            "Claude Code CLI not found. Install with:\n" +
            "  npm install -g @anthropic-ai/claude-code\n" +
            "\nOr set CLAUDE_CLI_PATH environment variable.\n" +
            "Or provide the path via ClaudeClientOptions { CliPath = \"/path/to/claude\" }");
    }

    private static string? FindOnPath()
    {
        string cliName = OperatingSystem.IsWindows() ? "claude.exe" : "claude";
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");

        if (pathEnv is null)
        {
            return null;
        }

        char separator = OperatingSystem.IsWindows() ? ';' : ':';
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
