using System.Text.Json;

namespace ClaudeAgentSdk.Sessions;

/// <summary>
/// Reads subagent transcripts from <c>&lt;projectDir&gt;/&lt;sessionId&gt;/subagents/</c>.
/// Ported from Python SDK sessions.py <c>list_subagents</c> and <c>get_subagent_messages</c>.
/// </summary>
internal static class SubagentTranscripts
{
    private const string AgentPrefix = "agent-";
    private const string TranscriptSuffix = ".jsonl";

    public static IReadOnlyList<string> List(string sessionId, string? directory)
    {
        if (SessionStorage.ValidateUuid(sessionId) is null) return [];
        if (ResolveSubagentsDir(sessionId, directory) is not { } subagentsDir) return [];

        return CollectAgentFiles(subagentsDir).Select(f => f.AgentId).ToList();
    }

    public static IReadOnlyList<SessionMessage> GetMessages(
        string sessionId, string agentId, string? directory, int? limit, int offset)
    {
        if (SessionStorage.ValidateUuid(sessionId) is null) return [];
        if (string.IsNullOrEmpty(agentId)) return [];
        if (ResolveSubagentsDir(sessionId, directory) is not { } subagentsDir) return [];

        string? match = null;
        foreach (var (foundId, filePath) in CollectAgentFiles(subagentsDir))
        {
            if (foundId == agentId)
            {
                match = filePath;
                break;
            }
        }
        if (match is null) return [];

        string content;
        try
        {
            content = File.ReadAllText(match);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
        if (content.Length == 0) return [];

        var (parentToolUseId, parentAgentId) = ReadParentIds(match);

        var entries = SessionMessageParser.ParseTranscriptEntries(content);
        var chain = SessionMessageParser.BuildSubagentChain(entries);
        return SessionMessageParser.ToSubagentMessages(chain, limit, offset, parentToolUseId, parentAgentId);
    }

    /// <summary>
    /// The subagents directory sits beside the session file:
    /// <c>&lt;projectDir&gt;/&lt;sessionId&gt;/subagents</c>. Null when the session is not found.
    /// </summary>
    private static string? ResolveSubagentsDir(string sessionId, string? directory) =>
        SessionStorage.FindSessionFile(sessionId, directory) is var (_, projectDir)
            ? Path.Combine(projectDir, sessionId, "subagents")
            : null;

    /// <summary>
    /// Collects <c>agent-*.jsonl</c> files, also in nested folders such as
    /// <c>workflows/&lt;runId&gt;/</c>. Entries in each folder go in ordinal name order.
    /// </summary>
    private static List<(string AgentId, string FilePath)> CollectAgentFiles(string baseDir)
    {
        var results = new List<(string, string)>();
        Walk(baseDir, results);
        return results;
    }

    private static void Walk(string dir, List<(string, string)> results)
    {
        string[] entries;
        try
        {
            entries = Directory.GetFileSystemEntries(dir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        Array.Sort(entries, (a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));
        foreach (string entry in entries)
        {
            string name = Path.GetFileName(entry);
            if (File.Exists(entry))
            {
                if (name.StartsWith(AgentPrefix, StringComparison.Ordinal)
                    && name.EndsWith(TranscriptSuffix, StringComparison.Ordinal)
                    && name.Length >= AgentPrefix.Length + TranscriptSuffix.Length)
                {
                    results.Add((name[AgentPrefix.Length..^TranscriptSuffix.Length], entry));
                }
            }
            else if (Directory.Exists(entry))
            {
                Walk(entry, results);
            }
        }
    }

    /// <summary>
    /// Reads <c>toolUseId</c> and <c>parentAgentId</c> from the <c>agent-&lt;id&gt;.meta.json</c>
    /// file beside the transcript. A missing, unreadable or invalid file gives nulls.
    /// </summary>
    private static (string? ToolUseId, string? ParentAgentId) ReadParentIds(string transcriptPath)
    {
        string metaPath = transcriptPath[..^TranscriptSuffix.Length] + ".meta.json";
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(metaPath));
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return (null, null);
            return (StringProperty(root, "toolUseId"), StringProperty(root, "parentAgentId"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return (null, null);
        }
    }

    private static string? StringProperty(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
