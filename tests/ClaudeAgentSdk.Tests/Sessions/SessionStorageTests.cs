using ClaudeAgentSdk.Sessions;

namespace ClaudeAgentSdk.Tests.Sessions;

public class SessionStorageTests
{
    [Test]
    public async Task ValidateUuid_ValidUuid_ReturnsUuid()
    {
        string? result = SessionStorage.ValidateUuid("550e8400-e29b-41d4-a716-446655440000");
        await Assert.That(result).IsEqualTo("550e8400-e29b-41d4-a716-446655440000");
    }

    [Test]
    public async Task ValidateUuid_InvalidString_ReturnsNull()
    {
        await Assert.That(SessionStorage.ValidateUuid("not-a-uuid")).IsNull();
        await Assert.That(SessionStorage.ValidateUuid("")).IsNull();
    }

    [Test]
    public async Task SimpleHash_MatchesJsOutput()
    {
        // Known values — must match JS simpleHash behavior
        string hash = SessionStorage.SimpleHash("test");
        await Assert.That(hash).IsNotEmpty();
        // Deterministic
        await Assert.That(SessionStorage.SimpleHash("test")).IsEqualTo(hash);
    }

    [Test]
    public async Task SanitizePath_ReplacesNonAlphanumeric()
    {
        await Assert.That(SessionStorage.SanitizePath("/home/user/project"))
            .IsEqualTo("-home-user-project");
    }

    [Test]
    public async Task SanitizePath_LongPath_TruncatesWithHash()
    {
        string longPath = new('a', 300);
        string result = SessionStorage.SanitizePath(longPath);
        await Assert.That(result.Length).IsLessThan(300);
        await Assert.That(result).StartsWith(new string('a', 200));
        await Assert.That(result).Contains("-"); // hash suffix
    }

    [Test]
    public async Task SanitizePath_ShortPath_NoHash()
    {
        string result = SessionStorage.SanitizePath("short");
        await Assert.That(result).IsEqualTo("short");
    }

    [Test]
    public async Task ExtractJsonStringField_FindsField()
    {
        string json = """{"type":"user","uuid":"abc-123","model":"claude"}""";
        await Assert.That(SessionStorage.ExtractJsonStringField(json, "uuid")).IsEqualTo("abc-123");
        await Assert.That(SessionStorage.ExtractJsonStringField(json, "type")).IsEqualTo("user");
    }

    [Test]
    public async Task ExtractJsonStringField_WithSpace_FindsField()
    {
        string json = """{"type": "user", "uuid": "abc-123"}""";
        await Assert.That(SessionStorage.ExtractJsonStringField(json, "uuid")).IsEqualTo("abc-123");
    }

    [Test]
    public async Task ExtractJsonStringField_Missing_ReturnsNull()
    {
        string json = """{"type":"user"}""";
        await Assert.That(SessionStorage.ExtractJsonStringField(json, "missing")).IsNull();
    }

    [Test]
    public async Task ExtractLastJsonStringField_FindsLastOccurrence()
    {
        string text = """
            {"customTitle":"First Title"}
            {"customTitle":"Last Title"}
            """;
        await Assert.That(SessionStorage.ExtractLastJsonStringField(text, "customTitle"))
            .IsEqualTo("Last Title");
    }

    [Test]
    public async Task ExtractFirstPromptFromHead_FindsUserPrompt()
    {
        string head = """
            {"type":"user","uuid":"u1","message":{"role":"user","content":"Hello Claude"}}
            {"type":"assistant","uuid":"a1","message":{"role":"assistant","content":"Hi!"}}
            """;
        await Assert.That(SessionStorage.ExtractFirstPromptFromHead(head))
            .IsEqualTo("Hello Claude");
    }

    [Test]
    public async Task ExtractFirstPromptFromHead_SkipsToolResults()
    {
        string head = """
            {"type":"user","uuid":"u1","message":{"role":"user","content":[{"type":"tool_result","tool_use_id":"t1","content":"file"}]}}
            {"type":"user","uuid":"u2","message":{"role":"user","content":"Real prompt"}}
            """;
        await Assert.That(SessionStorage.ExtractFirstPromptFromHead(head))
            .IsEqualTo("Real prompt");
    }

    [Test]
    public async Task ExtractFirstPromptFromHead_TruncatesLongPrompts()
    {
        string longContent = new('x', 300);
        string head = $$$"""{"type":"user","uuid":"u1","message":{"role":"user","content":"{{{longContent}}}"}}""";
        string result = SessionStorage.ExtractFirstPromptFromHead(head);
        await Assert.That(result.Length).IsLessThanOrEqualTo(201); // 200 + ellipsis
        await Assert.That(result).EndsWith("\u2026");
    }

    [Test]
    public async Task ParseSessionInfoFromLite_BasicSession()
    {
        string head = """{"type":"user","uuid":"u1","sessionId":"s1","message":{"role":"user","content":"Hello"},"timestamp":"2024-01-01T00:00:00Z"}""";
        var lite = new SessionStorage.LiteSessionFile(1700000000000, 1024, head, head);

        var info = SessionStorage.ParseSessionInfoFromLite("test-uuid-1234-5678-90ab-cdef12345678", lite);

        await Assert.That(info).IsNotNull();
        await Assert.That(info!.Summary).IsEqualTo("Hello");
        await Assert.That(info.FirstPrompt).IsEqualTo("Hello");
        await Assert.That(info.FileSize).IsEqualTo(1024);
    }

    [Test]
    public async Task ParseSessionInfoFromLite_WithCustomTitle_UsesTitleAsSummary()
    {
        string head = """{"type":"user","uuid":"u1","message":{"role":"user","content":"Hello"}}""";
        string tail = """{"type":"custom-title","customTitle":"My Custom Title","sessionId":"s1"}""";
        var lite = new SessionStorage.LiteSessionFile(1700000000000, 1024, head, tail);

        var info = SessionStorage.ParseSessionInfoFromLite("test-uuid-1234-5678-90ab-cdef12345678", lite);

        await Assert.That(info).IsNotNull();
        await Assert.That(info!.Summary).IsEqualTo("My Custom Title");
        await Assert.That(info.CustomTitle).IsEqualTo("My Custom Title");
    }

    [Test]
    public async Task ParseSessionInfoFromLite_SidechainSession_ReturnsNull()
    {
        string head = """{"type":"user","isSidechain":true,"uuid":"u1","message":{"role":"user","content":"Hi"}}""";
        var lite = new SessionStorage.LiteSessionFile(1700000000000, 100, head, head);

        var info = SessionStorage.ParseSessionInfoFromLite("test-uuid-1234-5678-90ab-cdef12345678", lite);

        await Assert.That(info).IsNull();
    }

    [Test]
    public async Task ParseSessionInfoFromLite_WithTag_ExtractsTag()
    {
        string head = """{"type":"user","uuid":"u1","message":{"role":"user","content":"Hello"}}""";
        string tail = """{"type":"tag","tag":"experiment","sessionId":"s1"}""";
        var lite = new SessionStorage.LiteSessionFile(1700000000000, 1024, head, tail);

        var info = SessionStorage.ParseSessionInfoFromLite("test-uuid-1234-5678-90ab-cdef12345678", lite);

        await Assert.That(info).IsNotNull();
        await Assert.That(info!.Tag).IsEqualTo("experiment");
    }

    [Test]
    public async Task ReadSessionLite_WithTempFile_ReadsHeadAndTail()
    {
        string tempFile = Path.GetTempFileName();
        try
        {
            string content = """{"type":"user","uuid":"u1","message":{"role":"user","content":"Hello"}}""" + "\n";
            await File.WriteAllTextAsync(tempFile, content);

            var lite = SessionStorage.ReadSessionLite(tempFile);

            await Assert.That(lite).IsNotNull();
            await Assert.That(lite!.Head).Contains("Hello");
            await Assert.That(lite.Size).IsGreaterThan(0);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public async Task ReadSessionLite_NonexistentFile_ReturnsNull()
    {
        var lite = SessionStorage.ReadSessionLite("/nonexistent/path/file.jsonl");
        await Assert.That(lite).IsNull();
    }
}
