using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeAgentSdk.Sessions;

namespace ClaudeAgentSdk.Tests.Sessions;

/// <summary>
/// Parallel tool turns fork the transcript. These tests check that the chain
/// walk puts back the calls and results that the parentUuid walk drops.
/// Ported from the Python SDK TestBuildConversationChain cases.
/// </summary>
[NotInParallel("sessions-env")]
public class ParallelToolRecoveryTests
{
    [Test]
    [Arguments(new[] { 0, 1, 2 })]
    [Arguments(new[] { 2, 1, 0 })]
    [Arguments(new[] { 1, 2, 0 })]
    [Arguments(new[] { 0, 2, 1 })]
    public async Task ParallelToolCalls_KeepEveryCallAndResult(int[] resultOrder)
    {
        var (calls, results) = ParallelToolTurn("prompt", 3, resultOrder);
        var entries = new List<JsonObject> { Entry("user", "prompt", null, "read them") };
        entries.AddRange(calls);
        entries.AddRange(results);
        entries.Add(Entry("assistant", "answer", (string)results[^1]["uuid"]!, "done"));

        var messages = SessionMessageParser.ToSessionMessages(Build(entries));

        List<string> callIds = BlockIds(messages, "tool_use", "id");
        List<string> resultIds = BlockIds(messages, "tool_result", "tool_use_id");
        await AssertSequence(callIds, new[] { "toolu_0", "toolu_1", "toolu_2" });
        await AssertSequence(resultIds.Order().ToList(), new[] { "toolu_0", "toolu_1", "toolu_2" });
        await Assert.That(messages.Count).IsEqualTo(8);
        await Assert.That(messages.Select(m => m.Uuid).Distinct().Count()).IsEqualTo(8);
        await Assert.That(messages[0].Uuid).IsEqualTo("prompt");
        await AssertSequence(messages.Skip(1).Take(6).Select(m => m.Type).ToList(), new[] { "assistant", "assistant", "assistant", "user", "user", "user" });
        await Assert.That(messages[^1].Uuid).IsEqualTo("answer");
    }

    [Test]
    public async Task ParallelToolCalls_AsLastTurn_RecoversResults()
    {
        var (calls, results) = ParallelToolTurn("prompt", 2, [1, 0]);
        var entries = new List<JsonObject> { Entry("user", "prompt", null, "read them") };
        entries.AddRange(calls);
        entries.AddRange(results);

        List<string> uuids = Uuids(Build(entries));

        await AssertSequence(uuids, new[] { "prompt", "call-0", "call-1", "result-1", "result-0" });
    }

    [Test]
    public async Task ParallelToolCalls_LimitOffset_CountsRecoveredEntries()
    {
        var (calls, results) = ParallelToolTurn("prompt", 3, [0, 1, 2]);
        var entries = new List<JsonObject> { Entry("user", "prompt", null, "read them") };
        entries.AddRange(calls);
        entries.AddRange(results);
        entries.Add(Entry("assistant", "answer", "result-2", "k"));

        var page = SessionMessageParser.ToSessionMessages(Build(entries), limit: 3, offset: 4);

        await AssertSequence(page.Select(m => m.Uuid).ToList(), new[] { "result-0", "result-1", "result-2" });
    }

    [Test]
    public async Task ParallelToolCalls_ThroughSessionsGetMessages_ReturnsBothCallsAndResults()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"claude-par-{Guid.NewGuid():N}");
        string projectDir = Path.Combine(tempDir, "projects", "test-project");
        Directory.CreateDirectory(projectDir);
        const string sid = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        try
        {
            var (calls, results) = ParallelToolTurn("prompt", 2, [0, 1]);
            var entries = new List<JsonObject> { Entry("user", "prompt", null, "read them") };
            entries.AddRange(calls);
            entries.AddRange(results);
            entries.Add(Entry("assistant", "answer", "result-1", "done"));
            File.WriteAllText(Path.Combine(projectDir, $"{sid}.jsonl"),
                string.Join("\n", entries.Select(e => e.ToJsonString())) + "\n");

            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", tempDir);
            var messages = ClaudeAgentSdk.Sessions.Sessions.GetMessages(sid);

            await AssertSequence(BlockIds(messages, "tool_use", "id"), new[] { "toolu_0", "toolu_1" });
            await AssertSequence(BlockIds(messages, "tool_result", "tool_use_id"), new[] { "toolu_0", "toolu_1" });
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", null);
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    public async Task RecoversSiblingCallsDroppedByTheWalk()
    {
        var (calls, results) = ParallelToolTurn("prompt", 3, [1, 2, 0]);
        var entries = new List<JsonObject> { Bare("user", "prompt", null) };
        entries.AddRange(calls);
        entries.AddRange(results);
        entries.Add(Bare("assistant", "answer", "result-0"));

        await AssertSequence(Uuids(Build(entries)), new[]
        {
            "prompt", "call-0", "call-1", "call-2", "result-1", "result-2", "result-0", "answer",
        });
    }

    [Test]
    public async Task RecoveredResults_AreInCompletionOrder()
    {
        var (calls, results) = ParallelToolTurn("prompt", 3, [1, 0, 2]);
        var entries = new List<JsonObject> { Bare("user", "prompt", null) };
        entries.AddRange(calls);
        entries.AddRange(results);
        entries.Add(Bare("assistant", "answer", "result-2"));

        await AssertSequence(Uuids(Build(entries)), new[]
        {
            "prompt", "call-0", "call-1", "call-2", "result-1", "result-0", "result-2", "answer",
        });
    }

    [Test]
    public async Task RecoveredCalls_AreInTimestampOrder()
    {
        var (calls, results) = ParallelToolTurn("prompt", 3, [1, 2, 0]);
        var entries = new List<JsonObject> { Bare("user", "prompt", null), calls[0], calls[2], calls[1] };
        entries.AddRange(results);
        entries.Add(Bare("assistant", "answer", "result-0"));

        List<string> uuids = Uuids(Build(entries));

        await AssertSequence(uuids.Skip(1).Take(3).ToList(), new[] { "call-0", "call-1", "call-2" });
    }

    [Test]
    public async Task SequentialToolCalls_AreUnchanged()
    {
        var entries = new List<JsonObject>
        {
            Bare("user", "prompt", null),
            Call("call-0", "prompt", "msg_1", "toolu_0"),
            Result("result-0", "call-0", "toolu_0"),
            Call("call-1", "result-0", "msg_1", "toolu_1"),
            Result("result-1", "call-1", "toolu_1"),
        };

        await AssertSequence(Uuids(Build(entries)), new[] { "prompt", "call-0", "result-0", "call-1", "result-1" });
    }

    [Test]
    public async Task AbandonedBranch_IsNotRecovered()
    {
        var (calls, results) = ParallelToolTurn("prompt", 2, [0, 1]);
        var entries = new List<JsonObject>
        {
            Bare("user", "prompt", null),
            Call("old-call", "prompt", "msg_abandoned", "toolu_old"),
            Result("old-result", "old-call", "toolu_old"),
        };
        entries.AddRange(calls);
        entries.AddRange(results);

        await AssertSequence(Uuids(Build(entries)), new[] { "prompt", "call-0", "call-1", "result-0", "result-1" });
    }

    [Test]
    public async Task RecoversResultWhoseParentIsOutsideTheTurn()
    {
        var (calls, results) = ParallelToolTurn("prompt", 2, [0, 1]);
        results[0]["parentUuid"] = "missing";
        results[0].Remove("sourceToolAssistantUUID");
        var entries = new List<JsonObject> { Bare("user", "prompt", null) };
        entries.AddRange(calls);
        entries.AddRange(results);
        entries.Add(Bare("assistant", "answer", "result-1"));

        await AssertSequence(Uuids(Build(entries)), new[]
        {
            "prompt", "call-0", "call-1", "result-0", "result-1", "answer",
        });
    }

    [Test]
    public async Task DoesNotDuplicateAnAnsweredCall()
    {
        var (calls, results) = ParallelToolTurn("prompt", 2, [0, 1]);
        var duplicate = (JsonObject)results[1].DeepClone();
        duplicate["uuid"] = "result-1-copy";
        duplicate["parentUuid"] = "elsewhere";
        duplicate.Remove("sourceToolAssistantUUID");
        var entries = new List<JsonObject> { Bare("user", "prompt", null) };
        entries.AddRange(calls);
        entries.AddRange(results);
        entries.Add(duplicate);
        entries.Add(Bare("assistant", "answer", "result-1"));

        await AssertSequence(Uuids(Build(entries)), new[]
        {
            "prompt", "call-0", "call-1", "result-0", "result-1", "answer",
        });
    }

    [Test]
    public async Task DoesNotPullInAnotherAgentsResult()
    {
        var (calls, results) = ParallelToolTurn("prompt", 2, [0, 1]);
        results[0]["parentUuid"] = "sidechain-call";
        results[0]["isSidechain"] = true;
        results[0]["agentId"] = "agent-1";
        results[0].Remove("sourceToolAssistantUUID");
        var entries = new List<JsonObject> { Bare("user", "prompt", null) };
        entries.AddRange(calls);
        entries.AddRange(results);
        entries.Add(Bare("assistant", "answer", "result-1"));

        await AssertSequence(Uuids(Build(entries)), new[] { "prompt", "call-0", "call-1", "result-1", "answer" });
    }

    [Test]
    public async Task Recovery_ToleratesMalformedEntries()
    {
        var (calls, results) = ParallelToolTurn("prompt", 2, [0, 1]);
        results[0]["timestamp"] = 12345;
        var entries = new List<JsonObject> { Bare("user", "prompt", null) };
        entries.AddRange(calls);
        entries.AddRange(results);
        entries.Add(Bare("assistant", "no-message", "result-1"));
        entries.Add(JsonNode.Parse("""
            {"type":"assistant","uuid":"odd","parentUuid":"no-message","message":{"id":7,"content":["text",null,{"type":"tool_use"}]}}
            """)!.AsObject());
        entries.Add(JsonNode.Parse("""
            {"type":"user","uuid":"odd-result","parentUuid":"odd","message":{"content":[{"type":"tool_result","tool_use_id":3}]}}
            """)!.AsObject());

        await AssertSequence(Uuids(Build(entries)), new[]
        {
            "prompt", "call-0", "call-1", "result-0", "result-1", "no-message", "odd", "odd-result",
        });
    }

    // Matches what the CLI writes: one assistant entry per tool_use block, all
    // with the same message.id and chained one after another, and one
    // tool_result entry per call whose parentUuid is that call's entry.
    // resultOrder is the order the results are written in.
    private static (List<JsonObject> Calls, List<JsonObject> Results) ParallelToolTurn(
        string parentUuid, int tools, int[] resultOrder)
    {
        var calls = new List<JsonObject>();
        for (int i = 0; i < tools; i++)
        {
            var call = Call($"call-{i}", calls.Count > 0 ? (string)calls[^1]["uuid"]! : parentUuid, "msg_parallel", $"toolu_{i}");
            call["timestamp"] = $"2026-01-01T00:00:01.{i:D3}Z";
            calls.Add(call);
        }

        var results = new List<JsonObject>();
        for (int position = 0; position < resultOrder.Length; position++)
        {
            int i = resultOrder[position];
            var result = Result($"result-{i}", $"call-{i}", $"toolu_{i}");
            result["sourceToolAssistantUUID"] = $"call-{i}";
            result["timestamp"] = $"2026-01-01T00:00:02.{position:D3}Z";
            results.Add(result);
        }

        return (calls, results);
    }

    private static async Task AssertSequence(IEnumerable<string> actual, string[] expected) =>
        await Assert.That(string.Join(",", actual)).IsEqualTo(string.Join(",", expected));

    private static JsonObject Bare(string type, string uuid, string? parentUuid) => new()
    {
        ["type"] = type,
        ["uuid"] = uuid,
        ["parentUuid"] = parentUuid,
        ["sessionId"] = "s",
    };

    private static JsonObject Entry(string type, string uuid, string? parentUuid, string content)
    {
        var entry = Bare(type, uuid, parentUuid);
        entry["message"] = new JsonObject { ["role"] = type, ["content"] = content };
        return entry;
    }

    private static JsonObject Call(string uuid, string parentUuid, string messageId, string toolUseId)
    {
        var entry = Bare("assistant", uuid, parentUuid);
        entry["message"] = new JsonObject
        {
            ["id"] = messageId,
            ["role"] = "assistant",
            ["content"] = new JsonArray(new JsonObject
            {
                ["type"] = "tool_use", ["id"] = toolUseId, ["name"] = "Read", ["input"] = new JsonObject(),
            }),
        };
        return entry;
    }

    private static JsonObject Result(string uuid, string parentUuid, string toolUseId)
    {
        var entry = Bare("user", uuid, parentUuid);
        entry["message"] = new JsonObject
        {
            ["role"] = "user",
            ["content"] = new JsonArray(new JsonObject
            {
                ["type"] = "tool_result", ["tool_use_id"] = toolUseId, ["content"] = "ok",
            }),
        };
        return entry;
    }

    private static List<JsonElement> Build(List<JsonObject> entries)
    {
        string content = string.Join("\n", entries.Select(e => e.ToJsonString()));
        return SessionMessageParser.BuildConversationChain(SessionMessageParser.ParseTranscriptEntries(content));
    }

    private static List<string> Uuids(List<JsonElement> chain) =>
        chain.Select(e => e.GetProperty("uuid").GetString()!).ToList();

    private static List<string> BlockIds(IReadOnlyList<SessionMessage> messages, string blockType, string field)
    {
        var ids = new List<string>();
        foreach (var message in messages)
        {
            if (message.Message.ValueKind != JsonValueKind.Object) continue;
            if (!message.Message.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
            foreach (var block in content.EnumerateArray())
            {
                if (block.ValueKind == JsonValueKind.Object
                    && block.TryGetProperty("type", out var t) && t.GetString() == blockType
                    && block.TryGetProperty(field, out var id) && id.ValueKind == JsonValueKind.String)
                {
                    ids.Add(id.GetString()!);
                }
            }
        }
        return ids;
    }
}
