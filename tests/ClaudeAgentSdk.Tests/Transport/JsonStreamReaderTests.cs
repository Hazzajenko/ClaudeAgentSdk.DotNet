using ClaudeAgentSdk.Exceptions;
using ClaudeAgentSdk.Messages;
using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk.Tests.Transport;

public class JsonStreamReaderTests
{
    [Test]
    public async Task ReadAllAsync_SingleAssistantMessage_Yields()
    {
        var input = """
            {"type":"assistant","message":{"content":[{"type":"text","text":"Hello"}],"model":"claude-sonnet-4-5-20250929"}}
            """;

        var messages = await ReadAll(input);

        await Assert.That(messages).Count().IsEqualTo(1);
        await Assert.That(messages[0]).IsTypeOf<AssistantMessage>();
    }

    [Test]
    public async Task ReadAllAsync_MultipleMessages_YieldsAll()
    {
        var input = """
            {"type":"assistant","message":{"content":[{"type":"text","text":"Hello"}],"model":"claude-sonnet-4-5-20250929"}}
            {"type":"result","subtype":"success","duration_ms":1000,"duration_api_ms":900,"is_error":false,"num_turns":1,"session_id":"s1"}
            """;

        var messages = await ReadAll(input);

        await Assert.That(messages).Count().IsEqualTo(2);
        await Assert.That(messages[0]).IsTypeOf<AssistantMessage>();
        await Assert.That(messages[1]).IsTypeOf<ResultMessage>();
    }

    [Test]
    public async Task ReadAllAsync_UnknownMessageType_SkippedAndNextMessageArrives()
    {
        var input = """
            {"type":"unknown_future_type","data":{"x":1}}
            {"type":"result","subtype":"success","duration_ms":1000,"duration_api_ms":900,"is_error":false,"num_turns":1,"session_id":"s1"}
            """;

        var messages = await ReadAll(input);

        await Assert.That(messages).Count().IsEqualTo(1);
        await Assert.That(messages[0]).IsTypeOf<ResultMessage>();
    }

    [Test]
    public async Task ReadAllAsync_ConversationReset_Yields()
    {
        var input = """
            {"type":"conversation_reset","new_conversation_id":"conv-002","uuid":"u1","session_id":"s1"}
            """;

        var messages = await ReadAll(input);

        await Assert.That(messages).Count().IsEqualTo(1);
        await Assert.That(messages[0]).IsTypeOf<ConversationResetMessage>();
    }

    [Test]
    public async Task ReadAllAsync_EmptyLines_Skipped()
    {
        var input = """

            {"type":"assistant","message":{"content":[{"type":"text","text":"Hello"}],"model":"claude-sonnet-4-5-20250929"}}

            {"type":"result","subtype":"success","duration_ms":1000,"duration_api_ms":900,"is_error":false,"num_turns":1,"session_id":"s1"}

            """;

        var messages = await ReadAll(input);

        await Assert.That(messages).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ReadAllAsync_EmptyInput_YieldsNothing()
    {
        var messages = await ReadAll("");

        await Assert.That(messages).Count().IsEqualTo(0);
    }

    [Test]
    public async Task ReadAllAsync_CancellationToken_StopsReading()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.That(async () => await ReadAll("not read", cts.Token))
            .ThrowsException()
            .WithMessageContaining("canceled");
    }

    [Test]
    public async Task ReadAllAsync_BufferExceeded_Throws()
    {
        // A single JSON line longer than the limit
        string longJson = "{\"type\":\"" + new string('x', 100) + "\"}";

        await Assert.That(async () =>
        {
            using var reader = new StringReader(longJson);
            await foreach (var _ in JsonStreamReader.ReadAllAsync(reader, maxBufferSize: 50))
            {
                // Should not reach here
            }
        }).ThrowsException();
    }

    [Test]
    public async Task ReadAllAsync_MixedMessageTypes_AllParsed()
    {
        var input = """
            {"type":"assistant","message":{"content":[{"type":"text","text":"thinking..."}],"model":"m"}}
            {"type":"system","subtype":"init"}
            {"type":"stream_event","uuid":"e1","session_id":"s1","event":{"type":"delta"}}
            {"type":"result","subtype":"done","duration_ms":100,"duration_api_ms":90,"is_error":false,"num_turns":1,"session_id":"s1"}
            """;

        var messages = await ReadAll(input);

        await Assert.That(messages).Count().IsEqualTo(4);
        await Assert.That(messages[0]).IsTypeOf<AssistantMessage>();
        await Assert.That(messages[1]).IsTypeOf<SystemMessage>();
        await Assert.That(messages[2]).IsTypeOf<StreamEvent>();
        await Assert.That(messages[3]).IsTypeOf<ResultMessage>();
    }

    [Test]
    public async Task ReadAllAsync_DeserializationError_Propagates()
    {
        // Valid JSON that fails mapping because a required field is missing. It must throw, not buffer.
        var input = """
            {"type":"assistant","message":{"content":[{"type":"text","text":"Hello"}],"model":"m"}}
            {"type":"conversation_reset","uuid":"u1","session_id":"s1"}
            {"type":"result","subtype":"success","duration_ms":1,"duration_api_ms":1,"is_error":false,"num_turns":1,"session_id":"s1"}
            """;

        var messages = new List<Message>();
        await Assert.That(async () =>
        {
            using var reader = new StringReader(input);
            await foreach (var msg in JsonStreamReader.ReadAllAsync(reader))
            {
                messages.Add(msg);
            }
        }).ThrowsException();

        // First message should have been yielded before the error
        await Assert.That(messages).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ReadAllAsync_NonJsonLineBetweenMessages_SkippedAndBothArrive()
    {
        var input = """
            {"type":"assistant","message":{"content":[{"type":"text","text":"Hello"}],"model":"m"}}
            [SandboxDebug] Seccomp filtering not available
            {"type":"result","subtype":"success","duration_ms":1,"duration_api_ms":1,"is_error":false,"num_turns":1,"session_id":"s1"}
            """;

        var messages = await ReadAll(input);

        await Assert.That(messages).Count().IsEqualTo(2);
        await Assert.That(messages[0]).IsTypeOf<AssistantMessage>();
        await Assert.That(messages[1]).IsTypeOf<ResultMessage>();
    }

    [Test]
    public async Task ReadAllAsync_MalformedJsonLine_ThrowsMessageParseExceptionWithLine()
    {
        var input = """
            {"type":"assistant","message":{"content":[{"type":"text","text":"Hello"}],"model":"m"}}
            {"type":"assistant","message":
            {"content":[]}}
            """;

        var messages = new List<Message>();
        MessageParseException? caught = null;
        try
        {
            using var reader = new StringReader(input);
            await foreach (var msg in JsonStreamReader.ReadAllAsync(reader))
            {
                messages.Add(msg);
            }
        }
        catch (MessageParseException ex)
        {
            caught = ex;
        }

        await Assert.That(caught).IsNotNull();
        await Assert.That(caught!.Line).IsEqualTo("""{"type":"assistant","message":""");
        await Assert.That(messages).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ReadAllAsync_LongNonJsonLine_SkippedWithoutBufferError()
    {
        string input = new string('x', 100) + "\n"
            + """{"type":"result","subtype":"success","duration_ms":1,"duration_api_ms":1,"is_error":false,"num_turns":1,"session_id":"s1"}""";

        using var reader = new StringReader(input);
        var messages = new List<Message>();
        await foreach (var msg in JsonStreamReader.ReadAllAsync(reader, maxBufferSize: 200))
        {
            messages.Add(msg);
        }

        await Assert.That(messages).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ReadAllAsync_LineUnderLimit_ResetsPerLine()
    {
        // Each line is under the limit. Together they are over it.
        var line = """{"type":"system","subtype":"init"}""";
        var input = string.Join("\n", line, line, line);

        using var reader = new StringReader(input);
        var messages = new List<Message>();
        await foreach (var msg in JsonStreamReader.ReadAllAsync(reader, maxBufferSize: line.Length))
        {
            messages.Add(msg);
        }

        await Assert.That(messages).Count().IsEqualTo(3);
    }

    private static async Task<List<Message>> ReadAll(string input, CancellationToken ct = default)
    {
        using var reader = new StringReader(input);
        var messages = new List<Message>();
        await foreach (var msg in JsonStreamReader.ReadAllAsync(reader, cancellationToken: ct))
        {
            messages.Add(msg);
        }
        return messages;
    }
}
