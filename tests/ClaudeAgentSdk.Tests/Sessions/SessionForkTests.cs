using System.Text.Json;
using ClaudeAgentSdk.Sessions;

namespace ClaudeAgentSdk.Tests.Sessions;

[NotInParallel("sessions-env")]
public class SessionForkTests
{
    private const string SessionId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    private const string U1 = "10000000-0000-0000-0000-000000000001";
    private const string A1 = "10000000-0000-0000-0000-000000000002";
    private const string U2 = "10000000-0000-0000-0000-000000000003";
    private const string A2 = "10000000-0000-0000-0000-000000000004";
    private string _tempDir = null!;
    private string _projectDir = null!;

    [Before(Test)]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"claude-fork-{Guid.NewGuid():N}");
        _projectDir = Path.Combine(_tempDir, "projects", "test-project");
        Directory.CreateDirectory(_projectDir);
        Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", _tempDir);
    }

    [After(Test)]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", null);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task Fork_NewSessionIsReadableByGetMessages()
    {
        WriteSource(Conversation());

        ForkSessionResult result = ClaudeAgentSdk.Sessions.Sessions.Fork(SessionId);

        await Assert.That(result.SessionId).IsNotEqualTo(SessionId);
        await Assert.That(Guid.TryParse(result.SessionId, out _)).IsTrue();
        await Assert.That(File.Exists(Path.Combine(_projectDir, $"{result.SessionId}.jsonl"))).IsTrue();

        var source = ClaudeAgentSdk.Sessions.Sessions.GetMessages(SessionId);
        var forked = ClaudeAgentSdk.Sessions.Sessions.GetMessages(result.SessionId);
        await Assert.That(forked.Count).IsEqualTo(4);
        for (int i = 0; i < forked.Count; i++)
        {
            await Assert.That(forked[i].Type).IsEqualTo(source[i].Type);
            await Assert.That(forked[i].Uuid).IsNotEqualTo(source[i].Uuid);
            await Assert.That(forked[i].SessionId).IsEqualTo(result.SessionId);
            await Assert.That(JsonElement.DeepEquals(forked[i].Message, source[i].Message)).IsTrue();
        }
    }

    [Test]
    public async Task Fork_RemapsParentChainAndSetsForkedFrom()
    {
        WriteSource(Conversation());

        var lines = ReadFork(ClaudeAgentSdk.Sessions.Sessions.Fork(SessionId));

        var messages = lines.Where(l => l.GetProperty("type").GetString() is "user" or "assistant").ToList();
        await Assert.That(messages[0].GetProperty("parentUuid").ValueKind).IsEqualTo(JsonValueKind.Null);
        for (int i = 1; i < messages.Count; i++)
        {
            await Assert.That(messages[i].GetProperty("parentUuid").GetString())
                .IsEqualTo(messages[i - 1].GetProperty("uuid").GetString());
        }

        string[] sourceUuids = [U1, A1, U2, A2];
        for (int i = 0; i < messages.Count; i++)
        {
            var forkedFrom = messages[i].GetProperty("forkedFrom");
            await Assert.That(forkedFrom.GetProperty("sessionId").GetString()).IsEqualTo(SessionId);
            await Assert.That(forkedFrom.GetProperty("messageUuid").GetString()).IsEqualTo(sourceUuids[i]);
            await Assert.That(messages[i].GetProperty("isSidechain").ValueKind).IsEqualTo(JsonValueKind.False);
        }
    }

    [Test]
    public async Task Fork_KeepsKeyOrderAndDropsSourceOnlyFields()
    {
        WriteSource(
            $$$"""{"parentUuid":null,"isSidechain":false,"type":"user","uuid":"{{{U1}}}","teamName":"t","agentName":"n","slug":"s","sourceToolAssistantUUID":"x","cwd":"/w","sessionId":"{{{SessionId}}}","timestamp":"2024-01-01T00:00:00.000Z","message":{"role":"user","content":"Hi"}}""");

        var line = ReadFork(ClaudeAgentSdk.Sessions.Sessions.Fork(SessionId))[0];

        string[] keys = line.EnumerateObject().Select(p => p.Name).ToArray();
        await Assert.That(string.Join(",", keys)).IsEqualTo(
            "parentUuid,isSidechain,type,uuid,cwd,sessionId,timestamp,message,logicalParentUuid,forkedFrom");
        await Assert.That(line.GetProperty("cwd").GetString()).IsEqualTo("/w");
    }

    [Test]
    public async Task Fork_UpdatesTimestampOnlyOnLastMessage()
    {
        WriteSource(Conversation());

        var lines = ReadFork(ClaudeAgentSdk.Sessions.Sessions.Fork(SessionId));

        await Assert.That(lines[0].GetProperty("timestamp").GetString()).IsEqualTo("2024-01-01T00:00:01.000Z");
        await Assert.That(lines[2].GetProperty("timestamp").GetString()).IsEqualTo("2024-01-01T00:00:03.000Z");
        var last = DateTimeOffset.Parse(lines[3].GetProperty("timestamp").GetString()!);
        await Assert.That(last).IsGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    [Test]
    public async Task Fork_DropsSidechainAndProgressEntriesAndSkipsProgressParents()
    {
        WriteSource(
            Entry("user", U1, null),
            Entry("progress", "20000000-0000-0000-0000-000000000001", U1),
            Entry("assistant", A1, "20000000-0000-0000-0000-000000000001"),
            Entry("assistant", "30000000-0000-0000-0000-000000000001", U1, extra: ",\"isSidechain\":true"));

        var lines = ReadFork(ClaudeAgentSdk.Sessions.Sessions.Fork(SessionId));

        var messages = lines.Where(l => l.GetProperty("type").GetString() != "custom-title").ToList();
        await Assert.That(messages.Count).IsEqualTo(2);
        await Assert.That(messages[1].GetProperty("forkedFrom").GetProperty("messageUuid").GetString()).IsEqualTo(A1);
        await Assert.That(messages[1].GetProperty("parentUuid").GetString())
            .IsEqualTo(messages[0].GetProperty("uuid").GetString());
    }

    [Test]
    public async Task Fork_RemapsLogicalParentUuid()
    {
        WriteSource(
            Entry("user", U1, null),
            Entry("system", A1, null, extra: $",\"logicalParentUuid\":\"{U1}\""),
            Entry("user", U2, A1, extra: ",\"logicalParentUuid\":\"99999999-0000-0000-0000-000000000000\""));

        var lines = ReadFork(ClaudeAgentSdk.Sessions.Sessions.Fork(SessionId));

        await Assert.That(lines[0].GetProperty("logicalParentUuid").ValueKind).IsEqualTo(JsonValueKind.Null);
        await Assert.That(lines[1].GetProperty("logicalParentUuid").GetString())
            .IsEqualTo(lines[0].GetProperty("uuid").GetString());
        await Assert.That(lines[2].GetProperty("logicalParentUuid").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task Fork_UpToMessageId_StopsAtThatMessageInclusive()
    {
        WriteSource(Conversation());

        var result = ClaudeAgentSdk.Sessions.Sessions.Fork(SessionId, upToMessageId: A1);

        var forked = ClaudeAgentSdk.Sessions.Sessions.GetMessages(result.SessionId);
        await Assert.That(forked.Count).IsEqualTo(2);
        await Assert.That(forked[1].Type).IsEqualTo("assistant");
    }

    [Test]
    public void Fork_UpToMessageIdNotInTranscript_ThrowsArgumentException()
    {
        WriteSource(Conversation());

        Assert.Throws<ArgumentException>(() =>
            ClaudeAgentSdk.Sessions.Sessions.Fork(SessionId, upToMessageId: "99999999-0000-0000-0000-000000000000"));
    }

    [Test]
    public void Fork_InvalidIds_ThrowArgumentException()
    {
        WriteSource(Conversation());

        Assert.Throws<ArgumentException>(() => ClaudeAgentSdk.Sessions.Sessions.Fork("not-a-uuid"));
        Assert.Throws<ArgumentException>(() =>
            ClaudeAgentSdk.Sessions.Sessions.Fork(SessionId, upToMessageId: "not-a-uuid"));
    }

    [Test]
    public void Fork_MissingSession_ThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() => ClaudeAgentSdk.Sessions.Sessions.Fork(SessionId));
    }

    [Test]
    public void Fork_NoTranscriptMessages_ThrowsInvalidOperation()
    {
        WriteSource($$"""{"type":"custom-title","customTitle":"x","sessionId":"{{SessionId}}"}""");

        Assert.Throws<InvalidOperationException>(() => ClaudeAgentSdk.Sessions.Sessions.Fork(SessionId));
    }

    [Test]
    public async Task Fork_ExplicitTitle_IsTrimmedAndUsed()
    {
        WriteSource(Conversation());

        var result = ClaudeAgentSdk.Sessions.Sessions.Fork(SessionId, title: "  Branch B  ");

        await Assert.That(ClaudeAgentSdk.Sessions.Sessions.GetInfo(result.SessionId)!.CustomTitle).IsEqualTo("Branch B");
    }

    [Test]
    public async Task Fork_DerivedTitle_UsesSourceTitleWithForkSuffix()
    {
        WriteSource([.. Conversation(),
            $$"""{"type":"custom-title","customTitle":"Old name","sessionId":"{{SessionId}}"}"""]);

        var result = ClaudeAgentSdk.Sessions.Sessions.Fork(SessionId, title: "   ");

        await Assert.That(ClaudeAgentSdk.Sessions.Sessions.GetInfo(result.SessionId)!.CustomTitle).IsEqualTo("Old name (fork)");
    }

    [Test]
    public async Task Fork_DerivedTitle_FallsBackToFirstPrompt()
    {
        WriteSource(Conversation());

        var result = ClaudeAgentSdk.Sessions.Sessions.Fork(SessionId);

        await Assert.That(ClaudeAgentSdk.Sessions.Sessions.GetInfo(result.SessionId)!.CustomTitle).IsEqualTo("First (fork)");
    }

    [Test]
    public async Task Fork_DerivedTitle_FallsBackToForkedSession()
    {
        WriteSource(Entry("assistant", A1, null));

        var result = ClaudeAgentSdk.Sessions.Sessions.Fork(SessionId);

        await Assert.That(ClaudeAgentSdk.Sessions.Sessions.GetInfo(result.SessionId)!.CustomTitle).IsEqualTo("Forked session (fork)");
    }

    [Test]
    public async Task Fork_ReemitsContentReplacementsOfSourceSession()
    {
        WriteSource([.. Conversation(),
            $$"""{"type":"content-replacement","sessionId":"{{SessionId}}","replacements":[{"id":1}]}""",
            """{"type":"content-replacement","sessionId":"11111111-2222-3333-4444-555555555555","replacements":[{"id":2}]}""",
            $$"""{"type":"content-replacement","sessionId":"{{SessionId}}","replacements":[{"id":3}]}"""]);

        var result = ClaudeAgentSdk.Sessions.Sessions.Fork(SessionId);
        var lines = ReadFork(result);

        var replacement = lines.Single(l => l.GetProperty("type").GetString() == "content-replacement");
        await Assert.That(replacement.GetProperty("sessionId").GetString()).IsEqualTo(result.SessionId);
        await Assert.That(replacement.GetProperty("replacements").GetRawText()).IsEqualTo("""[{"id":1},{"id":3}]""");
        await Assert.That(lines[^1].GetProperty("type").GetString()).IsEqualTo("custom-title");
    }

    [Test]
    public async Task Fork_LeavesSourceFileUnchanged()
    {
        WriteSource(Conversation());
        string path = Path.Combine(_projectDir, $"{SessionId}.jsonl");
        string before = File.ReadAllText(path);

        ClaudeAgentSdk.Sessions.Sessions.Fork(SessionId);

        await Assert.That(File.ReadAllText(path)).IsEqualTo(before);
    }

    private static string[] Conversation() =>
    [
        Entry("user", U1, null, content: "First", timestamp: "2024-01-01T00:00:01.000Z"),
        Entry("assistant", A1, U1, timestamp: "2024-01-01T00:00:02.000Z"),
        Entry("user", U2, A1, content: "Second", timestamp: "2024-01-01T00:00:03.000Z"),
        Entry("assistant", A2, U2, timestamp: "2024-01-01T00:00:04.000Z"),
    ];

    private static string Entry(
        string type, string uuid, string? parentUuid, string content = "text",
        string timestamp = "2024-01-01T00:00:00.000Z", string extra = "")
    {
        string parent = parentUuid is null ? "null" : $"\"{parentUuid}\"";
        string role = type == "assistant" ? "assistant" : "user";
        return $$$"""{"type":"{{{type}}}","uuid":"{{{uuid}}}","parentUuid":{{{parent}}},"sessionId":"{{{SessionId}}}","timestamp":"{{{timestamp}}}"{{{extra}}},"message":{"role":"{{{role}}}","content":"{{{content}}}"}}""";
    }

    private void WriteSource(params string[] lines) =>
        File.WriteAllText(Path.Combine(_projectDir, $"{SessionId}.jsonl"), string.Join("\n", lines) + "\n");

    private List<JsonElement> ReadFork(ForkSessionResult result)
    {
        string text = File.ReadAllText(Path.Combine(_projectDir, $"{result.SessionId}.jsonl"));
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l =>
            {
                using var doc = JsonDocument.Parse(l);
                return doc.RootElement.Clone();
            })
            .ToList();
    }
}
