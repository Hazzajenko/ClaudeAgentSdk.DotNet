using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ClaudeAgentSdk.Sessions;

/// <summary>
/// Append-only mutations to session JSONL files (rename, tag).
/// Ported from Python SDK session_mutations.py.
/// </summary>
internal static class SessionMutations
{
    public static void Rename(string sessionId, string title, string? directory)
    {
        if (SessionStorage.ValidateUuid(sessionId) is null)
            throw new ArgumentException($"Invalid session_id: {sessionId}", nameof(sessionId));

        string stripped = SanitizeUnicode(title).Trim();
        if (string.IsNullOrEmpty(stripped))
            throw new ArgumentException("title must be non-empty", nameof(title));

        string data = BuildCustomTitleJson(sessionId, stripped);
        AppendToSession(sessionId, data, directory);
    }

    public static void Tag(string sessionId, string? tag, string? directory)
    {
        if (SessionStorage.ValidateUuid(sessionId) is null)
            throw new ArgumentException($"Invalid session_id: {sessionId}", nameof(sessionId));

        if (tag is not null)
        {
            tag = SanitizeUnicode(tag).Trim();
            if (string.IsNullOrEmpty(tag))
                throw new ArgumentException("tag must be non-empty (use null to clear)", nameof(tag));
        }

        string data = BuildTagJson(sessionId, tag ?? "");
        AppendToSession(sessionId, data, directory);
    }

    private static string SanitizeUnicode(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            UnicodeCategory category = char.GetUnicodeCategory(c);
            if (category is UnicodeCategory.Format
                or UnicodeCategory.PrivateUse
                or UnicodeCategory.Surrogate)
            {
                continue;
            }

            if (c is (>= '\u200B' and <= '\u200F')
                or (>= '\u202A' and <= '\u202E')
                or (>= '\u2060' and <= '\u2069')
                or '\uFEFF')
            {
                continue;
            }

            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string BuildCustomTitleJson(string sessionId, string title)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            w.WriteStartObject();
            w.WriteString("type", "custom-title");
            w.WriteString("customTitle", title);
            w.WriteString("sessionId", sessionId);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static string BuildTagJson(string sessionId, string tag)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            w.WriteStartObject();
            w.WriteString("type", "tag");
            w.WriteString("tag", tag);
            w.WriteString("sessionId", sessionId);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static void AppendToSession(string sessionId, string data, string? directory)
    {
        string fileName = $"{sessionId}.jsonl";

        if (directory is not null)
        {
            string canonical = SessionStorage.CanonicalizePath(directory);
            string? projectDir = SessionStorage.FindProjectDir(canonical);
            if (projectDir is not null && TryAppend(Path.Combine(projectDir, fileName), data))
                return;

            throw new FileNotFoundException($"Session {sessionId} not found in project directory for {directory}");
        }

        // No directory — search all project directories
        string projectsDir = SessionStorage.GetProjectsDir();
        if (Directory.Exists(projectsDir))
        {
            foreach (string entry in Directory.GetDirectories(projectsDir))
            {
                if (TryAppend(Path.Combine(entry, fileName), data))
                    return;
            }
        }

        throw new FileNotFoundException($"Session {sessionId} not found in any project directory");
    }

    private static bool TryAppend(string path, string data)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            if (fs.Length == 0) return false;
            fs.Seek(0, SeekOrigin.End);
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            fs.Write(bytes);
            return true;
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }
}
