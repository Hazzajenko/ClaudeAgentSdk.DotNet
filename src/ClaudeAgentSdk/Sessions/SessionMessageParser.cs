using System.Text.Json;

namespace ClaudeAgentSdk.Sessions;

/// <summary>
/// Parses session JSONL transcripts and builds conversation chains.
/// Ported from Python SDK sessions.py — _parse_transcript_entries + _build_conversation_chain.
/// </summary>
internal static class SessionMessageParser
{
    private static readonly HashSet<string> TranscriptEntryTypes = ["user", "assistant", "progress", "system", "attachment"];

    /// <summary>
    /// Parse JSONL content into transcript entries (only those with uuid and valid type).
    /// </summary>
    public static List<JsonElement> ParseTranscriptEntries(string content)
    {
        var entries = new List<JsonElement>();

        foreach (string line in content.Split('\n'))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            JsonDocument doc;
            try { doc = JsonDocument.Parse(trimmed); }
            catch { continue; }

            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) continue;
            if (!root.TryGetProperty("type", out var typeProp)) continue;
            string? entryType = typeProp.GetString();
            if (entryType is null || !TranscriptEntryTypes.Contains(entryType)) continue;
            if (!root.TryGetProperty("uuid", out var uuidProp) || uuidProp.ValueKind != JsonValueKind.String) continue;

            entries.Add(root.Clone());
        }

        return entries;
    }

    /// <summary>
    /// Build conversation chain by finding leaf and walking parentUuid to root.
    /// Returns messages in chronological order (root → leaf).
    /// </summary>
    public static List<JsonElement> BuildConversationChain(List<JsonElement> entries)
    {
        if (entries.Count == 0) return [];

        // Index by uuid
        var byUuid = new Dictionary<string, JsonElement>();
        var entryIndex = new Dictionary<string, int>();
        for (int i = 0; i < entries.Count; i++)
        {
            string uuid = entries[i].GetProperty("uuid").GetString()!;
            byUuid[uuid] = entries[i];
            entryIndex[uuid] = i;
        }

        // Find terminal messages (no children point to them)
        var parentUuids = new HashSet<string>();
        foreach (var entry in entries)
        {
            if (entry.TryGetProperty("parentUuid", out var parent) && parent.ValueKind == JsonValueKind.String)
                parentUuids.Add(parent.GetString()!);
        }

        var terminals = new List<JsonElement>();
        foreach (var entry in entries)
        {
            string uuid = entry.GetProperty("uuid").GetString()!;
            if (!parentUuids.Contains(uuid))
                terminals.Add(entry);
        }

        // From each terminal, walk back to find nearest user/assistant leaf
        var leaves = new List<JsonElement>();
        foreach (var terminal in terminals)
        {
            var seen = new HashSet<string>();
            JsonElement? current = terminal;
            while (current is { } cur)
            {
                string uid = cur.GetProperty("uuid").GetString()!;
                if (!seen.Add(uid)) break;

                string? type = cur.TryGetProperty("type", out var t) ? t.GetString() : null;
                if (type is "user" or "assistant")
                {
                    leaves.Add(cur);
                    break;
                }

                string? parentId = cur.TryGetProperty("parentUuid", out var p) && p.ValueKind == JsonValueKind.String
                    ? p.GetString() : null;
                current = parentId is not null && byUuid.TryGetValue(parentId, out var parentEntry)
                    ? parentEntry : null;
            }
        }

        if (leaves.Count == 0) return [];

        // Pick best leaf — prefer main chain (not sidechain/team/meta), highest index
        var mainLeaves = leaves.Where(l =>
            !GetBool(l, "isSidechain") &&
            !l.TryGetProperty("teamName", out _) &&
            !GetBool(l, "isMeta")).ToList();

        JsonElement leaf = PickBest(mainLeaves.Count > 0 ? mainLeaves : leaves, entryIndex);

        // Walk from leaf to root
        var chain = new List<JsonElement>();
        var chainSeen = new HashSet<string>();
        JsonElement? chainCur = leaf;
        while (chainCur is { } cur)
        {
            string uid = cur.GetProperty("uuid").GetString()!;
            if (!chainSeen.Add(uid)) break;
            chain.Add(cur);

            string? parentId = cur.TryGetProperty("parentUuid", out var p) && p.ValueKind == JsonValueKind.String
                ? p.GetString() : null;
            chainCur = parentId is not null && byUuid.TryGetValue(parentId, out var parentEntry)
                ? parentEntry : null;
        }

        chain.Reverse();
        return chain;
    }

    /// <summary>
    /// Filter to visible messages and convert to SessionMessage.
    /// </summary>
    public static IReadOnlyList<SessionMessage> ToSessionMessages(
        List<JsonElement> chain, int? limit = null, int offset = 0)
    {
        var visible = chain.Where(IsVisibleMessage).ToList();
        var messages = visible.Select(e => new SessionMessage
        {
            Type = e.GetProperty("type").GetString()!,
            Uuid = e.GetProperty("uuid").GetString()!,
            SessionId = e.TryGetProperty("sessionId", out var sid) ? sid.GetString() ?? "" : "",
            Message = e.TryGetProperty("message", out var msg) ? msg.Clone() : default
        }).ToList();

        if (limit is > 0)
            return messages.Skip(offset).Take(limit.Value).ToList();
        if (offset > 0)
            return messages.Skip(offset).ToList();
        return messages;
    }

    private static bool IsVisibleMessage(JsonElement entry)
    {
        string? type = entry.TryGetProperty("type", out var t) ? t.GetString() : null;
        if (type is not ("user" or "assistant")) return false;
        if (GetBool(entry, "isMeta")) return false;
        if (GetBool(entry, "isSidechain")) return false;
        if (entry.TryGetProperty("teamName", out _)) return false;
        return true;
    }

    private static bool GetBool(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.True;

    private static JsonElement PickBest(List<JsonElement> candidates, Dictionary<string, int> entryIndex)
    {
        var best = candidates[0];
        int bestIdx = entryIndex.GetValueOrDefault(best.GetProperty("uuid").GetString()!, -1);
        for (int i = 1; i < candidates.Count; i++)
        {
            int idx = entryIndex.GetValueOrDefault(candidates[i].GetProperty("uuid").GetString()!, -1);
            if (idx > bestIdx)
            {
                best = candidates[i];
                bestIdx = idx;
            }
        }
        return best;
    }
}
