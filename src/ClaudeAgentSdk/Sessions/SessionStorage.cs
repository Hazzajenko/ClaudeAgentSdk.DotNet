using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClaudeAgentSdk.Sessions;

/// <summary>
/// Reads JSONL session files from ~/.claude/projects/ directories.
/// Ported from Python SDK sessions.py — extracts metadata via stat + head/tail reads.
/// </summary>
internal static partial class SessionStorage
{
    private const int LiteReadBufSize = 65536;
    private const int MaxSanitizedLength = 200;

    [GeneratedRegex(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$", RegexOptions.IgnoreCase)]
    private static partial Regex UuidRegex();

    [GeneratedRegex(@"[^a-zA-Z0-9]")]
    private static partial Regex SanitizeRegex();

    [GeneratedRegex(@"^(?:<local-command-stdout>|<session-start-hook>|<tick>|<goal>|\[Request interrupted by user[^\]]*\]|\s*<ide_opened_file>[\s\S]*</ide_opened_file>\s*$|\s*<ide_selection>[\s\S]*</ide_selection>\s*$)")]
    private static partial Regex SkipFirstPromptRegex();

    [GeneratedRegex(@"<command-name>(.*?)</command-name>")]
    private static partial Regex CommandNameRegex();

    public static string? ValidateUuid(string maybeUuid) =>
        UuidRegex().IsMatch(maybeUuid) ? maybeUuid : null;

    /// <summary>
    /// JS-compatible 32-bit hash, base36. Matches the TS simpleHash function.
    /// </summary>
    public static string SimpleHash(string s)
    {
        int h = 0;
        foreach (char ch in s)
        {
            h = (h << 5) - h + ch;
        }

        long abs = Math.Abs((long)h);
        if (abs == 0) return "0";

        const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
        var sb = new StringBuilder();
        long n = abs;
        while (n > 0)
        {
            sb.Insert(0, digits[(int)(n % 36)]);
            n /= 36;
        }
        return sb.ToString();
    }

    public static string SanitizePath(string name)
    {
        string sanitized = SanitizeRegex().Replace(name, "-");
        if (sanitized.Length <= MaxSanitizedLength)
            return sanitized;
        return $"{sanitized[..MaxSanitizedLength]}-{SimpleHash(name)}";
    }

    public static string GetConfigDir()
    {
        string? configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
        if (!string.IsNullOrEmpty(configDir))
            return Path.GetFullPath(configDir);
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
    }

    public static string GetProjectsDir() => Path.Combine(GetConfigDir(), "projects");

    public static string GetProjectDir(string projectPath) =>
        Path.Combine(GetProjectsDir(), SanitizePath(projectPath));

    public static string CanonicalizePath(string d)
    {
        try { return Path.GetFullPath(d); }
        catch { return d; }
    }

    /// <summary>
    /// Find the project directory, tolerating hash mismatches for long paths.
    /// </summary>
    public static string? FindProjectDir(string projectPath)
    {
        string exact = GetProjectDir(projectPath);
        if (Directory.Exists(exact)) return exact;

        string sanitized = SanitizePath(projectPath);
        if (sanitized.Length <= MaxSanitizedLength) return null;

        string prefix = sanitized[..MaxSanitizedLength];
        string projectsDir = GetProjectsDir();
        if (!Directory.Exists(projectsDir)) return null;

        foreach (string entry in Directory.GetDirectories(projectsDir))
        {
            string dirName = Path.GetFileName(entry);
            if (dirName.StartsWith(prefix + "-", StringComparison.Ordinal))
                return entry;
        }
        return null;
    }

    /// <summary>
    /// Extracts a simple JSON string field value without full parsing.
    /// Looks for "key":"value" or "key": "value" patterns.
    /// </summary>
    public static string? ExtractJsonStringField(ReadOnlySpan<char> text, string key)
    {
        ReadOnlySpan<char> pattern1 = $"\"{key}\":\"";
        ReadOnlySpan<char> pattern2 = $"\"{key}\": \"";

        foreach (var pattern in (ReadOnlySpan<ReadOnlyMemory<char>>)[pattern1.ToString().AsMemory(), pattern2.ToString().AsMemory()])
        {
            int idx = text.IndexOf(pattern.Span);
            if (idx < 0) continue;

            int valueStart = idx + pattern.Length;
            int i = valueStart;
            while (i < text.Length)
            {
                if (text[i] == '\\') { i += 2; continue; }
                if (text[i] == '"')
                    return UnescapeJsonString(text[valueStart..i]);
                i++;
            }
        }
        return null;
    }

    /// <summary>
    /// Like ExtractJsonStringField but finds the LAST occurrence.
    /// </summary>
    public static string? ExtractLastJsonStringField(ReadOnlySpan<char> text, string key)
    {
        string? lastValue = null;
        string[] patterns = [$"\"{key}\":\"", $"\"{key}\": \""];

        foreach (string pattern in patterns)
        {
            int searchFrom = 0;
            while (true)
            {
                int idx = text[searchFrom..].IndexOf(pattern.AsSpan());
                if (idx < 0) break;
                idx += searchFrom;

                int valueStart = idx + pattern.Length;
                int i = valueStart;
                while (i < text.Length)
                {
                    if (text[i] == '\\') { i += 2; continue; }
                    if (text[i] == '"')
                    {
                        lastValue = UnescapeJsonString(text[valueStart..i]);
                        break;
                    }
                    i++;
                }
                searchFrom = i + 1;
            }
        }
        return lastValue;
    }

    private static string UnescapeJsonString(ReadOnlySpan<char> raw)
    {
        if (raw.IndexOf('\\') < 0)
            return raw.ToString();
        try
        {
            using var doc = JsonDocument.Parse($"\"{raw}\"");
            return doc.RootElement.GetString() ?? raw.ToString();
        }
        catch
        {
            return raw.ToString();
        }
    }

    public sealed record LiteSessionFile(long Mtime, long Size, string Head, string Tail);

    /// <summary>
    /// Reads head + tail of a session file (64KB each) plus stat info.
    /// </summary>
    public static LiteSessionFile? ReadSessionLite(string filePath)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long size = fs.Length;
            if (size == 0) return null;

            long mtime = new DateTimeOffset(File.GetLastWriteTimeUtc(filePath)).ToUnixTimeMilliseconds();

            byte[] headBuf = new byte[Math.Min(LiteReadBufSize, size)];
            int headRead = fs.Read(headBuf, 0, headBuf.Length);
            string head = Encoding.UTF8.GetString(headBuf, 0, headRead);

            string tail;
            long tailOffset = Math.Max(0, size - LiteReadBufSize);
            if (tailOffset == 0)
            {
                tail = head;
            }
            else
            {
                fs.Seek(tailOffset, SeekOrigin.Begin);
                byte[] tailBuf = new byte[Math.Min(LiteReadBufSize, size - tailOffset)];
                int tailRead = fs.Read(tailBuf, 0, tailBuf.Length);
                tail = Encoding.UTF8.GetString(tailBuf, 0, tailRead);
            }

            return new LiteSessionFile(mtime, size, head, tail);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts the first meaningful user prompt from a JSONL head chunk.
    /// </summary>
    public static string ExtractFirstPromptFromHead(string head)
    {
        string commandFallback = "";

        foreach (string line in head.Split('\n'))
        {
            if (!line.Contains("\"type\":\"user\"") && !line.Contains("\"type\": \"user\""))
                continue;
            if (line.Contains("\"tool_result\"")) continue;
            if (line.Contains("\"isMeta\":true") || line.Contains("\"isMeta\": true")) continue;
            if (line.Contains("\"isCompactSummary\":true") || line.Contains("\"isCompactSummary\": true")) continue;

            JsonElement entry;
            try { entry = JsonDocument.Parse(line).RootElement; }
            catch { continue; }

            if (entry.ValueKind != JsonValueKind.Object) continue;
            if (!entry.TryGetProperty("type", out var t) || t.GetString() != "user") continue;
            if (!entry.TryGetProperty("message", out var message)) continue;
            if (message.ValueKind != JsonValueKind.Object) continue;
            if (!message.TryGetProperty("content", out var content)) continue;

            var texts = new List<string>();
            if (content.ValueKind == JsonValueKind.String)
            {
                texts.Add(content.GetString()!);
            }
            else if (content.ValueKind == JsonValueKind.Array)
            {
                foreach (var block in content.EnumerateArray())
                {
                    if (block.TryGetProperty("type", out var bt) && bt.GetString() == "text"
                        && block.TryGetProperty("text", out var tt))
                    {
                        texts.Add(tt.GetString()!);
                    }
                }
            }

            foreach (string raw in texts)
            {
                string result = raw.Replace('\n', ' ').Trim();
                if (string.IsNullOrEmpty(result)) continue;

                var cmdMatch = CommandNameRegex().Match(result);
                if (cmdMatch.Success)
                {
                    if (string.IsNullOrEmpty(commandFallback))
                        commandFallback = cmdMatch.Groups[1].Value;
                    continue;
                }

                if (SkipFirstPromptRegex().IsMatch(result)) continue;

                if (result.Length > 200)
                    result = result[..200].TrimEnd() + "\u2026";
                return result;
            }
        }

        return commandFallback;
    }

    /// <summary>
    /// Parses SessionInfo fields from a lite session read (head/tail/stat).
    /// </summary>
    public static SessionInfo? ParseSessionInfoFromLite(string sessionId, LiteSessionFile lite, string? projectPath = null)
    {
        string head = lite.Head, tail = lite.Tail;

        // Check first line for sidechain sessions
        int firstNewline = head.IndexOf('\n');
        string firstLine = firstNewline >= 0 ? head[..firstNewline] : head;
        if (firstLine.Contains("\"isSidechain\":true") || firstLine.Contains("\"isSidechain\": true"))
            return null;

        string? customTitle =
            ExtractLastJsonStringField(tail, "customTitle")
            ?? ExtractLastJsonStringField(head, "customTitle")
            ?? ExtractLastJsonStringField(tail, "aiTitle")
            ?? ExtractLastJsonStringField(head, "aiTitle");

        string? firstPrompt = ExtractFirstPromptFromHead(head);
        if (string.IsNullOrEmpty(firstPrompt)) firstPrompt = null;

        string? summary =
            customTitle
            ?? ExtractLastJsonStringField(tail, "lastPrompt")
            ?? ExtractLastJsonStringField(tail, "summary")
            ?? firstPrompt;

        if (summary is null) return null;

        string? gitBranch =
            ExtractLastJsonStringField(tail, "gitBranch")
            ?? ExtractJsonStringField(head, "gitBranch");

        string? sessionCwd = ExtractJsonStringField(head, "cwd") ?? projectPath;

        // Scope tag extraction to {"type":"tag"} lines
        string? tag = null;
        string[] tailLines = tail.Split('\n');
        for (int i = tailLines.Length - 1; i >= 0; i--)
        {
            if (tailLines[i].StartsWith("{\"type\":\"tag\"", StringComparison.Ordinal))
            {
                tag = ExtractLastJsonStringField(tailLines[i], "tag");
                break;
            }
        }

        // created_at from first entry's ISO timestamp
        long? createdAt = null;
        string? firstTimestamp = ExtractJsonStringField(firstLine, "timestamp");
        if (firstTimestamp is not null)
        {
            try
            {
                string ts = firstTimestamp.EndsWith('Z')
                    ? firstTimestamp[..^1] + "+00:00"
                    : firstTimestamp;
                createdAt = (long)DateTimeOffset.Parse(ts, CultureInfo.InvariantCulture).ToUnixTimeMilliseconds();
            }
            catch { }
        }

        return new SessionInfo
        {
            SessionId = sessionId,
            Summary = summary,
            LastModified = lite.Mtime,
            FileSize = lite.Size,
            CustomTitle = customTitle,
            FirstPrompt = firstPrompt,
            GitBranch = gitBranch,
            Cwd = sessionCwd,
            Tag = string.IsNullOrEmpty(tag) ? null : tag,
            CreatedAt = createdAt
        };
    }

    /// <summary>
    /// Reads session files from a single project directory.
    /// </summary>
    public static IReadOnlyList<SessionInfo> ReadSessionsFromDir(string projectDir, string? projectPath = null)
    {
        if (!Directory.Exists(projectDir)) return [];

        var results = new List<SessionInfo>();

        foreach (string filePath in Directory.GetFiles(projectDir, "*.jsonl"))
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string? sessionId = ValidateUuid(fileName);
            if (sessionId is null) continue;

            var lite = ReadSessionLite(filePath);
            if (lite is null) continue;

            var info = ParseSessionInfoFromLite(sessionId, lite, projectPath);
            if (info is not null) results.Add(info);
        }

        return results;
    }
}
