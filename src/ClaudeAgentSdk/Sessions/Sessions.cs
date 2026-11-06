using System.ComponentModel;

namespace ClaudeAgentSdk.Sessions;

/// <summary>
/// Session management utilities. Reads session data from ~/.claude/projects/ JSONL files.
/// </summary>
public static class Sessions
{
    /// <summary>
    /// List sessions sorted by last modified (newest first).
    /// When directory is provided, returns sessions for that project. Otherwise all projects.
    /// </summary>
    /// <param name="directory">Project directory. When null, sessions from all projects are listed.</param>
    /// <param name="limit">Maximum number of sessions to return. Null or 0 returns all.</param>
    /// <param name="offset">Number of sessions to skip from the start of the sorted list.</param>
    public static IReadOnlyList<SessionInfo> List(string? directory = null, int? limit = null, int offset = 0)
    {
        var sessions = directory is not null ? ListForProject(directory) : ListAllSessions();
        sessions.Sort((a, b) => b.LastModified.CompareTo(a.LastModified));

        IEnumerable<SessionInfo> page = sessions;
        if (offset > 0)
            page = page.Skip(offset);
        if (limit is > 0)
            page = page.Take(limit.Value);
        return page.ToList();
    }

    /// <summary>
    /// List sessions sorted by last modified (newest first). Kept so that code
    /// compiled against the overload without <c>offset</c> still runs.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IReadOnlyList<SessionInfo> List(string? directory, int? limit) =>
        List(directory, limit, offset: 0);

    /// <summary>
    /// Get metadata for a single session by ID.
    /// </summary>
    public static SessionInfo? GetInfo(string sessionId, string? directory = null)
    {
        string? uuid = SessionStorage.ValidateUuid(sessionId);
        if (uuid is null) return null;

        string fileName = $"{uuid}.jsonl";

        if (directory is not null)
        {
            string canonical = SessionStorage.CanonicalizePath(directory);
            string? projectDir = SessionStorage.FindProjectDir(canonical);
            if (projectDir is not null)
            {
                string filePath = Path.Combine(projectDir, fileName);
                var lite = SessionStorage.ReadSessionLite(filePath);
                if (lite is not null)
                    return SessionStorage.ParseSessionInfoFromLite(uuid, lite, canonical);
            }
            return null;
        }

        // No directory — search all project directories
        string projectsDir = SessionStorage.GetProjectsDir();
        if (!Directory.Exists(projectsDir)) return null;

        foreach (string entry in Directory.GetDirectories(projectsDir))
        {
            string filePath = Path.Combine(entry, fileName);
            var lite = SessionStorage.ReadSessionLite(filePath);
            if (lite is not null)
                return SessionStorage.ParseSessionInfoFromLite(uuid, lite);
        }
        return null;
    }

    /// <summary>
    /// Get conversation messages for a session in chronological order.
    /// </summary>
    public static IReadOnlyList<SessionMessage> GetMessages(
        string sessionId, string? directory = null, int? limit = null, int offset = 0)
    {
        if (SessionStorage.ValidateUuid(sessionId) is null) return [];

        string? content = ReadSessionFile(sessionId, directory);
        if (content is null) return [];

        var entries = SessionMessageParser.ParseTranscriptEntries(content);
        var chain = SessionMessageParser.BuildConversationChain(entries);
        return SessionMessageParser.ToSessionMessages(chain, limit, offset);
    }

    /// <summary>
    /// List the subagent ids of a session. Subagent transcripts are at
    /// <c>&lt;projectDir&gt;/&lt;sessionId&gt;/subagents/agent-&lt;agentId&gt;.jsonl</c>,
    /// and can be in nested folders such as <c>workflows/&lt;runId&gt;/</c>.
    /// </summary>
    /// <returns>
    /// The agent ids. Empty when <paramref name="sessionId"/> is not a valid UUID,
    /// the session is not found, or the session has no subagents.
    /// </returns>
    public static IReadOnlyList<string> ListSubagents(string sessionId, string? directory = null) =>
        SubagentTranscripts.List(sessionId, directory);

    /// <summary>
    /// Get the messages of a subagent transcript in chronological order. Each message has
    /// <see cref="SessionMessage.ParentToolUseId"/> and <see cref="SessionMessage.ParentAgentId"/>
    /// from the <c>agent-&lt;agentId&gt;.meta.json</c> file beside the transcript.
    /// </summary>
    /// <param name="sessionId">UUID of the parent session.</param>
    /// <param name="agentId">Subagent id, as <see cref="ListSubagents"/> returns it.</param>
    /// <param name="directory">Project directory. When null, all project directories are searched.</param>
    /// <param name="limit">Maximum number of messages to return. Null or 0 returns all.</param>
    /// <param name="offset">Number of messages to skip from the start.</param>
    /// <returns>The messages. Empty when the session or the subagent is not found.</returns>
    public static IReadOnlyList<SessionMessage> GetSubagentMessages(
        string sessionId, string agentId, string? directory = null, int? limit = null, int offset = 0) =>
        SubagentTranscripts.GetMessages(sessionId, agentId, directory, limit, offset);

    /// <summary>
    /// Rename a session by appending a custom-title entry.
    /// </summary>
    public static void Rename(string sessionId, string title, string? directory = null) =>
        SessionMutations.Rename(sessionId, title, directory);

    /// <summary>
    /// Tag a session. Pass null to clear the tag.
    /// </summary>
    public static void Tag(string sessionId, string? tag, string? directory = null) =>
        SessionMutations.Tag(sessionId, tag, directory);

    /// <summary>
    /// Delete a session. This removes the session JSONL file and the sibling
    /// <c>&lt;sessionId&gt;/</c> directory, which holds the subagent transcripts.
    /// The delete is permanent. For a soft delete, tag the session and filter on the tag.
    /// </summary>
    /// <param name="sessionId">UUID of the session to delete.</param>
    /// <param name="directory">Project directory. When null, all project directories are searched.</param>
    /// <exception cref="ArgumentException"><paramref name="sessionId"/> is not a valid UUID.</exception>
    /// <exception cref="FileNotFoundException">No non-empty session file was found.</exception>
    public static void Delete(string sessionId, string? directory = null) =>
        SessionMutations.Delete(sessionId, directory);

    private static string? ReadSessionFile(string sessionId, string? directory)
    {
        string fileName = $"{sessionId}.jsonl";

        if (directory is not null)
        {
            string canonical = SessionStorage.CanonicalizePath(directory);
            string? projectDir = SessionStorage.FindProjectDir(canonical);
            if (projectDir is not null)
            {
                string path = Path.Combine(projectDir, fileName);
                if (File.Exists(path))
                    return File.ReadAllText(path);
            }
            return null;
        }

        string projectsDir = SessionStorage.GetProjectsDir();
        if (!Directory.Exists(projectsDir)) return null;

        foreach (string entry in Directory.GetDirectories(projectsDir))
        {
            string path = Path.Combine(entry, fileName);
            if (File.Exists(path))
                return File.ReadAllText(path);
        }
        return null;
    }

    private static List<SessionInfo> ListForProject(string directory)
    {
        string canonical = SessionStorage.CanonicalizePath(directory);
        string? projectDir = SessionStorage.FindProjectDir(canonical);
        if (projectDir is null) return [];

        return [.. SessionStorage.ReadSessionsFromDir(projectDir, canonical)];
    }

    private static List<SessionInfo> ListAllSessions()
    {
        string projectsDir = SessionStorage.GetProjectsDir();
        if (!Directory.Exists(projectsDir)) return [];

        var all = new List<SessionInfo>();
        foreach (string projectDir in Directory.GetDirectories(projectsDir))
        {
            all.AddRange(SessionStorage.ReadSessionsFromDir(projectDir));
        }

        // Deduplicate by session ID (keep newest)
        var byId = new Dictionary<string, SessionInfo>();
        foreach (var s in all)
        {
            if (!byId.TryGetValue(s.SessionId, out var existing) || s.LastModified > existing.LastModified)
                byId[s.SessionId] = s;
        }

        return [.. byId.Values];
    }
}
