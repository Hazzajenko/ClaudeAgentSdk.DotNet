namespace ClaudeAgentSdk.Tests.Sessions;

[NotInParallel("sessions-env")]
public class SessionDeleteTests
{
    private const string SessionId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    private string _tempDir = null!;

    [Before(Test)]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"claude-del-{Guid.NewGuid():N}");
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
    public async Task Delete_RemovesSessionFileAndSubagentsDirectory()
    {
        string projectDir = CreateProjectWithSession();
        string subagentsDir = Path.Combine(projectDir, SessionId, "subagents");
        Directory.CreateDirectory(subagentsDir);
        File.WriteAllText(Path.Combine(subagentsDir, "agent-a1.jsonl"), "{}\n");

        ClaudeAgentSdk.Sessions.Sessions.Delete(SessionId);

        await Assert.That(File.Exists(Path.Combine(projectDir, $"{SessionId}.jsonl"))).IsFalse();
        await Assert.That(Directory.Exists(Path.Combine(projectDir, SessionId))).IsFalse();
    }

    [Test]
    public async Task Delete_WithoutSubagentsDirectory_RemovesSessionFile()
    {
        string projectDir = CreateProjectWithSession();

        ClaudeAgentSdk.Sessions.Sessions.Delete(SessionId);

        await Assert.That(File.Exists(Path.Combine(projectDir, $"{SessionId}.jsonl"))).IsFalse();
    }

    [Test]
    public async Task Delete_WithDirectory_RemovesSessionInThatProject()
    {
        string projectPath = Path.Combine(_tempDir, "work", "my-project");
        string projectDir = Path.Combine(_tempDir, "projects",
            ClaudeAgentSdk.Sessions.SessionStorage.SanitizePath(Path.GetFullPath(projectPath)));
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, $"{SessionId}.jsonl"), SessionLine);

        ClaudeAgentSdk.Sessions.Sessions.Delete(SessionId, projectPath);

        await Assert.That(File.Exists(Path.Combine(projectDir, $"{SessionId}.jsonl"))).IsFalse();
    }

    [Test]
    public async Task Delete_LeavesOtherSessionsAlone()
    {
        string projectDir = CreateProjectWithSession();
        string other = Path.Combine(projectDir, "11111111-2222-3333-4444-555555555555.jsonl");
        File.WriteAllText(other, SessionLine);

        ClaudeAgentSdk.Sessions.Sessions.Delete(SessionId);

        await Assert.That(File.Exists(other)).IsTrue();
    }

    [Test]
    public void Delete_MissingSession_ThrowsFileNotFound()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "projects", "test-project"));

        Assert.Throws<FileNotFoundException>(() =>
            ClaudeAgentSdk.Sessions.Sessions.Delete(SessionId));
    }

    [Test]
    public void Delete_EmptySessionFile_ThrowsFileNotFound()
    {
        string projectDir = Path.Combine(_tempDir, "projects", "test-project");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, $"{SessionId}.jsonl"), "");

        Assert.Throws<FileNotFoundException>(() =>
            ClaudeAgentSdk.Sessions.Sessions.Delete(SessionId));
    }

    [Test]
    public void Delete_InvalidSessionId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            ClaudeAgentSdk.Sessions.Sessions.Delete("not-a-uuid"));
    }

    private const string SessionLine =
        """{"type":"user","uuid":"u1","message":{"role":"user","content":"Hello"}}""" + "\n";

    private string CreateProjectWithSession()
    {
        string projectDir = Path.Combine(_tempDir, "projects", "test-project");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(Path.Combine(projectDir, $"{SessionId}.jsonl"), SessionLine);
        return projectDir;
    }
}
