using ClaudeAgentSdk.Json;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Tests.Messages;

public class ContentBlockParsingTests
{
    [Test]
    public async Task AssistantMessage_UnknownBlockType_SkipsBlock()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [
                        {"type": "text", "text": "before"},
                        {"type": "some_future_block", "data": {"x": 1}},
                        {"type": "text", "text": "after"}
                    ],
                    "model": "m"
                }
            }
            """;

        var assistant = (AssistantMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(assistant.Content).Count().IsEqualTo(2);
        await Assert.That(((TextBlock)assistant.Content[0]).Text).IsEqualTo("before");
        await Assert.That(((TextBlock)assistant.Content[1]).Text).IsEqualTo("after");
    }

    [Test]
    public async Task AssistantMessage_ServerToolUse_MapsToServerToolUseBlock()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [
                        {
                            "id": "srvtoolu_01",
                            "type": "server_tool_use",
                            "name": "web_search",
                            "input": {"query": "dotnet"}
                        }
                    ],
                    "model": "m"
                }
            }
            """;

        var assistant = (AssistantMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(assistant.Content).Count().IsEqualTo(1);
        await Assert.That(assistant.Content[0]).IsTypeOf<ServerToolUseBlock>();
        var block = (ServerToolUseBlock)assistant.Content[0];
        await Assert.That(block.Id).IsEqualTo("srvtoolu_01");
        await Assert.That(block.Name).IsEqualTo("web_search");
        await Assert.That(block.Input.GetProperty("query").GetString()).IsEqualTo("dotnet");
    }

    [Test]
    [Arguments("advisor_tool_result")]
    [Arguments("web_search_tool_result")]
    [Arguments("web_fetch_tool_result")]
    [Arguments("code_execution_tool_result")]
    public async Task AssistantMessage_ServerToolResult_MapsToServerToolResultBlock(string type)
    {
        string json = $$"""
            {
                "type": "assistant",
                "message": {
                    "content": [
                        {
                            "type": "{{type}}",
                            "tool_use_id": "srvtoolu_01",
                            "content": {"type": "advice", "text": "ok"}
                        }
                    ],
                    "model": "m"
                }
            }
            """;

        var assistant = (AssistantMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(assistant.Content).Count().IsEqualTo(1);
        await Assert.That(assistant.Content[0]).IsTypeOf<ServerToolResultBlock>();
        var block = (ServerToolResultBlock)assistant.Content[0];
        await Assert.That(block.ToolUseId).IsEqualTo("srvtoolu_01");
        await Assert.That(block.Content.GetProperty("text").GetString()).IsEqualTo("ok");
    }

    [Test]
    public async Task AssistantMessage_KnownBlocks_StillMap()
    {
        const string json = """
            {
                "type": "assistant",
                "message": {
                    "content": [
                        {"type": "thinking", "thinking": "hmm", "signature": "sig"},
                        {"tool_use_id": "t1", "type": "tool_result", "content": "done", "is_error": false},
                        {"type": "tool_use", "id": "t2", "name": "Read", "input": {}}
                    ],
                    "model": "m"
                }
            }
            """;

        var assistant = (AssistantMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(assistant.Content).Count().IsEqualTo(3);
        await Assert.That(((ThinkingBlock)assistant.Content[0]).Signature).IsEqualTo("sig");
        var result = (ToolResultBlock)assistant.Content[1];
        await Assert.That(result.ToolUseId).IsEqualTo("t1");
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(((ToolUseBlock)assistant.Content[2]).Name).IsEqualTo("Read");
    }

    [Test]
    public async Task UserMessage_UnknownBlockType_SkipsBlock()
    {
        const string json = """
            {
                "type": "user",
                "message": {
                    "content": [
                        {"type": "image", "source": {"type": "base64", "data": "AA=="}},
                        {"type": "tool_result", "tool_use_id": "t1", "content": "ok"}
                    ]
                }
            }
            """;

        var user = (UserMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        var blocks = (IReadOnlyList<ContentBlock>)user.Content;
        await Assert.That(blocks).Count().IsEqualTo(1);
        await Assert.That(blocks[0]).IsTypeOf<ToolResultBlock>();
    }
}
