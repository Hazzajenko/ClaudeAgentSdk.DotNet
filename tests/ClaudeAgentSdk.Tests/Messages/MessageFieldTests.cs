using System.Text.Json;
using ClaudeAgentSdk.Json;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Tests.Messages;

/// <summary>
/// Fields that the CLI sends on result, assistant, user and rate limit frames.
/// The JSON in each test is a frame in the shape the CLI writes to stdout.
/// </summary>
public class MessageFieldTests
{
    private const string ResultFrame = """
        {"type":"result","subtype":"error_during_execution","is_error":true,"duration_ms":3120,"duration_api_ms":2950,"num_turns":2,"result":"","stop_reason":null,"session_id":"7d4e9c1a-2b3f-4e5a-8c6d-1f2e3d4c5b6a","total_cost_usd":0.0421,"usage":{"input_tokens":12,"output_tokens":340},"modelUsage":{"claude-sonnet-4-5-20250929":{"inputTokens":12,"outputTokens":340,"cacheReadInputTokens":14200,"cacheCreationInputTokens":1800,"webSearchRequests":1,"costUSD":0.0421,"contextWindow":200000,"maxOutputTokens":64000,"canonicalModel":"claude-sonnet-4-5","provider":"firstParty"}},"permission_denials":[{"tool_name":"Bash","tool_use_id":"toolu_01ABC","tool_input":{"command":"rm -rf build"}}],"deferred_tool_use":{"id":"toolu_01DEF","name":"Write","input":{"file_path":"/tmp/a.txt","content":"x"}},"errors":["API Error: 529 Overloaded","retry limit reached"],"api_error_status":529,"terminal_reason":"aborted_tools","uuid":"0b1c2d3e-4f50-6172-8394-a5b6c7d8e9f0","origin":{"kind":"peer","from":"uds:/tmp/peer.sock","name":"builder","fromSession":"sess-42","senderTaskId":"task-7","body":"hello","verifiedPeerPid":4242,"futureKey":"kept"}}
        """;

    private static ResultMessage ParseResult(string json) =>
        (ResultMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

    [Test]
    public async Task ResultMessage_ReadsErrorsApiErrorStatusTerminalReasonAndUuid()
    {
        var result = ParseResult(ResultFrame);

        await Assert.That(result.Errors).IsNotNull();
        await Assert.That(result.Errors!).IsEquivalentTo(new[] { "API Error: 529 Overloaded", "retry limit reached" });
        await Assert.That(result.ApiErrorStatus).IsEqualTo(529);
        await Assert.That(result.TerminalReason).IsEqualTo("aborted_tools");
        await Assert.That(result.Uuid).IsEqualTo("0b1c2d3e-4f50-6172-8394-a5b6c7d8e9f0");
    }

    [Test]
    public async Task ResultMessage_ReadsModelUsageWithCamelCaseKeys()
    {
        var result = ParseResult(ResultFrame);

        await Assert.That(result.ModelUsage).IsNotNull();
        var usage = result.ModelUsage!["claude-sonnet-4-5-20250929"];
        await Assert.That(usage.InputTokens).IsEqualTo(12);
        await Assert.That(usage.OutputTokens).IsEqualTo(340);
        await Assert.That(usage.CacheReadInputTokens).IsEqualTo(14200);
        await Assert.That(usage.CacheCreationInputTokens).IsEqualTo(1800);
        await Assert.That(usage.WebSearchRequests).IsEqualTo(1);
        await Assert.That(usage.CostUsd).IsEqualTo(0.0421);
        await Assert.That(usage.ContextWindow).IsEqualTo(200000);
        await Assert.That(usage.MaxOutputTokens).IsEqualTo(64000);
        await Assert.That(usage.CanonicalModel).IsEqualTo("claude-sonnet-4-5");
        await Assert.That(usage.Provider).IsEqualTo("firstParty");
    }

    [Test]
    public async Task ResultMessage_KeepsPermissionDenialsAsJson()
    {
        var result = ParseResult(ResultFrame);

        await Assert.That(result.PermissionDenials).IsNotNull();
        var denials = result.PermissionDenials!.Value;
        await Assert.That(denials.ValueKind).IsEqualTo(JsonValueKind.Array);
        await Assert.That(denials.GetArrayLength()).IsEqualTo(1);
        await Assert.That(denials[0].GetProperty("tool_name").GetString()).IsEqualTo("Bash");
        await Assert.That(denials[0].GetProperty("tool_input").GetProperty("command").GetString()).IsEqualTo("rm -rf build");
    }

    [Test]
    public async Task ResultMessage_ReadsDeferredToolUse()
    {
        var result = ParseResult(ResultFrame);

        await Assert.That(result.DeferredToolUse).IsNotNull();
        var deferred = result.DeferredToolUse!;
        await Assert.That(deferred.Id).IsEqualTo("toolu_01DEF");
        await Assert.That(deferred.Name).IsEqualTo("Write");
        await Assert.That(deferred.Input.GetProperty("file_path").GetString()).IsEqualTo("/tmp/a.txt");
    }

    [Test]
    public async Task ResultMessage_ReadsOriginWithCamelCaseAndFromKeys()
    {
        var result = ParseResult(ResultFrame);

        await Assert.That(result.Origin).IsNotNull();
        var origin = result.Origin!;
        await Assert.That(origin.Kind).IsEqualTo("peer");
        await Assert.That(origin.From).IsEqualTo("uds:/tmp/peer.sock");
        await Assert.That(origin.Name).IsEqualTo("builder");
        await Assert.That(origin.FromSession).IsEqualTo("sess-42");
        await Assert.That(origin.SenderTaskId).IsEqualTo("task-7");
        await Assert.That(origin.Body).IsEqualTo("hello");
        await Assert.That(origin.VerifiedPeerPid).IsEqualTo(4242);
        await Assert.That(origin.ExtensionData!["futureKey"].GetString()).IsEqualTo("kept");
    }

    [Test]
    public async Task UserMessage_ReadsOrigin()
    {
        const string json = """
            {"type":"user","message":{"role":"user","content":"<task-notification>done</task-notification>"},"uuid":"u-1","session_id":"s-1","parent_tool_use_id":null,"origin":{"kind":"task-notification","subkind":"scheduled-trigger"}}
            """;

        var user = (UserMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(user.Origin).IsNotNull();
        await Assert.That(user.Origin!.Kind).IsEqualTo("task-notification");
        await Assert.That(user.Origin.Subkind).IsEqualTo("scheduled-trigger");
    }

    [Test]
    public async Task UserMessage_ChannelOrigin_ReadsServer()
    {
        const string json = """
            {"type":"user","message":{"role":"user","content":"ping"},"origin":{"kind":"channel","server":"slack"}}
            """;

        var user = (UserMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(user.Origin!.Server).IsEqualTo("slack");
    }

    [Test]
    public async Task AssistantMessage_ReadsMessageIdStopReasonSessionIdAndUuid()
    {
        const string json = """
            {"type":"assistant","message":{"id":"msg_01XYZ","type":"message","role":"assistant","model":"claude-sonnet-4-5-20250929","content":[{"type":"text","text":"Done."}],"stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":3,"output_tokens":5}},"parent_tool_use_id":null,"session_id":"7d4e9c1a-2b3f-4e5a-8c6d-1f2e3d4c5b6a","uuid":"a1b2c3d4-e5f6-4789-9abc-def012345678"}
            """;

        var assistant = (AssistantMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(assistant.MessageId).IsEqualTo("msg_01XYZ");
        await Assert.That(assistant.StopReason).IsEqualTo("end_turn");
        await Assert.That(assistant.SessionId).IsEqualTo("7d4e9c1a-2b3f-4e5a-8c6d-1f2e3d4c5b6a");
        await Assert.That(assistant.Uuid).IsEqualTo("a1b2c3d4-e5f6-4789-9abc-def012345678");
    }

    [Test]
    public async Task RateLimitInfo_RawKeepsTheWholeInfoObject()
    {
        const string json = """
            {"type":"rate_limit_event","rate_limit_info":{"status":"allowed_warning","resetsAt":1767225600,"rateLimitType":"five_hour","utilization":0.91,"isUsingOverage":false,"surpassedThreshold":0.9},"uuid":"r-1","session_id":"s-1"}
            """;

        var info = ((RateLimitEvent)ClaudeJsonOptions.DeserializeMessage(json)!).RateLimitInfo;

        await Assert.That(info.Raw).IsNotNull();
        var raw = info.Raw!.Value;
        await Assert.That(raw.GetProperty("status").GetString()).IsEqualTo("allowed_warning");
        await Assert.That(raw.GetProperty("resetsAt").GetInt64()).IsEqualTo(1767225600);
        await Assert.That(raw.GetProperty("isUsingOverage").GetBoolean()).IsFalse();
        await Assert.That(raw.GetProperty("surpassedThreshold").GetDouble()).IsEqualTo(0.9);
    }

    [Test]
    [Arguments("""{"server":"slack"}""")]
    [Arguments("""{"kind":7}""")]
    [Arguments("\"human\"")]
    [Arguments("null")]
    public async Task Origin_WithoutStringKind_IsNull(string origin)
    {
        string userJson = $$"""{"type":"user","message":{"content":"hi"},"origin":{{origin}}}""";
        string resultJson = $$"""{"type":"result","subtype":"success","is_error":false,"duration_ms":1,"duration_api_ms":1,"num_turns":1,"session_id":"s","origin":{{origin}}}""";

        var user = (UserMessage)ClaudeJsonOptions.DeserializeMessage(userJson)!;
        var result = ParseResult(resultJson);

        await Assert.That(user.Origin).IsNull();
        await Assert.That(result.Origin).IsNull();
    }
}
