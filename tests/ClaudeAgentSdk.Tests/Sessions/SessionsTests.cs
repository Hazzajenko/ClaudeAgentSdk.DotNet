using ClaudeAgentSdk.Sessions;

namespace ClaudeAgentSdk.Tests.Sessions;

[NotInParallel("sessions-env")]
public class SessionsTests
{
    private string _tempDir = null!;

    [Before(Test)]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"claude-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [After(Test)]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task List_WithSessions_ReturnsSorted()
    {
        // Create a fake project dir structure
        string projectDir = CreateProjectDir("test-project");

        CreateSessionFile(projectDir, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            """{"type":"user","uuid":"u1","message":{"role":"user","content":"First session"}}""",
            mtime: new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        CreateSessionFile(projectDir, "11111111-2222-3333-4444-555555555555",
            """{"type":"user","uuid":"u2","message":{"role":"user","content":"Second session"}}""",
            mtime: new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        // Override config dir
        Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", _tempDir);
        try
        {
            var sessions = ClaudeAgentSdk.Sessions.Sessions.List();

            await Assert.That(sessions.Count).IsEqualTo(2);
            // Newest first
            await Assert.That(sessions[0].LastModified).IsGreaterThanOrEqualTo(sessions[1].LastModified);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", null);
        }
    }

    [Test]
    public async Task List_WithLimit_RespectsLimit()
    {
        string projectDir = CreateProjectDir("test-project");

        CreateSessionFile(projectDir, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            """{"type":"user","uuid":"u1","message":{"role":"user","content":"Session 1"}}""");
        CreateSessionFile(projectDir, "11111111-2222-3333-4444-555555555555",
            """{"type":"user","uuid":"u2","message":{"role":"user","content":"Session 2"}}""");

        Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", _tempDir);
        try
        {
            var sessions = ClaudeAgentSdk.Sessions.Sessions.List(limit: 1);
            await Assert.That(sessions.Count).IsEqualTo(1);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", null);
        }
    }

    [Test]
    public async Task GetInfo_ExistingSession_ReturnsInfo()
    {
        string projectDir = CreateProjectDir("test-project");

        CreateSessionFile(projectDir, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            """{"type":"user","uuid":"u1","message":{"role":"user","content":"Hello"}}""");

        Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", _tempDir);
        try
        {
            var info = ClaudeAgentSdk.Sessions.Sessions.GetInfo("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

            await Assert.That(info).IsNotNull();
            await Assert.That(info!.SessionId).IsEqualTo("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            await Assert.That(info.Summary).IsEqualTo("Hello");
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", null);
        }
    }

    [Test]
    public async Task GetInfo_NonexistentSession_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", _tempDir);
        try
        {
            var info = ClaudeAgentSdk.Sessions.Sessions.GetInfo("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            await Assert.That(info).IsNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", null);
        }
    }

    [Test]
    public async Task GetInfo_InvalidUuid_ReturnsNull()
    {
        var info = ClaudeAgentSdk.Sessions.Sessions.GetInfo("not-a-uuid");
        await Assert.That(info).IsNull();
    }

    [Test]
    public async Task List_EmptyDir_ReturnsEmpty()
    {
        Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", _tempDir);
        try
        {
            var sessions = ClaudeAgentSdk.Sessions.Sessions.List();
            await Assert.That(sessions.Count).IsEqualTo(0);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", null);
        }
    }

    private string CreateProjectDir(string name)
    {
        string dir = Path.Combine(_tempDir, "projects", name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CreateSessionFile(string projectDir, string sessionId, string content, DateTime? mtime = null)
    {
        string filePath = Path.Combine(projectDir, $"{sessionId}.jsonl");
        File.WriteAllText(filePath, content + "\n");
        if (mtime is not null)
            File.SetLastWriteTimeUtc(filePath, mtime.Value);
    }
}
