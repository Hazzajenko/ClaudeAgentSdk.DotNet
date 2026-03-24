using ClaudeAgentSdk;
using ClaudeAgentSdk.Messages;

namespace TestingWithTransport;

/// <summary>
/// Demonstrates testing Claude SDK queries by injecting a FakeTransport.
/// No CLI needed — all responses are canned JSON.
/// </summary>
public class BasicQueryTests
{
    [Test]
    public async Task QueryAsync_SimpleTextResponse_ReturnsAssistantMessage()
    {
        // Arrange: define the canned CLI output (JSON lines)
        var transport = FakeTransport.WithMessages("""
            {"type":"assistant","message":{"content":[{"type":"text","text":"The answer is 42."}],"model":"claude-sonnet-4-5-20250929"}}
            {"type":"result","subtype":"success","duration_ms":500,"duration_api_ms":450,"is_error":false,"num_turns":1,"session_id":"s1","result":"The answer is 42."}
            """);

        // Act: run query with the fake transport
        var messages = new List<Message>();
        await foreach (var msg in Claude.QueryAsync("What is the meaning of life?", transport: transport))
        {
            messages.Add(msg);
        }

        // Assert: verify the message stream
        await Assert.That(messages).Count().IsEqualTo(2);

        // First message: assistant with text
        await Assert.That(messages[0]).IsTypeOf<AssistantMessage>();
        var assistant = (AssistantMessage)messages[0];
        await Assert.That(assistant.Text).IsEqualTo("The answer is 42.");

        // Last message: result with metadata
        await Assert.That(messages[1]).IsTypeOf<ResultMessage>();
        var result = (ResultMessage)messages[1];
        await Assert.That(result.NumTurns).IsEqualTo(1);
        await Assert.That(result.IsError).IsFalse();
    }

    [Test]
    public async Task ClaudeClient_SendAsync_WithFakeTransport()
    {
        // Arrange
        var transport = FakeTransport.WithMessages("""
            {"type":"assistant","message":{"content":[{"type":"text","text":"Hello from ClaudeClient!"}],"model":"m"}}
            {"type":"result","subtype":"success","duration_ms":100,"duration_api_ms":90,"is_error":false,"num_turns":1,"session_id":"s1","result":"Hello from ClaudeClient!"}
            """);

        // Act: use the stateful ClaudeClient with injected transport
        await using var client = new ClaudeClient(transport: transport);
        var messages = new List<Message>();
        await foreach (var msg in client.SendAsync("hi"))
        {
            messages.Add(msg);
        }

        // Assert
        await Assert.That(messages.Count).IsGreaterThanOrEqualTo(1);
        var result = messages.OfType<ResultMessage>().First();
        await Assert.That(result.Result).IsEqualTo("Hello from ClaudeClient!");
    }

    [Test]
    public async Task QueryAsync_ToolUseFlow_ParsesAllMessageTypes()
    {
        // Simulate a tool use round-trip: assistant calls Read → user returns tool result → assistant responds
        var transport = FakeTransport.WithMessages("""
            {"type":"assistant","message":{"content":[{"type":"tool_use","id":"toolu_01","name":"Read","input":{"file_path":"/tmp/data.txt"}}],"model":"m"}}
            {"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"toolu_01","content":"file contents here"}]},"parent_tool_use_id":"toolu_01"}
            {"type":"assistant","message":{"content":[{"type":"text","text":"The file contains: file contents here"}],"model":"m"}}
            {"type":"result","subtype":"success","duration_ms":2000,"duration_api_ms":1800,"is_error":false,"num_turns":2,"session_id":"s1","result":"The file contains: file contents here"}
            """);

        var messages = new List<Message>();
        await foreach (var msg in Claude.QueryAsync("Read /tmp/data.txt", transport: transport))
        {
            messages.Add(msg);
        }

        // 4 messages: tool_use assistant → tool_result user → text assistant → result
        await Assert.That(messages).Count().IsEqualTo(4);
        await Assert.That(messages[0]).IsTypeOf<AssistantMessage>();
        await Assert.That(messages[1]).IsTypeOf<UserMessage>();
        await Assert.That(messages[2]).IsTypeOf<AssistantMessage>();
        await Assert.That(messages[3]).IsTypeOf<ResultMessage>();

        // Verify tool use block
        var toolUseMsg = (AssistantMessage)messages[0];
        await Assert.That(toolUseMsg.Content[0]).IsTypeOf<ToolUseBlock>();
        var toolUse = (ToolUseBlock)toolUseMsg.Content[0];
        await Assert.That(toolUse.Name).IsEqualTo("Read");

        // Verify result
        var result = (ResultMessage)messages[3];
        await Assert.That(result.NumTurns).IsEqualTo(2);
    }

    [Test]
    public async Task QueryAsync_ErrorResult_DetectsError()
    {
        var transport = FakeTransport.WithMessages("""
            {"type":"result","subtype":"error","duration_ms":100,"duration_api_ms":0,"is_error":true,"num_turns":0,"session_id":"s1"}
            """);

        var messages = new List<Message>();
        await foreach (var msg in Claude.QueryAsync("fail", transport: transport))
        {
            messages.Add(msg);
        }

        var result = messages.OfType<ResultMessage>().First();
        await Assert.That(result.IsError).IsTrue();
    }

    [Test]
    public async Task FakeTransport_CapturesWrittenMessages_ForPromptAssertions()
    {
        var transport = FakeTransport.WithMessages("""
            {"type":"result","subtype":"success","duration_ms":0,"duration_api_ms":0,"is_error":false,"num_turns":0,"session_id":"s1"}
            """);

        await foreach (var _ in Claude.QueryAsync("my specific prompt", transport: transport)) { }

        // WrittenMessages captures everything sent to the transport
        // [0] = init handshake, [1] = user message with prompt
        await Assert.That(transport.WrittenMessages.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(transport.WrittenMessages[1]).Contains("my specific prompt");
    }
}
