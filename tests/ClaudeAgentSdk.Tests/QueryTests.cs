using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeAgentSdk.Messages;
using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk.Tests;

/// <summary>
/// Tests for the query flow using mock transport (synthetic CLI output).
/// </summary>
public class QueryTests
{
    private const string StructuredOutputFlow = """
        {"type":"assistant","message":{"content":[{"type":"tool_use","id":"so_1","name":"StructuredOutput","input":{"content":{"name":"Alice","age":30}}}],"model":"claude-sonnet-4-5-20250929"}}
        {"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"so_1","content":"Structured output provided successfully"}]},"parent_tool_use_id":"so_1"}
        {"type":"assistant","message":{"content":[{"type":"text","text":"Extracted the person info."}],"model":"claude-sonnet-4-5-20250929"}}
        {"type":"result","subtype":"success","duration_ms":200,"duration_api_ms":180,"is_error":false,"num_turns":2,"session_id":"s1","structured_output":{"name":"Alice","age":30}}
        """;

    [Test]
    public async Task QueryCoreAsync_DoesNotCloseStdinBeforeResult()
    {
        var transport = new Transport.MockTransport(StructuredOutputFlow);

        var messages = new List<Message>();
        await foreach (var msg in Claude.QueryCoreAsync("Extract person info", transport))
        {
            messages.Add(msg);
        }

        await Assert.That(transport.EndInputCalled).IsFalse();
        await Assert.That(messages.OfType<ResultMessage>().First().StructuredOutput).IsNotNull();
    }

    [Test]
    public async Task QueryCoreAsync_StopsAfterResultMessage()
    {
        // Messages after ResultMessage should not be yielded
        var input = """
            {"type":"assistant","message":{"content":[{"type":"text","text":"first"}],"model":"m"}}
            {"type":"result","subtype":"success","duration_ms":100,"duration_api_ms":90,"is_error":false,"num_turns":1,"session_id":"s1"}
            {"type":"assistant","message":{"content":[{"type":"text","text":"should not appear"}],"model":"m"}}
            """;

        var transport = new Transport.MockTransport(input);
        var messages = new List<Message>();
        await foreach (var msg in Claude.QueryCoreAsync("test", transport))
        {
            messages.Add(msg);
        }

        await Assert.That(messages).Count().IsEqualTo(2);
        await Assert.That(messages[0]).IsTypeOf<AssistantMessage>();
        await Assert.That(messages[1]).IsTypeOf<ResultMessage>();
    }

    [Test]
    public async Task QueryFlow_AssistantThenResult_CollectsTextFromAssistant()
    {
        var input = """
            {"type":"assistant","message":{"content":[{"type":"text","text":"The answer is 42."}],"model":"claude-sonnet-4-5-20250929"}}
            {"type":"result","subtype":"success","duration_ms":1000,"duration_api_ms":900,"is_error":false,"num_turns":1,"session_id":"s1","result":"The answer is 42."}
            """;

        string result = await SimulateQueryText(input);

        await Assert.That(result).IsEqualTo("The answer is 42.");
    }

    [Test]
    public async Task QueryFlow_MultipleAssistantMessages_UsesLastText()
    {
        var input = """
            {"type":"assistant","message":{"content":[{"type":"thinking","thinking":"Let me think...","signature":"sig1"}],"model":"m"}}
            {"type":"assistant","message":{"content":[{"type":"text","text":"First response"}],"model":"m"}}
            {"type":"assistant","message":{"content":[{"type":"text","text":"Final response"}],"model":"m"}}
            {"type":"result","subtype":"success","duration_ms":1000,"duration_api_ms":900,"is_error":false,"num_turns":1,"session_id":"s1"}
            """;

        string result = await SimulateQueryText(input);

        await Assert.That(result).IsEqualTo("Final response");
    }

    [Test]
    public async Task QueryFlow_ResultWithResultField_PrefersResultField()
    {
        var input = """
            {"type":"assistant","message":{"content":[{"type":"text","text":"streaming text"}],"model":"m"}}
            {"type":"result","subtype":"success","duration_ms":1000,"duration_api_ms":900,"is_error":false,"num_turns":1,"session_id":"s1","result":"final result text"}
            """;

        string result = await SimulateQueryText(input);

        await Assert.That(result).IsEqualTo("final result text");
    }

    [Test]
    public async Task QueryFlow_NoText_ReturnsEmpty()
    {
        var input = """
            {"type":"result","subtype":"success","duration_ms":0,"duration_api_ms":0,"is_error":true,"num_turns":0,"session_id":"s1"}
            """;

        string result = await SimulateQueryText(input);

        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task QueryFlow_ToolUseAndResult_ParsesAllMessages()
    {
        var input = """
            {"type":"assistant","message":{"content":[{"type":"tool_use","id":"t1","name":"Read","input":{"file_path":"/tmp/test"}}],"model":"m"}}
            {"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"t1","content":"file contents"}]},"parent_tool_use_id":"t1"}
            {"type":"assistant","message":{"content":[{"type":"text","text":"I read the file."}],"model":"m"}}
            {"type":"result","subtype":"success","duration_ms":2000,"duration_api_ms":1800,"is_error":false,"num_turns":2,"session_id":"s1"}
            """;

        using var reader = new StringReader(input);
        var messages = new List<Message>();
        await foreach (var msg in JsonStreamReader.ReadAllAsync(reader))
        {
            messages.Add(msg);
        }

        await Assert.That(messages).Count().IsEqualTo(4);
        await Assert.That(messages[0]).IsTypeOf<AssistantMessage>();
        await Assert.That(messages[1]).IsTypeOf<UserMessage>();
        await Assert.That(messages[2]).IsTypeOf<AssistantMessage>();
        await Assert.That(messages[3]).IsTypeOf<ResultMessage>();

        // Verify tool use round trip
        var toolUse = ((AssistantMessage)messages[0]).Content[0] as ToolUseBlock;
        await Assert.That(toolUse!.Name).IsEqualTo("Read");
    }

    [Test]
    public async Task QueryFlow_StructuredOutput_DeserializesToType()
    {
        var input = """
            {"type":"assistant","message":{"content":[{"type":"text","text":"{}"}],"model":"m"}}
            {"type":"result","subtype":"success","duration_ms":100,"duration_api_ms":90,"is_error":false,"num_turns":1,"session_id":"s1","structured_output":{"name":"Alice","age":30}}
            """;

        using var reader = new StringReader(input);
        PersonDto? result = null;

        await foreach (var message in JsonStreamReader.ReadAllAsync(reader))
        {
            if (message is ResultMessage { StructuredOutput: { } output })
            {
                result = output.Deserialize(TestJsonContext.Default.PersonDto);
            }
        }

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("Alice");
        await Assert.That(result.Age).IsEqualTo(30);
    }

    [Test]
    public async Task QueryFlow_NoStructuredOutput_ReturnsNull()
    {
        var input = """
            {"type":"result","subtype":"success","duration_ms":0,"duration_api_ms":0,"is_error":false,"num_turns":0,"session_id":"s1"}
            """;

        using var reader = new StringReader(input);
        PersonDto? result = null;

        await foreach (var message in JsonStreamReader.ReadAllAsync(reader))
        {
            if (message is ResultMessage { StructuredOutput: { } output })
            {
                result = output.Deserialize(TestJsonContext.Default.PersonDto);
            }
        }

        await Assert.That(result).IsNull();
    }

    /// <summary>
    /// Simulates QueryTextAsync logic: reads all messages, returns final text.
    /// </summary>
    private static async Task<string> SimulateQueryText(string cliOutput)
    {
        string? resultText = null;
        using var reader = new StringReader(cliOutput);

        await foreach (var message in JsonStreamReader.ReadAllAsync(reader))
        {
            switch (message)
            {
                case ResultMessage { Result: not null } result:
                    resultText = result.Result;
                    break;
                case AssistantMessage assistant:
                    foreach (var block in assistant.Content)
                    {
                        if (block is TextBlock textBlock)
                        {
                            resultText = textBlock.Text;
                        }
                    }
                    break;
            }
        }

        return resultText ?? "";
    }
}

public sealed record PersonDto
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
    [JsonPropertyName("age")]
    public int Age { get; init; }
}

[JsonSerializable(typeof(PersonDto))]
internal sealed partial class TestJsonContext : JsonSerializerContext;
