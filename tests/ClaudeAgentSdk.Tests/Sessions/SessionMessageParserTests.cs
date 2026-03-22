using ClaudeAgentSdk.Sessions;

namespace ClaudeAgentSdk.Tests.Sessions;

[NotInParallel("sessions-env")]
public class SessionMessageParserTests
{
    private string _tempDir = null!;

    [Before(Test)]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"claude-msg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
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
    public async Task GetMessages_SimpleConversation_ReturnsChronological()
    {
        string content = string.Join("\n",
            """{"type":"user","uuid":"u1","sessionId":"s1","message":{"role":"user","content":"Hello"}}""",
            """{"type":"assistant","uuid":"a1","parentUuid":"u1","sessionId":"s1","message":{"role":"assistant","content":"Hi!"}}""",
            """{"type":"user","uuid":"u2","parentUuid":"a1","sessionId":"s1","message":{"role":"user","content":"How are you?"}}""",
            """{"type":"assistant","uuid":"a2","parentUuid":"u2","sessionId":"s1","message":{"role":"assistant","content":"Good!"}}""");

        CreateSessionFile("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", content);

        var messages = ClaudeAgentSdk.Sessions.Sessions.GetMessages("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        await Assert.That(messages.Count).IsEqualTo(4);
        await Assert.That(messages[0].Type).IsEqualTo("user");
        await Assert.That(messages[0].Uuid).IsEqualTo("u1");
        await Assert.That(messages[1].Type).IsEqualTo("assistant");
        await Assert.That(messages[2].Type).IsEqualTo("user");
        await Assert.That(messages[3].Type).IsEqualTo("assistant");
    }

    [Test]
    public async Task GetMessages_FiltersSidechainMessages()
    {
        string content = string.Join("\n",
            """{"type":"user","uuid":"u1","sessionId":"s1","message":{"role":"user","content":"Hello"}}""",
            """{"type":"assistant","uuid":"a1","parentUuid":"u1","sessionId":"s1","isSidechain":true,"message":{"role":"assistant","content":"Sidechain"}}""",
            """{"type":"assistant","uuid":"a2","parentUuid":"u1","sessionId":"s1","message":{"role":"assistant","content":"Main"}}""");

        CreateSessionFile("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", content);

        var messages = ClaudeAgentSdk.Sessions.Sessions.GetMessages("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        // Sidechain messages should be excluded from the visible chain
        foreach (var msg in messages)
        {
            await Assert.That(msg.Message.GetProperty("content").GetString()).IsNotEqualTo("Sidechain");
        }
    }

    [Test]
    public async Task GetMessages_WithLimitAndOffset()
    {
        string content = string.Join("\n",
            """{"type":"user","uuid":"u1","sessionId":"s1","message":{"role":"user","content":"First"}}""",
            """{"type":"assistant","uuid":"a1","parentUuid":"u1","sessionId":"s1","message":{"role":"assistant","content":"Reply1"}}""",
            """{"type":"user","uuid":"u2","parentUuid":"a1","sessionId":"s1","message":{"role":"user","content":"Second"}}""",
            """{"type":"assistant","uuid":"a2","parentUuid":"u2","sessionId":"s1","message":{"role":"assistant","content":"Reply2"}}""");

        CreateSessionFile("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", content);

        var page = ClaudeAgentSdk.Sessions.Sessions.GetMessages("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", limit: 2, offset: 1);

        await Assert.That(page.Count).IsEqualTo(2);
        await Assert.That(page[0].Uuid).IsEqualTo("a1");
        await Assert.That(page[1].Uuid).IsEqualTo("u2");
    }

    [Test]
    public async Task GetMessages_InvalidUuid_ReturnsEmpty()
    {
        var messages = ClaudeAgentSdk.Sessions.Sessions.GetMessages("not-a-uuid");
        await Assert.That(messages.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetMessages_NonexistentSession_ReturnsEmpty()
    {
        var messages = ClaudeAgentSdk.Sessions.Sessions.GetMessages("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        await Assert.That(messages.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ParseTranscriptEntries_SkipsNonTranscriptTypes()
    {
        string content = string.Join("\n",
            """{"type":"user","uuid":"u1","message":{}}""",
            """{"type":"custom-title","customTitle":"Test"}""",
            """{"type":"tag","tag":"v1"}""",
            """{"type":"assistant","uuid":"a1","message":{}}""");

        var entries = SessionMessageParser.ParseTranscriptEntries(content);

        await Assert.That(entries.Count).IsEqualTo(2); // only user + assistant
    }

    private void CreateSessionFile(string sessionId, string content)
    {
        string projectDir = Path.Combine(_tempDir, "projects", "test-project");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, $"{sessionId}.jsonl"), content + "\n");
    }
}
