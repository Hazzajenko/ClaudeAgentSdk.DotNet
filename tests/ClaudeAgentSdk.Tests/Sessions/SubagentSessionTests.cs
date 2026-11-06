namespace ClaudeAgentSdk.Tests.Sessions;

[NotInParallel("sessions-env")]
public class SubagentSessionTests
{
    private const string SessionId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    private string _tempDir = null!;
    private string _projectDir = null!;

    [Before(Test)]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"claude-sub-{Guid.NewGuid():N}");
        _projectDir = Path.Combine(_tempDir, "projects", "test-project");
        Directory.CreateDirectory(_projectDir);
        Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", _tempDir);
        File.WriteAllText(Path.Combine(_projectDir, $"{SessionId}.jsonl"),
            """{"type":"user","uuid":"u1","sessionId":"aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee","message":{"role":"user","content":"Hello"}}""" + "\n");
    }

    [After(Test)]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", null);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string SubagentsDir => Path.Combine(_projectDir, SessionId, "subagents");

    [Test]
    public async Task ListSubagents_ReturnsAgentIdsSortedAndNested()
    {
        WriteAgent("b2", Transcript("s1"));
        WriteAgent("a1", Transcript("s2"));
        WriteAgent("w1", Transcript("s3"), Path.Combine("workflows", "run-1"));
        File.WriteAllText(Path.Combine(SubagentsDir, "agent-a1.meta.json"), "{}");
        File.WriteAllText(Path.Combine(SubagentsDir, "notes.jsonl"), "{}\n");

        var ids = ClaudeAgentSdk.Sessions.Sessions.ListSubagents(SessionId);

        await Assert.That(string.Join(",", ids)).IsEqualTo("a1,b2,w1");
    }

    [Test]
    public async Task ListSubagents_NoSubagentsDirectory_ReturnsEmpty()
    {
        var ids = ClaudeAgentSdk.Sessions.Sessions.ListSubagents(SessionId);

        await Assert.That(ids.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ListSubagents_MissingSessionOrInvalidId_ReturnsEmpty()
    {
        await Assert.That(ClaudeAgentSdk.Sessions.Sessions.ListSubagents("11111111-2222-3333-4444-555555555555").Count).IsEqualTo(0);
        await Assert.That(ClaudeAgentSdk.Sessions.Sessions.ListSubagents("not-a-uuid").Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetSubagentMessages_CarriesParentIdsFromMetaSidecar()
    {
        WriteAgent("a1", Transcript("s1"));
        File.WriteAllText(Path.Combine(SubagentsDir, "agent-a1.meta.json"),
            """{"agentType":"general-purpose","toolUseId":"toolu_01","parentAgentId":"p9"}""");

        var messages = ClaudeAgentSdk.Sessions.Sessions.GetSubagentMessages(SessionId, "a1");

        await Assert.That(messages.Count).IsEqualTo(2);
        await Assert.That(messages[0].Uuid).IsEqualTo("s1-1");
        await Assert.That(messages[1].Uuid).IsEqualTo("s1-2");
        await Assert.That(messages.All(m => m.ParentToolUseId == "toolu_01")).IsTrue();
        await Assert.That(messages.All(m => m.ParentAgentId == "p9")).IsTrue();
    }

    [Test]
    public async Task GetSubagentMessages_FindsNestedAgentFile()
    {
        WriteAgent("w1", Transcript("s1"), Path.Combine("workflows", "run-1"));

        var messages = ClaudeAgentSdk.Sessions.Sessions.GetSubagentMessages(SessionId, "w1");

        await Assert.That(messages.Count).IsEqualTo(2);
    }

    [Test]
    [Arguments(null)]
    [Arguments("not json")]
    [Arguments("[1,2]")]
    [Arguments("""{"toolUseId":5}""")]
    public async Task GetSubagentMessages_UnusableMetaSidecar_LeavesParentIdsNull(string? meta)
    {
        WriteAgent("a1", Transcript("s1"));
        if (meta is not null)
            File.WriteAllText(Path.Combine(SubagentsDir, "agent-a1.meta.json"), meta);

        var messages = ClaudeAgentSdk.Sessions.Sessions.GetSubagentMessages(SessionId, "a1");

        await Assert.That(messages.Count).IsEqualTo(2);
        await Assert.That(messages[0].ParentToolUseId).IsNull();
        await Assert.That(messages[0].ParentAgentId).IsNull();
    }

    [Test]
    public async Task GetSubagentMessages_AppliesOffsetAndLimit()
    {
        WriteAgent("a1", string.Join("\n",
            Entry("m1", null, "user"),
            Entry("m2", "m1", "assistant"),
            Entry("m3", "m2", "user"),
            Entry("m4", "m3", "assistant")) + "\n");

        var page = ClaudeAgentSdk.Sessions.Sessions.GetSubagentMessages(SessionId, "a1", limit: 2, offset: 1);
        var rest = ClaudeAgentSdk.Sessions.Sessions.GetSubagentMessages(SessionId, "a1", offset: 3);

        await Assert.That(string.Join(",", page.Select(m => m.Uuid))).IsEqualTo("m2,m3");
        await Assert.That(string.Join(",", rest.Select(m => m.Uuid))).IsEqualTo("m4");
    }

    [Test]
    public async Task GetSubagentMessages_WalksChainFromLastUserOrAssistantEntry()
    {
        WriteAgent("a1", string.Join("\n",
            Entry("m1", null, "user"),
            Entry("x1", "m1", "assistant"),
            Entry("m2", "m1", "assistant"),
            Entry("p1", "m2", "progress")) + "\n");

        var messages = ClaudeAgentSdk.Sessions.Sessions.GetSubagentMessages(SessionId, "a1");

        await Assert.That(string.Join(",", messages.Select(m => m.Uuid))).IsEqualTo("m1,m2");
    }

    [Test]
    public async Task GetSubagentMessages_UnknownAgentOrEmptyId_ReturnsEmpty()
    {
        WriteAgent("a1", Transcript("s1"));

        await Assert.That(ClaudeAgentSdk.Sessions.Sessions.GetSubagentMessages(SessionId, "zz").Count).IsEqualTo(0);
        await Assert.That(ClaudeAgentSdk.Sessions.Sessions.GetSubagentMessages(SessionId, "").Count).IsEqualTo(0);
        await Assert.That(ClaudeAgentSdk.Sessions.Sessions.GetSubagentMessages("not-a-uuid", "a1").Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetMessages_TopLevelMessagesHaveNullParentIds()
    {
        var messages = ClaudeAgentSdk.Sessions.Sessions.GetMessages(SessionId);

        await Assert.That(messages.Count).IsEqualTo(1);
        await Assert.That(messages[0].ParentToolUseId).IsNull();
        await Assert.That(messages[0].ParentAgentId).IsNull();
    }

    private void WriteAgent(string agentId, string content, string? subDir = null)
    {
        string dir = subDir is null ? SubagentsDir : Path.Combine(SubagentsDir, subDir);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"agent-{agentId}.jsonl"), content);
    }

    private static string Transcript(string prefix) =>
        string.Join("\n",
            Entry($"{prefix}-1", null, "user"),
            Entry($"{prefix}-2", $"{prefix}-1", "assistant")) + "\n";

    private static string Entry(string uuid, string? parentUuid, string type)
    {
        string parent = parentUuid is null ? "null" : $"\"{parentUuid}\"";
        string role = type == "assistant" ? "assistant" : "user";
        return $$$"""{"type":"{{{type}}}","uuid":"{{{uuid}}}","parentUuid":{{{parent}}},"isSidechain":true,"sessionId":"{{{SessionId}}}","message":{"role":"{{{role}}}","content":"text"}}""";
    }
}
