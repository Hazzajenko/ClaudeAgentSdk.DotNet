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

    [Test]
    public async Task TaskStartedMessage_Deserializes()
    {
        const string json = """
            {
                "type": "system",
                "subtype": "task_started",
                "data": {
                    "task_id": "task-001",
                    "description": "Running tests",
                    "uuid": "msg-001",
                    "session_id": "sess-001",
                    "tool_use_id": "tool-001",
                    "task_type": "agent"
                }
            }
            """;

        var msg = ClaudeJsonOptions.DeserializeMessage(json);

        await Assert.That(msg).IsTypeOf<TaskStartedMessage>();
        var task = (TaskStartedMessage)msg!;
        await Assert.That(task.TaskId).IsEqualTo("task-001");
        await Assert.That(task.Description).IsEqualTo("Running tests");
        await Assert.That(task.Uuid).IsEqualTo("msg-001");
        await Assert.That(task.SessionId).IsEqualTo("sess-001");
        await Assert.That(task.ToolUseId).IsEqualTo("tool-001");
        await Assert.That(task.TaskType).IsEqualTo("agent");
        // Also a SystemMessage
        await Assert.That(task.Subtype).IsEqualTo("task_started");
    }

    [Test]
    public async Task TaskProgressMessage_Deserializes()
    {
        const string json = """
            {
                "type": "system",
                "subtype": "task_progress",
                "data": {
                    "task_id": "task-001",
                    "description": "Still running",
                    "uuid": "msg-002",
                    "session_id": "sess-001",
                    "usage": {
                        "total_tokens": 5000,
                        "tool_uses": 3,
                        "duration_ms": 12000
                    },
                    "last_tool_name": "Bash"
                }
            }
            """;

        var msg = ClaudeJsonOptions.DeserializeMessage(json);

        await Assert.That(msg).IsTypeOf<TaskProgressMessage>();
        var task = (TaskProgressMessage)msg!;
        await Assert.That(task.TaskId).IsEqualTo("task-001");
        await Assert.That(task.Usage.TotalTokens).IsEqualTo(5000);
        await Assert.That(task.Usage.ToolUses).IsEqualTo(3);
        await Assert.That(task.Usage.DurationMs).IsEqualTo(12000);
        await Assert.That(task.LastToolName).IsEqualTo("Bash");
    }

    [Test]
    public async Task TaskNotificationMessage_Deserializes()
    {
        const string json = """
            {
                "type": "system",
                "subtype": "task_notification",
                "data": {
                    "task_id": "task-001",
                    "status": "completed",
                    "output_file": "/tmp/output.txt",
                    "summary": "Tests passed",
                    "uuid": "msg-003",
                    "session_id": "sess-001",
                    "usage": {
                        "total_tokens": 10000,
                        "tool_uses": 7,
                        "duration_ms": 30000
                    }
                }
            }
            """;

        var msg = ClaudeJsonOptions.DeserializeMessage(json);

        await Assert.That(msg).IsTypeOf<TaskNotificationMessage>();
        var task = (TaskNotificationMessage)msg!;
        await Assert.That(task.TaskId).IsEqualTo("task-001");
        await Assert.That(task.Status).IsEqualTo("completed");
        await Assert.That(task.OutputFile).IsEqualTo("/tmp/output.txt");
        await Assert.That(task.Summary).IsEqualTo("Tests passed");
        await Assert.That(task.Usage).IsNotNull();
        await Assert.That(task.Usage!.Value.TotalTokens).IsEqualTo(10000);
    }

    [Test]
    public async Task SystemMessage_NonTaskSubtype_StillDeserializesAsSystemMessage()
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
        // Should NOT be a TaskStartedMessage
        await Assert.That(msg is TaskStartedMessage).IsFalse();
    }

    [Test]
    public async Task UserMessage_ToolResultWithOutOfOrderType_Deserializes()
    {
        // CLI emits tool_use_id BEFORE type — this broke STJ polymorphic deserialization
        // before AllowOutOfOrderMetadataProperties was enabled
        const string json = """
            {
                "type": "user",
                "message": {
                    "role": "user",
                    "content": [
                        {
                            "tool_use_id": "toolu_01ABC",
                            "type": "tool_result",
                            "content": "Structured output provided successfully"
                        }
                    ]
                },
                "parent_tool_use_id": null
            }
            """;

        var msg = (UserMessage)ClaudeJsonOptions.DeserializeMessage(json)!;
        var blocks = (IReadOnlyList<ContentBlock>)msg.Content;

        await Assert.That(blocks).Count().IsEqualTo(1);
        var tr = (ToolResultBlock)blocks[0];
        await Assert.That(tr.ToolUseId).IsEqualTo("toolu_01ABC");
        await Assert.That(tr.Content!.Value.GetString()).IsEqualTo("Structured output provided successfully");
    }

    [Test]
    public async Task UserMessage_MultipleContentBlocks_Deserializes()
    {
        const string json = """
            {
                "type": "user",
                "message": {
                    "content": [
                        {"type": "text", "text": "Here is the result:"},
                        {"tool_use_id": "toolu_99", "type": "tool_result", "content": "42"}
                    ]
                }
            }
            """;

        var msg = (UserMessage)ClaudeJsonOptions.DeserializeMessage(json)!;
        var blocks = (IReadOnlyList<ContentBlock>)msg.Content;

        await Assert.That(blocks).Count().IsEqualTo(2);
        await Assert.That(blocks[0]).IsTypeOf<TextBlock>();
        await Assert.That(blocks[1]).IsTypeOf<ToolResultBlock>();
    }

    [Test]
    public async Task ResultMessage_WithStructuredOutput_PreservesJsonElement()
    {
        const string json = """
            {
                "type": "result",
                "subtype": "success",
                "duration_ms": 200,
                "duration_api_ms": 180,
                "is_error": false,
                "num_turns": 2,
                "session_id": "s1",
                "structured_output": {"name": "Alice", "age": 30, "tags": ["a", "b"]}
            }
            """;

        var result = (ResultMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(result.StructuredOutput).IsNotNull();
        await Assert.That(result.StructuredOutput!.Value.GetProperty("name").GetString()).IsEqualTo("Alice");
        await Assert.That(result.StructuredOutput!.Value.GetProperty("age").GetInt32()).IsEqualTo(30);
        await Assert.That(result.StructuredOutput!.Value.GetProperty("tags").GetArrayLength()).IsEqualTo(2);
    }

    [Test]
    public async Task AssistantMessage_ThinkingThenToolUse_Deserializes()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [
                        {"type": "thinking", "thinking": "I need to extract data.", "signature": "sig1"},
                        {"type": "tool_use", "id": "toolu_01X", "name": "StructuredOutput", "input": {"name": "Bob"}}
                    ],
                    "model": "claude-sonnet-4-6"
                }
            }
            """;

        var msg = (AssistantMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(msg.Content).Count().IsEqualTo(2);
        await Assert.That(msg.Content[0]).IsTypeOf<ThinkingBlock>();
        await Assert.That(msg.Content[1]).IsTypeOf<ToolUseBlock>();
        await Assert.That(((ToolUseBlock)msg.Content[1]).Name).IsEqualTo("StructuredOutput");
    }

    [Test]
    public async Task ControlResponse_PreservesRawElement()
    {
        const string json = """
            {
                "type": "control_response",
                "response": {
                    "subtype": "success",
                    "request_id": "req_1",
                    "response": {"commands": []}
                }
            }
            """;

        var msg = ClaudeJsonOptions.DeserializeMessage(json);

        await Assert.That(msg).IsTypeOf<ControlResponseMessage>();
        var cr = (ControlResponseMessage)msg!;
        await Assert.That(cr.RawElement.GetProperty("response").GetProperty("request_id").GetString())
            .IsEqualTo("req_1");
    }

    [Test]
    public async Task ControlRequest_PreservesRawElement()
    {
        const string json = """
            {
                "type": "control_request",
                "request_id": "req_2",
                "request": {"subtype": "can_use_tool", "tool_name": "Bash"}
            }
            """;

        var msg = ClaudeJsonOptions.DeserializeMessage(json);

        await Assert.That(msg).IsTypeOf<ControlRequestMessage>();
        var cr = (ControlRequestMessage)msg!;
        await Assert.That(cr.RawElement.GetProperty("request").GetProperty("tool_name").GetString())
            .IsEqualTo("Bash");
    }

    [Test]
    public async Task ExtraUnknownProperties_AreIgnored()
    {
        // CLI may add new fields in future versions — they should be silently ignored
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [{"type": "text", "text": "hi"}],
                    "model": "claude-sonnet-4-6",
                    "future_field": true,
                    "context_management": null
                },
                "parent_tool_use_id": null,
                "session_id": "sess-999",
                "uuid": "msg-999"
            }
            """;

        var msg = (AssistantMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(((TextBlock)msg.Content[0]).Text).IsEqualTo("hi");
    }

    [Test]
    public async Task ContentBlock_WithExtraProperties_StillDeserializes()
    {
        // tool_use blocks from CLI include "caller" field not in our wire type
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [
                        {
                            "type": "tool_use",
                            "id": "toolu_01X",
                            "name": "Read",
                            "input": {"file_path": "/tmp/x"},
                            "caller": {"type": "direct"}
                        }
                    ],
                    "model": "claude-sonnet-4-6"
                }
            }
            """;

        var msg = (AssistantMessage)ClaudeJsonOptions.DeserializeMessage(json)!;
        var tu = (ToolUseBlock)msg.Content[0];

        await Assert.That(tu.Name).IsEqualTo("Read");
    }

    [Test]
    public async Task RealCli_StructuredOutputToolUse_Deserializes()
    {
        // Actual CLI output for StructuredOutput tool invocation
        const string json = """
            {"type":"assistant","message":{"model":"claude-sonnet-4-6","id":"msg_01X","type":"message","role":"assistant","content":[{"type":"tool_use","id":"toolu_01Y","name":"StructuredOutput","input":{"name":"John Smith","age":35},"caller":{"type":"direct"}}],"stop_reason":null,"stop_sequence":null,"usage":{"input_tokens":3,"cache_creation_input_tokens":0,"cache_read_input_tokens":15936,"output_tokens":0},"context_management":null},"parent_tool_use_id":null,"session_id":"sess-1","uuid":"uuid-1"}
            """;

        var msg = (AssistantMessage)ClaudeJsonOptions.DeserializeMessage(json)!;
        var tu = (ToolUseBlock)msg.Content[0];

        await Assert.That(tu.Name).IsEqualTo("StructuredOutput");
        await Assert.That(tu.Input.GetProperty("name").GetString()).IsEqualTo("John Smith");
        await Assert.That(tu.Input.GetProperty("age").GetInt32()).IsEqualTo(35);
    }

    [Test]
    public async Task RealCli_ToolResultFromCli_Deserializes()
    {
        // Actual CLI output: CLI-generated tool_result (tool_use_id before type!)
        const string json = """
            {"type":"user","message":{"role":"user","content":[{"tool_use_id":"toolu_01Y","type":"tool_result","content":"Structured output provided successfully"}]},"parent_tool_use_id":null,"session_id":"sess-1","uuid":"uuid-2","timestamp":"2026-03-24T05:15:36.489Z","tool_use_result":"Structured output provided successfully"}
            """;

        var msg = (UserMessage)ClaudeJsonOptions.DeserializeMessage(json)!;
        var blocks = (IReadOnlyList<ContentBlock>)msg.Content;

        await Assert.That(blocks).Count().IsEqualTo(1);
        var tr = (ToolResultBlock)blocks[0];
        await Assert.That(tr.ToolUseId).IsEqualTo("toolu_01Y");
        await Assert.That(msg.ToolUseResult!.Value.GetString()).IsEqualTo("Structured output provided successfully");
    }

    [Test]
    public async Task RealCli_ResultWithStructuredOutput_Deserializes()
    {
        // Actual CLI output: final result with structured_output
        const string json = """
            {"type":"result","subtype":"success","is_error":false,"duration_ms":5499,"duration_api_ms":4937,"num_turns":2,"result":"Extracted info.","stop_reason":"end_turn","session_id":"sess-1","total_cost_usd":0.067,"usage":{"input_tokens":4},"structured_output":{"name":"John Smith","age":35},"fast_mode_state":"off","uuid":"uuid-3"}
            """;

        var result = (ResultMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(result.NumTurns).IsEqualTo(2);
        await Assert.That(result.StructuredOutput).IsNotNull();
        await Assert.That(result.StructuredOutput!.Value.GetProperty("name").GetString()).IsEqualTo("John Smith");
        await Assert.That(result.TotalCostUsd).IsEqualTo(0.067);
    }
}
