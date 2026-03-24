using System.Text.Json;
using ClaudeAgentSdk.Hooks;
using ClaudeAgentSdk.Json;

namespace ClaudeAgentSdk.Tests.Hooks;

public class HookInputDeserializationTests
{
    [Test]
    public async Task PreToolUseInput_DeserializesFromSnakeCaseJson()
    {
        using var doc = JsonDocument.Parse("""
            {
                "session_id": "abc-123",
                "transcript_path": "/tmp/transcript.jsonl",
                "cwd": "/home/user/project",
                "permission_mode": "default",
                "hook_event_name": "PreToolUse",
                "tool_name": "Bash",
                "tool_input": {"command": "echo hello"},
                "tool_use_id": "toolu_01abc"
            }
            """);

        var input = doc.RootElement.Deserialize(ClaudeJsonContext.Default.HookInput);

        await Assert.That(input).IsNotNull();
        await Assert.That(input).IsTypeOf<PreToolUseInput>();

        var pre = (PreToolUseInput)input!;
        await Assert.That(pre.SessionId).IsEqualTo("abc-123");
        await Assert.That(pre.Cwd).IsEqualTo("/home/user/project");
        await Assert.That(pre.ToolName).IsEqualTo("Bash");
        await Assert.That(pre.ToolUseId).IsEqualTo("toolu_01abc");
        await Assert.That(pre.ToolInput.GetProperty("command").GetString()).IsEqualTo("echo hello");
    }

    [Test]
    public async Task UserPromptSubmitInput_DeserializesPrompt()
    {
        using var doc = JsonDocument.Parse("""
            {
                "session_id": "sess-1",
                "transcript_path": "/tmp/t.jsonl",
                "cwd": "/home/user",
                "hook_event_name": "UserPromptSubmit",
                "prompt": "What's my favorite color?"
            }
            """);

        var input = doc.RootElement.Deserialize(ClaudeJsonContext.Default.HookInput);

        await Assert.That(input).IsTypeOf<UserPromptSubmitInput>();

        var ups = (UserPromptSubmitInput)input!;
        await Assert.That(ups.Prompt).IsEqualTo("What's my favorite color?");
        await Assert.That(ups.SessionId).IsEqualTo("sess-1");
    }

    [Test]
    public async Task PostToolUseInput_DeserializesToolResponse()
    {
        using var doc = JsonDocument.Parse("""
            {
                "session_id": "sess-2",
                "transcript_path": "/tmp/t.jsonl",
                "cwd": "/home/user",
                "hook_event_name": "PostToolUse",
                "tool_name": "Bash",
                "tool_input": {"command": "ls /nonexistent"},
                "tool_response": "ls: cannot access '/nonexistent': No such file or directory",
                "tool_use_id": "toolu_02xyz"
            }
            """);

        var input = doc.RootElement.Deserialize(ClaudeJsonContext.Default.HookInput);

        await Assert.That(input).IsTypeOf<PostToolUseInput>();

        var post = (PostToolUseInput)input!;
        await Assert.That(post.ToolName).IsEqualTo("Bash");
        await Assert.That(post.ToolUseId).IsEqualTo("toolu_02xyz");
        await Assert.That(post.ToolResponse.GetString()).IsEqualTo("ls: cannot access '/nonexistent': No such file or directory");
    }

    [Test]
    public async Task PostToolUseFailureInput_DeserializesError()
    {
        using var doc = JsonDocument.Parse("""
            {
                "session_id": "sess-3",
                "transcript_path": "/tmp/t.jsonl",
                "cwd": "/home/user",
                "hook_event_name": "PostToolUseFailure",
                "tool_name": "Write",
                "tool_input": {},
                "tool_use_id": "toolu_03",
                "error": "Permission denied"
            }
            """);

        var input = doc.RootElement.Deserialize(ClaudeJsonContext.Default.HookInput);

        await Assert.That(input).IsTypeOf<PostToolUseFailureInput>();
        await Assert.That(((PostToolUseFailureInput)input!).Error).IsEqualTo("Permission denied");
    }

    [Test]
    public async Task StopInput_DeserializesStopHookActive()
    {
        using var doc = JsonDocument.Parse("""
            {
                "session_id": "sess-4",
                "transcript_path": "/tmp/t.jsonl",
                "cwd": "/home/user",
                "hook_event_name": "Stop",
                "stop_hook_active": true
            }
            """);

        var input = doc.RootElement.Deserialize(ClaudeJsonContext.Default.HookInput);

        await Assert.That(input).IsTypeOf<StopInput>();
        await Assert.That(((StopInput)input!).StopHookActive).IsTrue();
    }

    [Test]
    public async Task NotificationInput_DeserializesAllFields()
    {
        using var doc = JsonDocument.Parse("""
            {
                "session_id": "sess-5",
                "transcript_path": "/tmp/t.jsonl",
                "cwd": "/home/user",
                "hook_event_name": "Notification",
                "message": "Task complete",
                "title": "Done",
                "notification_type": "info"
            }
            """);

        var input = doc.RootElement.Deserialize(ClaudeJsonContext.Default.HookInput);

        await Assert.That(input).IsTypeOf<NotificationInput>();
        var notif = (NotificationInput)input!;
        await Assert.That(notif.Message).IsEqualTo("Task complete");
        await Assert.That(notif.Title).IsEqualTo("Done");
        await Assert.That(notif.NotificationType).IsEqualTo("info");
    }

    [Test]
    public async Task SubagentStartInput_DeserializesAgentFields()
    {
        using var doc = JsonDocument.Parse("""
            {
                "session_id": "sess-6",
                "transcript_path": "/tmp/t.jsonl",
                "cwd": "/home/user",
                "hook_event_name": "SubagentStart",
                "agent_id": "agent-123",
                "agent_type": "research"
            }
            """);

        var input = doc.RootElement.Deserialize(ClaudeJsonContext.Default.HookInput);

        await Assert.That(input).IsTypeOf<SubagentStartInput>();
        var sa = (SubagentStartInput)input!;
        await Assert.That(sa.AgentId).IsEqualTo("agent-123");
        await Assert.That(sa.AgentType).IsEqualTo("research");
    }

    [Test]
    public async Task PreToolUseInput_WithOptionalAgentContext()
    {
        using var doc = JsonDocument.Parse("""
            {
                "session_id": "sess-7",
                "transcript_path": "/tmp/t.jsonl",
                "cwd": "/home/user",
                "hook_event_name": "PreToolUse",
                "tool_name": "Read",
                "tool_input": {},
                "tool_use_id": "toolu_04",
                "agent_id": "sub-1",
                "agent_type": "coding"
            }
            """);

        var input = doc.RootElement.Deserialize(ClaudeJsonContext.Default.HookInput);

        var pre = (PreToolUseInput)input!;
        await Assert.That(pre.AgentId).IsEqualTo("sub-1");
        await Assert.That(pre.AgentType).IsEqualTo("coding");
    }
}
