using ClaudeAgentSdk.Sessions;

namespace ClaudeAgentSdk.Tests.Sessions;

[NotInParallel("sessions-env")]
public class SessionMutationsTests
{
    private string _tempDir = null!;

    [Before(Test)]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"claude-mut-{Guid.NewGuid():N}");
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
    public async Task Rename_AppendsCustomTitleEntry()
    {
        string projectDir = CreateProjectWithSession("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        ClaudeAgentSdk.Sessions.Sessions.Rename("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "New Title");

        string content = File.ReadAllText(Path.Combine(projectDir, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee.jsonl"));
        await Assert.That(content).Contains("\"custom-title\"");
        await Assert.That(content).Contains("\"New Title\"");
    }

    [Test]
    public async Task Rename_TitleVisibleInGetInfo()
    {
        CreateProjectWithSession("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        ClaudeAgentSdk.Sessions.Sessions.Rename("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "Renamed Session");

        var info = ClaudeAgentSdk.Sessions.Sessions.GetInfo("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        await Assert.That(info).IsNotNull();
        await Assert.That(info!.CustomTitle).IsEqualTo("Renamed Session");
        await Assert.That(info.Summary).IsEqualTo("Renamed Session");
    }

    [Test]
    public async Task Tag_AppendsTagEntry()
    {
        string projectDir = CreateProjectWithSession("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        ClaudeAgentSdk.Sessions.Sessions.Tag("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "experiment");

        string content = File.ReadAllText(Path.Combine(projectDir, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee.jsonl"));
        await Assert.That(content).Contains("\"type\":\"tag\"");
        await Assert.That(content).Contains("\"experiment\"");
    }

    [Test]
    public async Task Tag_Null_ClearsTag()
    {
        string projectDir = CreateProjectWithSession("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        // Set then clear
        ClaudeAgentSdk.Sessions.Sessions.Tag("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "temp");
        ClaudeAgentSdk.Sessions.Sessions.Tag("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", null);

        var info = ClaudeAgentSdk.Sessions.Sessions.GetInfo("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        await Assert.That(info).IsNotNull();
        await Assert.That(info!.Tag).IsNull(); // empty tag = null
    }

    [Test]
    public async Task Rename_InvalidUuid_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ClaudeAgentSdk.Sessions.Sessions.Rename("not-a-uuid", "Title"));
        await Assert.That(ex.Message).Contains("Invalid session_id");
    }

    [Test]
    public async Task Rename_EmptyTitle_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ClaudeAgentSdk.Sessions.Sessions.Rename("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "  "));
        await Assert.That(ex.Message).Contains("non-empty");
    }

    [Test]
    public void Rename_NonexistentSession_ThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() =>
            ClaudeAgentSdk.Sessions.Sessions.Rename("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "Title"));
    }

    [Test]
    public async Task Rename_StripsZeroWidthChars()
    {
        string projectDir = CreateProjectWithSession("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        // U+200B zero-width space, U+200C zero-width non-joiner, U+200D zero-width joiner
        ClaudeAgentSdk.Sessions.Sessions.Rename("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "clean\u200Btitle\u200C\u200D");

        string content = File.ReadAllText(Path.Combine(projectDir, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee.jsonl"));
        await Assert.That(content).Contains("\"cleantitle\"");
    }

    [Test]
    public async Task Rename_StripsDirectionalMarks()
    {
        string projectDir = CreateProjectWithSession("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        // U+202A left-to-right embedding, U+202E right-to-left override
        ClaudeAgentSdk.Sessions.Sessions.Rename("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "dir\u202Atest\u202E");

        string content = File.ReadAllText(Path.Combine(projectDir, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee.jsonl"));
        await Assert.That(content).Contains("\"dirtest\"");
    }

    [Test]
    public async Task Rename_StripsFormatChars()
    {
        string projectDir = CreateProjectWithSession("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        // U+FEFF BOM / zero-width no-break space, U+00AD soft hyphen (Cf category)
        ClaudeAgentSdk.Sessions.Sessions.Rename("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "soft\u00ADhyphen\uFEFF");

        string content = File.ReadAllText(Path.Combine(projectDir, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee.jsonl"));
        await Assert.That(content).Contains("\"softhyphen\"");
    }

    [Test]
    public async Task Rename_StripsPrivateUseChars()
    {
        string projectDir = CreateProjectWithSession("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        // U+E000 is in private-use area (Co category)
        ClaudeAgentSdk.Sessions.Sessions.Rename("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "priv\uE000ate");

        string content = File.ReadAllText(Path.Combine(projectDir, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee.jsonl"));
        await Assert.That(content).Contains("\"private\"");
    }

    [Test]
    public async Task Rename_ThrowsWhenEmptyAfterSanitization()
    {
        CreateProjectWithSession("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        // All chars are dangerous — result is empty after sanitization+trim
        var ex = Assert.Throws<ArgumentException>(() =>
            ClaudeAgentSdk.Sessions.Sessions.Rename("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "\u200B\u200C\uFEFF"));
        await Assert.That(ex.Message).Contains("non-empty");
    }

    [Test]
    public async Task Tag_SanitizesUnicode()
    {
        string projectDir = CreateProjectWithSession("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        ClaudeAgentSdk.Sessions.Sessions.Tag("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "tag\u200Bvalue\uFEFF");

        string content = File.ReadAllText(Path.Combine(projectDir, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee.jsonl"));
        await Assert.That(content).Contains("\"tagvalue\"");
    }

    private string CreateProjectWithSession(string sessionId)
    {
        string projectDir = Path.Combine(_tempDir, "projects", "test-project");
        Directory.CreateDirectory(projectDir);
        string filePath = Path.Combine(projectDir, $"{sessionId}.jsonl");
        File.WriteAllText(filePath,
            """{"type":"user","uuid":"u1","message":{"role":"user","content":"Hello"}}""" + "\n");
        return projectDir;
    }
}
