using ClaudeAgentSdk.Json;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Tests.Messages;

public class MessageDeserializationTests
{
    [Test]
    public async Task AssistantMessage_WithTextBlock_Deserializes()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [
                        {"type": "text", "text": "Hello, world!"}
                    ],
                    "model": "claude-sonnet-4-5-20250929"
                }
            }
            """;

        var msg = ClaudeJsonOptions.DeserializeMessage(json);

        await Assert.That(msg).IsTypeOf<AssistantMessage>();
        var assistant = (AssistantMessage)msg!;
        await Assert.That(assistant.Content).Count().IsEqualTo(1);
        await Assert.That(assistant.Content[0]).IsTypeOf<TextBlock>();
        await Assert.That(((TextBlock)assistant.Content[0]).Text).IsEqualTo("Hello, world!");
        await Assert.That(assistant.Model).IsEqualTo("claude-sonnet-4-5-20250929");
    }

    [Test]
    public async Task AssistantMessage_WithToolUseBlock_Deserializes()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [
                        {
                            "type": "tool_use",
                            "id": "toolu_01ABC",
                            "name": "Read",
                            "input": {"file_path": "/tmp/test.txt"}
                        }
                    ],
                    "model": "claude-sonnet-4-5-20250929"
                }
            }
            """;

        var msg = ClaudeJsonOptions.DeserializeMessage(json);

        await Assert.That(msg).IsTypeOf<AssistantMessage>();
        var assistant = (AssistantMessage)msg!;
        var toolUse = assistant.Content[0] as ToolUseBlock;
        await Assert.That(toolUse).IsNotNull();
        await Assert.That(toolUse!.Id).IsEqualTo("toolu_01ABC");
        await Assert.That(toolUse.Name).IsEqualTo("Read");
        await Assert.That(toolUse.Input.GetProperty("file_path").GetString()).IsEqualTo("/tmp/test.txt");
    }

    [Test]
    public async Task AssistantMessage_WithThinkingBlock_Deserializes()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [
                        {
                            "type": "thinking",
                            "thinking": "Let me think about this...",
                            "signature": "sig_abc123"
                        },
                        {"type": "text", "text": "Here's my answer."}
                    ],
                    "model": "claude-opus-4-5-20250929"
                }
            }
            """;

        var msg = ClaudeJsonOptions.DeserializeMessage(json);

        await Assert.That(msg).IsTypeOf<AssistantMessage>();
        var assistant = (AssistantMessage)msg!;
        await Assert.That(assistant.Content).Count().IsEqualTo(2);
        var thinking = assistant.Content[0] as ThinkingBlock;
        await Assert.That(thinking).IsNotNull();
        await Assert.That(thinking!.Thinking).IsEqualTo("Let me think about this...");
        await Assert.That(thinking.Signature).IsEqualTo("sig_abc123");
    }

    [Test]
    public async Task AssistantMessage_WithOptionalFields_Deserializes()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [{"type": "text", "text": "hi"}],
                    "model": "claude-sonnet-4-5-20250929",
                    "usage": {"input_tokens": 10, "output_tokens": 5}
                },
                "parent_tool_use_id": "toolu_parent",
                "error": "rate_limit"
            }
            """;

        var msg = (AssistantMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(msg.ParentToolUseId).IsEqualTo("toolu_parent");
        await Assert.That(msg.Error).IsEqualTo("rate_limit");
        await Assert.That(msg.Usage).IsNotNull();
        await Assert.That(msg.Usage!.Value.GetProperty("input_tokens").GetInt32()).IsEqualTo(10);
    }

    [Test]
    public async Task UserMessage_WithStringContent_Deserializes()
    {
        const string json = """
            {
                "type": "user",
                "message": {
                    "content": "Hello Claude"
                },
                "uuid": "msg-001"
            }
            """;

        var msg = ClaudeJsonOptions.DeserializeMessage(json);

        await Assert.That(msg).IsTypeOf<UserMessage>();
        var user = (UserMessage)msg!;
        await Assert.That(user.Content).IsTypeOf<string>();
        await Assert.That((string)user.Content).IsEqualTo("Hello Claude");
        await Assert.That(user.Uuid).IsEqualTo("msg-001");
    }

    [Test]
    public async Task UserMessage_WithToolResultContent_Deserializes()
    {
        const string json = """
            {
                "type": "user",
                "message": {
                    "content": [
                        {
                            "type": "tool_result",
                            "tool_use_id": "toolu_01ABC",
                            "content": "file contents here",
                            "is_error": false
                        }
                    ]
                },
                "parent_tool_use_id": "toolu_parent"
            }
            """;

        var msg = (UserMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(msg.Content).IsAssignableTo<IReadOnlyList<ContentBlock>>();
        await Assert.That(msg.ParentToolUseId).IsEqualTo("toolu_parent");
    }

    [Test]
    public async Task SystemMessage_Deserializes()
    {
        const string json = """
            {
                "type": "system",
                "subtype": "init",
                "session_id": "sess-001"
            }
            """;

        var msg = ClaudeJsonOptions.DeserializeMessage(json);

        await Assert.That(msg).IsTypeOf<SystemMessage>();
        var system = (SystemMessage)msg!;
        await Assert.That(system.Subtype).IsEqualTo("init");
    }

    [Test]
    public async Task ResultMessage_Deserializes()
    {
        const string json = """
            {
                "type": "result",
                "subtype": "success",
                "duration_ms": 5000,
                "duration_api_ms": 4500,
                "is_error": false,
                "num_turns": 3,
                "session_id": "sess-001",
                "total_cost_usd": 0.05,
                "result": "Task completed successfully",
                "stop_reason": "end_turn"
            }
            """;

        var msg = ClaudeJsonOptions.DeserializeMessage(json);

        await Assert.That(msg).IsTypeOf<ResultMessage>();
        var result = (ResultMessage)msg!;
        await Assert.That(result.DurationMs).IsEqualTo(5000);
        await Assert.That(result.DurationApiMs).IsEqualTo(4500);
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.NumTurns).IsEqualTo(3);
        await Assert.That(result.SessionId).IsEqualTo("sess-001");
        await Assert.That(result.TotalCostUsd).IsEqualTo(0.05);
        await Assert.That(result.Result).IsEqualTo("Task completed successfully");
        await Assert.That(result.StopReason).IsEqualTo("end_turn");
    }

    [Test]
    public async Task ResultMessage_WithNullOptionals_Deserializes()
    {
        const string json = """
            {
                "type": "result",
                "subtype": "success",
                "duration_ms": 1000,
                "duration_api_ms": 900,
                "is_error": false,
                "num_turns": 1,
                "session_id": "sess-002"
            }
            """;

        var result = (ResultMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(result.TotalCostUsd).IsNull();
        await Assert.That(result.StopReason).IsNull();
        await Assert.That(result.Result).IsNull();
    }

    [Test]
    public async Task StreamEvent_Deserializes()
    {
        const string json = """
            {
                "type": "stream_event",
                "uuid": "evt-001",
                "session_id": "sess-001",
                "event": {"type": "content_block_delta", "delta": {"type": "text_delta", "text": "Hi"}}
            }
            """;

        var msg = ClaudeJsonOptions.DeserializeMessage(json);

        await Assert.That(msg).IsTypeOf<StreamEvent>();
        var evt = (StreamEvent)msg!;
        await Assert.That(evt.Uuid).IsEqualTo("evt-001");
        await Assert.That(evt.SessionId).IsEqualTo("sess-001");
        await Assert.That(evt.Event.GetProperty("type").GetString()).IsEqualTo("content_block_delta");
    }

    [Test]
    public async Task RateLimitEvent_Deserializes()
    {
        const string json = """
            {
                "type": "rate_limit_event",
                "uuid": "evt-002",
                "session_id": "sess-001",
                "rate_limit_info": {
                    "status": "allowed_warning",
                    "resetsAt": 1700000000,
                    "rateLimitType": "five_hour",
                    "utilization": 0.85
                }
            }
            """;

        var msg = ClaudeJsonOptions.DeserializeMessage(json);

        await Assert.That(msg).IsTypeOf<RateLimitEvent>();
        var rle = (RateLimitEvent)msg!;
        await Assert.That(rle.RateLimitInfo.Status).IsEqualTo("allowed_warning");
        await Assert.That(rle.RateLimitInfo.ResetsAt).IsEqualTo(1700000000);
        await Assert.That(rle.RateLimitInfo.RateLimitType).IsEqualTo("five_hour");
        await Assert.That(rle.RateLimitInfo.Utilization).IsEqualTo(0.85);
    }

    [Test]
    public async Task MissingTypeField_ThrowsJsonException()
    {
        const string json = """{"subtype": "init"}""";

        await Assert.That(() => ClaudeJsonOptions.DeserializeMessage(json))
            .ThrowsException();
    }

    [Test]
    public async Task UnknownMessageType_ThrowsJsonException()
    {
        const string json = """{"type": "unknown_future_type"}""";

        await Assert.That(() => ClaudeJsonOptions.DeserializeMessage(json))
            .ThrowsException();
    }
}
