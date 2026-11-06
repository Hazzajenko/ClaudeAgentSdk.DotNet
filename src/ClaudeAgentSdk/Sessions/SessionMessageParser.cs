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

            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                root = doc.RootElement.Clone();
            }
            catch { continue; }
            if (root.ValueKind != JsonValueKind.Object) continue;
            if (!root.TryGetProperty("type", out var typeProp)) continue;
            string? entryType = typeProp.GetString();
            if (entryType is null || !TranscriptEntryTypes.Contains(entryType)) continue;
            if (!root.TryGetProperty("uuid", out var uuidProp) || uuidProp.ValueKind != JsonValueKind.String) continue;

            entries.Add(root);
        }

        return entries;
    }

    /// <summary>
    /// Build conversation chain by finding leaf and walking parentUuid to root.
    /// Returns messages in chronological order (root → leaf). Tool calls and results
    /// that the walk skipped in a parallel tool turn are put back after the walk.
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
        return RecoverOrphanedParallelToolResults(entries, byUuid, chain, chainSeen);
    }

    /// <summary>
    /// Puts back the tool calls and results that a parallel turn left off the chain.
    /// </summary>
    /// <remarks>
    /// The CLI writes one assistant entry per content block. A response with N
    /// parallel tool calls is N assistant entries that share <c>message.id</c>,
    /// chained one after another. The <c>parentUuid</c> of each tool_result entry
    /// is the assistant entry of its own call, so the transcript forks. The
    /// <c>parentUuid</c> walk follows only the branch that the next turn continued
    /// from. It keeps one result and drops every call written after the call that
    /// result answers.
    ///
    /// For each response on the chain, this collects its assistant entries and
    /// tool results that the walk did not reach. It inserts them after the last
    /// entry of the response on the chain, so the response stays in one piece and
    /// each result comes after its call. Ported from Python SDK
    /// <c>_recover_orphaned_parallel_tool_results</c>.
    /// </remarks>
    private static List<JsonElement> RecoverOrphanedParallelToolResults(
        List<JsonElement> entries,
        Dictionary<string, JsonElement> byUuid,
        List<JsonElement> chain,
        HashSet<string> seen)
    {
        var chainAssistants = chain.Where(e => GetString(e, "type") == "assistant").ToList();
        if (chainAssistants.Count == 0) return chain;

        // Anchor is the last entry of each response on the chain. Later entries
        // in chain order overwrite earlier ones.
        var anchorByMessageId = new Dictionary<string, string>();
        foreach (var entry in chainAssistants)
        {
            if (AssistantMessageId(entry) is { } messageId)
                anchorByMessageId[messageId] = Uuid(entry);
        }
        if (anchorByMessageId.Count == 0) return chain;

        // Unique entries in file order. A repeated uuid keeps its first position
        // and its last value, the same as the dictionary in the Python SDK.
        var fileOrder = new List<JsonElement>();
        var filePosition = new Dictionary<string, int>();
        foreach (var entry in entries)
        {
            string uuid = Uuid(entry);
            if (filePosition.TryAdd(uuid, fileOrder.Count))
                fileOrder.Add(byUuid[uuid]);
        }

        // Group each assistant entry by response, and record which entry made
        // each tool call. A tool_use id that two different responses claim is
        // ambiguous. It maps to null and never places a result.
        var siblingsByMessageId = new Dictionary<string, List<JsonElement>>();
        var callOwner = new Dictionary<string, JsonElement?>();
        var toolResultEntries = new List<JsonElement>();
        foreach (var entry in fileOrder)
        {
            if (AssistantMessageId(entry) is { } messageId)
            {
                if (!siblingsByMessageId.TryGetValue(messageId, out var group))
                {
                    group = [];
                    siblingsByMessageId[messageId] = group;
                }
                group.Add(entry);

                foreach (string callId in ContentBlockIds(entry, "tool_use", "id"))
                {
                    if (!callOwner.TryGetValue(callId, out var owner))
                    {
                        callOwner[callId] = entry;
                        continue;
                    }
                    bool sameResponse = owner is { } o && AssistantMessageId(o) == messageId;
                    callOwner[callId] = sameResponse ? entry : null;
                }
            }
            else if (IsToolResultEntry(entry))
            {
                toolResultEntries.Add(entry);
            }
        }

        // Index each tool result under the assistant entries it can belong to:
        // its parent, its sourceToolAssistantUUID and the owner of each call it
        // answers. The last two count only when the same agent wrote both.
        var resultsByAssistant = new Dictionary<string, List<JsonElement>>();
        foreach (var result in toolResultEntries)
        {
            AddTo(resultsByAssistant, GetString(result, "parentUuid")!, result);

            if (GetString(result, "sourceToolAssistantUUID") is { } source
                && byUuid.TryGetValue(source, out var sourceEntry)
                && SameWriter(result, sourceEntry))
            {
                AddTo(resultsByAssistant, source, result);
            }

            foreach (string callId in ContentBlockIds(result, "tool_result", "tool_use_id"))
            {
                if (callOwner.TryGetValue(callId, out var owner) && owner is { } o && SameWriter(result, o))
                    AddTo(resultsByAssistant, Uuid(o), result);
            }
        }

        // Only strays need this set, and strays are rare, so build it on first use.
        HashSet<string>? answeredOnChain = null;

        var processed = new HashSet<string>();
        var inserts = new Dictionary<string, List<JsonElement>>();
        foreach (var assistant in chainAssistants)
        {
            if (AssistantMessageId(assistant) is not { } messageId || !processed.Add(messageId))
                continue;

            List<JsonElement> group = siblingsByMessageId.TryGetValue(messageId, out var g) ? g : [assistant];
            var memberUuids = group.Select(Uuid).ToHashSet();
            var orphanedSiblings = group.Where(m => !seen.Contains(Uuid(m))).ToList();
            var orphanedResults = new List<JsonElement>();

            // Results indexed under this response whose parent is not one of its
            // entries. They come back only if they answer a call that has no answer yet.
            var strays = new List<JsonElement>();
            var collected = new HashSet<string>();
            foreach (var member in group)
            {
                if (!resultsByAssistant.TryGetValue(Uuid(member), out var memberResults)) continue;
                foreach (var result in memberResults)
                {
                    string resultUuid = Uuid(result);
                    if (seen.Contains(resultUuid) || !collected.Add(resultUuid)) continue;
                    if (memberUuids.Contains(GetString(result, "parentUuid")!))
                        orphanedResults.Add(result);
                    else
                        strays.Add(result);
                }
            }

            if (strays.Count > 0)
            {
                answeredOnChain ??= chain
                    .SelectMany(e => ContentBlockIds(e, "tool_result", "tool_use_id"))
                    .ToHashSet();
                var answered = new HashSet<string>(answeredOnChain);
                foreach (var result in orphanedResults)
                    answered.UnionWith(ContentBlockIds(result, "tool_result", "tool_use_id"));

                foreach (var result in strays.OrderBy(e => filePosition[Uuid(e)]))
                {
                    List<string> callIds = ContentBlockIds(result, "tool_result", "tool_use_id");
                    bool answersOpenCall = callIds.Any(callId =>
                        !answered.Contains(callId)
                        && callOwner.TryGetValue(callId, out var owner)
                        && owner is { } o
                        && memberUuids.Contains(Uuid(o)));
                    if (!answersOpenCall) continue;
                    answered.UnionWith(callIds);
                    orphanedResults.Add(result);
                }
            }

            if (orphanedSiblings.Count == 0 && orphanedResults.Count == 0) continue;

            // Timestamp order keeps content block order and completion order.
            // OrderBy is stable, so on a tie siblings keep file order and results
            // keep the order of the calls they answer.
            var recovered = orphanedSiblings.OrderBy(TimestampSortKey, StringComparer.Ordinal)
                .Concat(orphanedResults.OrderBy(TimestampSortKey, StringComparer.Ordinal))
                .ToList();
            foreach (var entry in recovered)
                seen.Add(Uuid(entry));
            inserts[anchorByMessageId[messageId]] = recovered;
        }

        if (inserts.Count == 0) return chain;

        var resultChain = new List<JsonElement>(chain.Count + inserts.Values.Sum(v => v.Count));
        foreach (var entry in chain)
        {
            resultChain.Add(entry);
            if (inserts.TryGetValue(Uuid(entry), out var recovered))
                resultChain.AddRange(recovered);
        }
        return resultChain;
    }

    private static void AddTo(Dictionary<string, List<JsonElement>> index, string key, JsonElement value)
    {
        if (!index.TryGetValue(key, out var list))
        {
            list = [];
            index[key] = list;
        }
        list.Add(value);
    }

    private static string Uuid(JsonElement entry) => entry.GetProperty("uuid").GetString()!;

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string TimestampSortKey(JsonElement entry) => GetString(entry, "timestamp") ?? "";

    /// <summary>The API <c>message.id</c> of an assistant entry, or null.</summary>
    private static string? AssistantMessageId(JsonElement entry)
    {
        if (GetString(entry, "type") != "assistant") return null;
        if (!entry.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) return null;
        string? id = GetString(message, "id");
        return string.IsNullOrEmpty(id) ? null : id;
    }

    /// <summary>The <paramref name="idField"/> of each <paramref name="blockType"/> content block.</summary>
    private static List<string> ContentBlockIds(JsonElement entry, string blockType, string idField)
    {
        var ids = new List<string>();
        if (!entry.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) return ids;
        if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) return ids;
        foreach (var block in content.EnumerateArray())
        {
            if (block.ValueKind == JsonValueKind.Object
                && GetString(block, "type") == blockType
                && GetString(block, idField) is { } id)
            {
                ids.Add(id);
            }
        }
        return ids;
    }

    /// <summary>True for a user entry that has a parent and a tool_result block.</summary>
    private static bool IsToolResultEntry(JsonElement entry)
    {
        if (GetString(entry, "type") != "user") return false;
        if (string.IsNullOrEmpty(GetString(entry, "parentUuid"))) return false;
        if (!entry.TryGetProperty("message", out var message) || message.ValueKind != JsonValueKind.Object) return false;
        if (!message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) return false;
        return content.EnumerateArray().Any(b => b.ValueKind == JsonValueKind.Object && GetString(b, "type") == "tool_result");
    }

    /// <summary>True if the same agent or subagent wrote both entries.</summary>
    private static bool SameWriter(JsonElement a, JsonElement b) =>
        GetBool(a, "isSidechain") == GetBool(b, "isSidechain")
        && JsonElementEquals(a, b, "agentId");

    private static bool JsonElementEquals(JsonElement a, JsonElement b, string prop)
    {
        bool hasA = a.TryGetProperty(prop, out var va) && va.ValueKind != JsonValueKind.Null;
        bool hasB = b.TryGetProperty(prop, out var vb) && vb.ValueKind != JsonValueKind.Null;
        if (!hasA || !hasB) return hasA == hasB;
        return JsonElement.DeepEquals(va, vb);
    }

    /// <summary>
    /// Filter to visible messages and convert to SessionMessage.
    /// </summary>
    public static IReadOnlyList<SessionMessage> ToSessionMessages(
        List<JsonElement> chain, int? limit = null, int offset = 0)
    {
        var messages = chain.Where(IsVisibleMessage).Select(e => ToSessionMessage(e)).ToList();
        return Page(messages, limit, offset);
    }

    /// <summary>
    /// Build the chain of a subagent transcript. A subagent transcript is linear,
    /// so the last user or assistant entry is the leaf. Walk <c>parentUuid</c> to
    /// the root. Ported from Python SDK <c>_build_subagent_chain</c>.
    /// </summary>
    public static List<JsonElement> BuildSubagentChain(List<JsonElement> entries)
    {
        if (entries.Count == 0) return [];

        var byUuid = new Dictionary<string, JsonElement>();
        foreach (var entry in entries)
            byUuid[Uuid(entry)] = entry;

        JsonElement? leaf = null;
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            if (GetString(entries[i], "type") is "user" or "assistant")
            {
                leaf = entries[i];
                break;
            }
        }
        if (leaf is null) return [];

        var chain = new List<JsonElement>();
        var seen = new HashSet<string>();
        JsonElement? current = leaf;
        while (current is { } cur)
        {
            if (!seen.Add(Uuid(cur))) break;
            chain.Add(cur);
            current = GetString(cur, "parentUuid") is { Length: > 0 } parentId
                && byUuid.TryGetValue(parentId, out var parent) ? parent : null;
        }

        chain.Reverse();
        return chain;
    }

    /// <summary>
    /// Convert a subagent chain to messages. Each message gets the same parent ids.
    /// Unlike <see cref="ToSessionMessages"/>, meta and sidechain entries are kept,
    /// because every entry in a subagent transcript is a sidechain entry.
    /// </summary>
    public static IReadOnlyList<SessionMessage> ToSubagentMessages(
        List<JsonElement> chain, int? limit, int offset, string? parentToolUseId, string? parentAgentId)
    {
        var messages = chain
            .Where(e => GetString(e, "type") is "user" or "assistant")
            .Select(e => ToSessionMessage(e, parentToolUseId, parentAgentId))
            .ToList();
        return Page(messages, limit, offset);
    }

    private static SessionMessage ToSessionMessage(
        JsonElement e, string? parentToolUseId = null, string? parentAgentId = null) => new()
    {
        Type = e.GetProperty("type").GetString()!,
        Uuid = e.GetProperty("uuid").GetString()!,
        SessionId = e.TryGetProperty("sessionId", out var sid) ? sid.GetString() ?? "" : "",
        Message = e.TryGetProperty("message", out var msg) ? msg.Clone() : default,
        ParentToolUseId = parentToolUseId,
        ParentAgentId = parentAgentId,
    };

    private static IReadOnlyList<SessionMessage> Page(List<SessionMessage> messages, int? limit, int offset)
    {
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
