using System.Text.Json;
using ClaudeAgentSdk.Hooks;

namespace ClaudeAgentSdk.Tests.Hooks;

public class HookOutputSerializationTests
{
    [Test]
    public async Task Empty_SerializesToEmptyObject()
    {
        var json = HookOutput.Empty.ToJsonElement();

        await Assert.That(json.ValueKind).IsEqualTo(JsonValueKind.Object);
        await Assert.That(json.EnumerateObject().Count()).IsEqualTo(0);
    }

    [Test]
    public async Task Continue_SerializesAsContinueKeyword()
    {
        var output = new HookOutput { Continue = true };
        var json = output.ToJsonElement();

        await Assert.That(json.GetProperty("continue").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task PreToolUseDeny_SerializesWithCamelCaseHookSpecificOutput()
    {
        var output = new HookOutput
        {
            HookSpecificOutput = new PreToolUseOutput
            {
                PermissionDecision = "deny",
                PermissionDecisionReason = "Blocked pattern",
            },
        };
        var json = output.ToJsonElement();

        var hso = json.GetProperty("hookSpecificOutput");
        await Assert.That(hso.GetProperty("hookEventName").GetString()).IsEqualTo("PreToolUse");
        await Assert.That(hso.GetProperty("permissionDecision").GetString()).IsEqualTo("deny");
        await Assert.That(hso.GetProperty("permissionDecisionReason").GetString()).IsEqualTo("Blocked pattern");
    }

    [Test]
    public async Task UserPromptSubmitOutput_SetsHookEventNameAutomatically()
    {
        var output = new HookOutput
        {
            HookSpecificOutput = new UserPromptSubmitOutput
            {
                AdditionalContext = "My favorite color is hot pink",
            },
        };
        var json = output.ToJsonElement();

        var hso = json.GetProperty("hookSpecificOutput");
        await Assert.That(hso.GetProperty("hookEventName").GetString()).IsEqualTo("UserPromptSubmit");
        await Assert.That(hso.GetProperty("additionalContext").GetString()).IsEqualTo("My favorite color is hot pink");
    }

    [Test]
    public async Task PostToolUseOutput_WithSystemMessageAndReason()
    {
        var output = new HookOutput
        {
            SystemMessage = "The command produced an error",
            Reason = "Tool execution failed",
            HookSpecificOutput = new PostToolUseOutput
            {
                AdditionalContext = "Try a different approach.",
            },
        };
        var json = output.ToJsonElement();

        await Assert.That(json.GetProperty("systemMessage").GetString()).IsEqualTo("The command produced an error");
        await Assert.That(json.GetProperty("reason").GetString()).IsEqualTo("Tool execution failed");

        var hso = json.GetProperty("hookSpecificOutput");
        await Assert.That(hso.GetProperty("hookEventName").GetString()).IsEqualTo("PostToolUse");
        await Assert.That(hso.GetProperty("additionalContext").GetString()).IsEqualTo("Try a different approach.");
    }

    [Test]
    public async Task NullProperties_AreOmitted()
    {
        var output = new HookOutput
        {
            SystemMessage = "warning",
        };
        var json = output.ToJsonElement();

        await Assert.That(json.TryGetProperty("continue", out _)).IsFalse();
        await Assert.That(json.TryGetProperty("stopReason", out _)).IsFalse();
        await Assert.That(json.TryGetProperty("hookSpecificOutput", out _)).IsFalse();
        await Assert.That(json.GetProperty("systemMessage").GetString()).IsEqualTo("warning");
    }

    [Test]
    public async Task StopOutput_SerializesCorrectly()
    {
        var output = new HookOutput
        {
            Continue = false,
            StopReason = "Critical error detected",
            HookSpecificOutput = new StopOutput
            {
                AdditionalContext = "Halted for safety",
            },
        };
        var json = output.ToJsonElement();

        await Assert.That(json.GetProperty("continue").GetBoolean()).IsFalse();
        await Assert.That(json.GetProperty("stopReason").GetString()).IsEqualTo("Critical error detected");
        await Assert.That(json.GetProperty("hookSpecificOutput").GetProperty("hookEventName").GetString()).IsEqualTo("Stop");
    }
}
