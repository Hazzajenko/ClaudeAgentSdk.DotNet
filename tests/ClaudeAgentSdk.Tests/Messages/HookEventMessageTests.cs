using ClaudeAgentSdk.Json;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Tests.Messages;

public class HookEventMessageTests
{
    [Test]
    public async Task HookStarted_Deserializes()
    {
        const string json = """{"type":"system","subtype":"hook_started","hook_id":"h-1","hook_name":"SessionStart:startup","hook_event":"SessionStart","uuid":"msg-001","session_id":"sess-001"}""";

        var msg = ClaudeJsonOptions.DeserializeMessage(json);

        await Assert.That(msg).IsTypeOf<HookEventMessage>();
        var hook = (HookEventMessage)msg!;
        await Assert.That(hook.Subtype).IsEqualTo("hook_started");
        await Assert.That(hook.HookEventName).IsEqualTo("SessionStart");
        await Assert.That(hook.Uuid).IsEqualTo("msg-001");
        await Assert.That(hook.SessionId).IsEqualTo("sess-001");
        await Assert.That(hook.Data.GetProperty("hook_id").GetString()).IsEqualTo("h-1");
    }

    [Test]
    public async Task HookResponse_Deserializes()
    {
        const string json = """{"type":"system","subtype":"hook_response","hook_event":"PreToolUse","output":"ok","exit_code":0,"outcome":"success"}""";

        var msg = ClaudeJsonOptions.DeserializeMessage(json);

        await Assert.That(msg).IsTypeOf<HookEventMessage>();
        var hook = (HookEventMessage)msg!;
        await Assert.That(hook.Subtype).IsEqualTo("hook_response");
        await Assert.That(hook.HookEventName).IsEqualTo("PreToolUse");
        await Assert.That(hook.Uuid).IsNull();
        await Assert.That(hook.SessionId).IsNull();
        await Assert.That(hook.Data.GetProperty("outcome").GetString()).IsEqualTo("success");
    }

    [Test]
    public async Task HookEventName_FallsBackToHookName()
    {
        const string json = """{"type":"system","subtype":"hook_started","hook_name":"Stop"}""";

        var hook = (HookEventMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(hook.HookEventName).IsEqualTo("Stop");
    }

    [Test]
    public async Task HookEventName_FallsBackToHookEventName()
    {
        const string json = """{"type":"system","subtype":"hook_started","hook_event":"","hook_event_name":"Setup"}""";

        var hook = (HookEventMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(hook.HookEventName).IsEqualTo("Setup");
    }

    [Test]
    public async Task HookEventName_NoNameField_IsEmpty()
    {
        const string json = """{"type":"system","subtype":"hook_started","hook_event":42}""";

        var hook = (HookEventMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(hook.HookEventName).IsEqualTo("");
    }

    [Test]
    public async Task HookEventMessage_IsSystemMessage()
    {
        const string json = """{"type":"system","subtype":"hook_started","hook_event":"SessionStart"}""";

        var msg = ClaudeJsonOptions.DeserializeMessage(json);

        await Assert.That(msg is SystemMessage).IsTrue();
    }

    [Test]
    public async Task FormatMessage_HookEvent_ShowsSubtypeAndName()
    {
        const string json = """{"type":"system","subtype":"hook_response","hook_event":"PreToolUse"}""";

        var msg = ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(Claude.FormatMessage(msg)).IsEqualTo("[hook_response] PreToolUse");
    }
}
