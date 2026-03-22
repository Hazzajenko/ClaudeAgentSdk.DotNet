using System.Text.Json;
using ClaudeAgentSdk.Sessions;

namespace ClaudeAgentSdk.Tests.Sessions;

public class SessionTypesTests
{
    [Test]
    public async Task SessionInfo_RequiredFields_Set()
    {
        var info = new SessionInfo
        {
            SessionId = "550e8400-e29b-41d4-a716-446655440000",
            Summary = "Test session",
            LastModified = 1700000000000
        };

        await Assert.That(info.SessionId).IsEqualTo("550e8400-e29b-41d4-a716-446655440000");
        await Assert.That(info.Summary).IsEqualTo("Test session");
        await Assert.That(info.LastModified).IsEqualTo(1700000000000);
        await Assert.That(info.FileSize).IsNull();
        await Assert.That(info.CustomTitle).IsNull();
        await Assert.That(info.Tag).IsNull();
    }

    [Test]
    public async Task SessionInfo_AllFields_Set()
    {
        var info = new SessionInfo
        {
            SessionId = "test-id",
            Summary = "My session",
            LastModified = 1700000000000,
            FileSize = 4096,
            CustomTitle = "Custom Title",
            FirstPrompt = "Hello",
            GitBranch = "main",
            Cwd = "/home/user/project",
            Tag = "experiment",
            CreatedAt = 1699999000000
        };

        await Assert.That(info.CustomTitle).IsEqualTo("Custom Title");
        await Assert.That(info.Tag).IsEqualTo("experiment");
        await Assert.That(info.GitBranch).IsEqualTo("main");
    }

    [Test]
    public async Task SessionMessage_Constructs()
    {
        using var doc = JsonDocument.Parse("""{"role":"user","content":"Hello"}""");
        var msg = new SessionMessage
        {
            Type = "user",
            Uuid = "msg-001",
            SessionId = "sess-001",
            Message = doc.RootElement.Clone()
        };

        await Assert.That(msg.Type).IsEqualTo("user");
        await Assert.That(msg.Uuid).IsEqualTo("msg-001");
        await Assert.That(msg.Message.GetProperty("role").GetString()).IsEqualTo("user");
    }
}
