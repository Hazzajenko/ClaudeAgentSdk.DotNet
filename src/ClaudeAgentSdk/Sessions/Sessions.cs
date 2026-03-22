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
    public static IReadOnlyList<SessionInfo> List(string? directory = null, int? limit = null)
    {
        if (directory is not null)
            return ListForProject(directory, limit);
        return ListAllSessions(limit);
    }

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
    /// Rename a session by appending a custom-title entry.
    /// </summary>
    public static void Rename(string sessionId, string title, string? directory = null) =>
        SessionMutations.Rename(sessionId, title, directory);

    /// <summary>
    /// Tag a session. Pass null to clear the tag.
    /// </summary>
    public static void Tag(string sessionId, string? tag, string? directory = null) =>
        SessionMutations.Tag(sessionId, tag, directory);

    private static IReadOnlyList<SessionInfo> ListForProject(string directory, int? limit)
    {
        string canonical = SessionStorage.CanonicalizePath(directory);
        string? projectDir = SessionStorage.FindProjectDir(canonical);
        if (projectDir is null) return [];

        var sessions = new List<SessionInfo>(SessionStorage.ReadSessionsFromDir(projectDir, canonical));
        sessions.Sort((a, b) => b.LastModified.CompareTo(a.LastModified));

        if (limit is > 0)
            return sessions.GetRange(0, Math.Min(limit.Value, sessions.Count));
        return sessions;
    }

    private static IReadOnlyList<SessionInfo> ListAllSessions(int? limit)
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

        var result = new List<SessionInfo>(byId.Values);
        result.Sort((a, b) => b.LastModified.CompareTo(a.LastModified));

        if (limit is > 0)
            return result.GetRange(0, Math.Min(limit.Value, result.Count));
        return result;
    }
}
