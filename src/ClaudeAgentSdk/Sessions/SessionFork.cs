using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ClaudeAgentSdk.Sessions;

/// <summary>
/// Copies a session into a new session file with new UUIDs.
/// Ported from Python SDK session_mutations.py <c>fork_session</c> and <c>_build_fork_lines</c>.
/// </summary>
internal static class SessionFork
{
    private static readonly HashSet<string> TranscriptTypes = ["user", "assistant", "attachment", "system", "progress"];

    /// <summary>Fields that would carry state from the source session into the fork.</summary>
    private static readonly HashSet<string> RemovedFields = ["teamName", "agentName", "slug", "sourceToolAssistantUUID"];

    /// <summary>Order of the fields that the fork sets on each message.</summary>
    private static readonly string[] OverrideOrder =
        ["uuid", "parentUuid", "logicalParentUuid", "sessionId", "timestamp", "isSidechain", "forkedFrom"];

    public static ForkSessionResult Fork(string sessionId, string? directory, string? upToMessageId, string? title)
    {
        if (SessionStorage.ValidateUuid(sessionId) is null)
            throw new ArgumentException($"Invalid session_id: {sessionId}", nameof(sessionId));
        if (!string.IsNullOrEmpty(upToMessageId) && SessionStorage.ValidateUuid(upToMessageId) is null)
            throw new ArgumentException($"Invalid up_to_message_id: {upToMessageId}", nameof(upToMessageId));

        if (SessionStorage.FindSessionFile(sessionId, directory) is not var (filePath, projectDir))
        {
            throw new FileNotFoundException(directory is null
                ? $"Session {sessionId} not found"
                : $"Session {sessionId} not found in project directory for {directory}");
        }

        byte[] content = File.ReadAllBytes(filePath);
        if (content.Length == 0)
            throw NoMessages(sessionId);

        var (transcript, replacements) = ParseForkTranscript(content, sessionId);
        var (forkedSessionId, lines) = BuildForkLines(
            transcript, replacements, sessionId, upToMessageId, title, () => DeriveTitle(content));

        string forkPath = Path.Combine(projectDir, $"{forkedSessionId}.jsonl");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using (var fs = new FileStream(forkPath, options))
        {
            fs.Write(Encoding.UTF8.GetBytes(string.Join("\n", lines) + "\n"));
        }

        return new ForkSessionResult { SessionId = forkedSessionId };
    }

    /// <summary>
    /// Transcript entries that have a string uuid, plus the replacements of each
    /// content-replacement entry of this session.
    /// </summary>
    private static (List<JsonElement> Transcript, List<JsonElement> Replacements) ParseForkTranscript(
        byte[] content, string sessionId)
    {
        var transcript = new List<JsonElement>();
        var replacements = new List<JsonElement>();

        foreach (string line in Encoding.UTF8.GetString(content).Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0) continue;

            JsonElement entry;
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                entry = doc.RootElement.Clone();
            }
            catch (JsonException) { continue; }
            if (entry.ValueKind != JsonValueKind.Object) continue;

            string? type = GetString(entry, "type");
            if (type is not null && TranscriptTypes.Contains(type) && GetString(entry, "uuid") is not null)
            {
                transcript.Add(entry);
            }
            else if (type == "content-replacement"
                && GetString(entry, "sessionId") == sessionId
                && entry.TryGetProperty("replacements", out var list)
                && list.ValueKind == JsonValueKind.Array)
            {
                replacements.AddRange(list.EnumerateArray());
            }
        }

        return (transcript, replacements);
    }

    /// <summary>
    /// The fork transform. Each uuid gets a new value and <c>parentUuid</c> links
    /// follow the new values. Sidechain and progress entries are not written.
    /// </summary>
    private static (string ForkedSessionId, List<string> Lines) BuildForkLines(
        List<JsonElement> transcript,
        List<JsonElement> replacements,
        string sessionId,
        string? upToMessageId,
        string? title,
        Func<string?> deriveTitle)
    {
        // Sidechains are subagent entries with their own parentUuid graph.
        // Meta entries stay, because they are part of the main chain.
        transcript = transcript.Where(e => !IsTruthy(e, "isSidechain")).ToList();
        if (transcript.Count == 0)
            throw NoMessages(sessionId);

        if (!string.IsNullOrEmpty(upToMessageId))
        {
            int cutoff = transcript.FindIndex(e => GetString(e, "uuid") == upToMessageId);
            if (cutoff == -1)
            {
                throw new ArgumentException(
                    $"Message {upToMessageId} not found in session {sessionId}", nameof(upToMessageId));
            }
            transcript = transcript.GetRange(0, cutoff + 1);
        }

        // Progress entries are in the map, because the parentUuid walk goes through them.
        var uuidMapping = new Dictionary<string, string>();
        var byUuid = new Dictionary<string, JsonElement>();
        foreach (var entry in transcript)
        {
            string uuid = GetString(entry, "uuid")!;
            uuidMapping[uuid] = NewUuid();
            byUuid[uuid] = entry;
        }

        // Progress entries are UI-only chain links. A new fork does not need them.
        var writable = transcript.Where(e => GetString(e, "type") != "progress").ToList();
        if (writable.Count == 0)
            throw NoMessages(sessionId);

        string forkedSessionId = NewUuid();
        string now = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);
        var lines = new List<string>(writable.Count + 2);

        for (int i = 0; i < writable.Count; i++)
        {
            JsonElement original = writable[i];
            string originalUuid = GetString(original, "uuid")!;

            // Resolve parentUuid and skip progress ancestors.
            string? newParentUuid = null;
            string? parentId = GetString(original, "parentUuid");
            while (!string.IsNullOrEmpty(parentId))
            {
                if (!byUuid.TryGetValue(parentId, out var parent)) break;
                if (GetString(parent, "type") != "progress")
                {
                    newParentUuid = uuidMapping[parentId];
                    break;
                }
                parentId = GetString(parent, "parentUuid");
            }

            // Only the last message gets a new timestamp, so resume picks it as the leaf.
            bool isLast = i == writable.Count - 1;
            JsonElement? keptTimestamp = !isLast && original.TryGetProperty("timestamp", out var ts) ? ts : null;

            var overrides = new Dictionary<string, Action<Utf8JsonWriter>>
            {
                ["uuid"] = w => w.WriteStringValue(uuidMapping[originalUuid]),
                ["parentUuid"] = w => WriteStringOrNull(w, newParentUuid),
                ["logicalParentUuid"] = w => WriteLogicalParent(w, original, uuidMapping),
                ["sessionId"] = w => w.WriteStringValue(forkedSessionId),
                ["timestamp"] = w =>
                {
                    if (keptTimestamp is { } kept) kept.WriteTo(w);
                    else w.WriteStringValue(now);
                },
                ["isSidechain"] = w => w.WriteBooleanValue(false),
                ["forkedFrom"] = w =>
                {
                    w.WriteStartObject();
                    w.WriteString("sessionId", sessionId);
                    w.WriteString("messageUuid", originalUuid);
                    w.WriteEndObject();
                },
            };

            lines.Add(WriteJson(w =>
            {
                w.WriteStartObject();
                foreach (var (name, value) in DistinctProperties(original))
                {
                    if (RemovedFields.Contains(name)) continue;
                    w.WritePropertyName(name);
                    if (overrides.Remove(name, out var write)) write(w);
                    else value.WriteTo(w);
                }
                // Overrides not in the source go at the end, in Python dict order.
                foreach (string name in OverrideOrder)
                {
                    if (!overrides.TryGetValue(name, out var write)) continue;
                    w.WritePropertyName(name);
                    write(w);
                }
                w.WriteEndObject();
            }));
        }

        if (replacements.Count > 0)
        {
            lines.Add(WriteJson(w =>
            {
                w.WriteStartObject();
                w.WriteString("type", "content-replacement");
                w.WriteString("sessionId", forkedSessionId);
                w.WriteStartArray("replacements");
                foreach (var replacement in replacements)
                    replacement.WriteTo(w);
                w.WriteEndArray();
                w.WriteString("uuid", NewUuid());
                w.WriteString("timestamp", now);
                w.WriteEndObject();
            }));
        }

        // Title: explicit, else the source title with " (fork)". The session
        // list reads the last custom-title entry, so this entry is the one it shows.
        string? forkTitle = string.IsNullOrEmpty(title) ? null : title.Trim();
        if (string.IsNullOrEmpty(forkTitle))
            forkTitle = $"{deriveTitle() ?? "Forked session"} (fork)";

        lines.Add(WriteJson(w =>
        {
            w.WriteStartObject();
            w.WriteString("type", "custom-title");
            w.WriteString("sessionId", forkedSessionId);
            w.WriteString("customTitle", forkTitle);
            w.WriteString("uuid", NewUuid());
            w.WriteString("timestamp", now);
            w.WriteEndObject();
        }));

        return (forkedSessionId, lines);
    }

    /// <summary>
    /// Source title from the head and tail of the file: customTitle, then aiTitle,
    /// then the first prompt. Null when there is none.
    /// </summary>
    private static string? DeriveTitle(byte[] content)
    {
        int bufLen = content.Length;
        int headLen = Math.Min(bufLen, SessionStorage.LiteReadBufSize);
        int tailStart = Math.Max(0, bufLen - SessionStorage.LiteReadBufSize);
        string head = Encoding.UTF8.GetString(content, 0, headLen);
        string tail = Encoding.UTF8.GetString(content, tailStart, bufLen - tailStart);

        return NullIfEmpty(SessionStorage.ExtractLastJsonStringField(tail, "customTitle"))
            ?? NullIfEmpty(SessionStorage.ExtractLastJsonStringField(head, "customTitle"))
            ?? NullIfEmpty(SessionStorage.ExtractLastJsonStringField(tail, "aiTitle"))
            ?? NullIfEmpty(SessionStorage.ExtractLastJsonStringField(head, "aiTitle"))
            ?? NullIfEmpty(SessionStorage.ExtractFirstPromptFromHead(head));
    }

    /// <summary>
    /// A truthy string maps to its new uuid, or null when it is not in the fork.
    /// A falsy value stays as it is. A missing field becomes null.
    /// </summary>
    private static void WriteLogicalParent(Utf8JsonWriter w, JsonElement original, Dictionary<string, string> uuidMapping)
    {
        if (!original.TryGetProperty("logicalParentUuid", out var logical))
        {
            w.WriteNullValue();
        }
        else if (!IsTruthy(logical))
        {
            logical.WriteTo(w);
        }
        else
        {
            WriteStringOrNull(w,
                logical.ValueKind == JsonValueKind.String && uuidMapping.TryGetValue(logical.GetString()!, out var mapped)
                    ? mapped
                    : null);
        }
    }

    /// <summary>
    /// Properties in source order. A repeated name keeps its first position and
    /// its last value, the same as a Python dict.
    /// </summary>
    private static List<(string Name, JsonElement Value)> DistinctProperties(JsonElement obj)
    {
        var result = new List<(string, JsonElement)>();
        var index = new Dictionary<string, int>();
        foreach (var property in obj.EnumerateObject())
        {
            if (index.TryGetValue(property.Name, out int i))
            {
                result[i] = (property.Name, property.Value);
            }
            else
            {
                index[property.Name] = result.Count;
                result.Add((property.Name, property.Value));
            }
        }
        return result;
    }

    private static string WriteJson(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            write(w);
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteStringOrNull(Utf8JsonWriter w, string? value)
    {
        if (value is null) w.WriteNullValue();
        else w.WriteStringValue(value);
    }

    /// <summary>Python truthiness of a JSON value.</summary>
    private static bool IsTruthy(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.String => value.GetString()!.Length > 0,
        JsonValueKind.Number => value.GetDouble() != 0,
        JsonValueKind.Array => value.GetArrayLength() > 0,
        JsonValueKind.Object => value.EnumerateObject().Any(),
        _ => false,
    };

    private static bool IsTruthy(JsonElement entry, string property) =>
        entry.TryGetProperty(property, out var value) && IsTruthy(value);

    private static string? GetString(JsonElement el, string property) =>
        el.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static string NewUuid() => Guid.NewGuid().ToString();

    private static InvalidOperationException NoMessages(string sessionId) =>
        new($"Session {sessionId} has no messages to fork");
}
