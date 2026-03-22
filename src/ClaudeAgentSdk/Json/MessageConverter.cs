using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Json;

internal sealed class MessageConverter : JsonConverter<Message>
{
    public override Message? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        return Parse(doc.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, Message value, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Serialization of Message is not supported");
    }

    /// <summary>
    /// Parse a JsonElement into a Message. Used by ClaudeJsonOptions.DeserializeMessage
    /// to avoid AOT-unsafe JsonSerializer.Deserialize calls.
    /// </summary>
    internal static Message Parse(JsonElement root)
    {
        if (!root.TryGetProperty("type", out var typeProp))
        {
            throw new JsonException("Message missing 'type' field");
        }

        return typeProp.GetString() switch
        {
            "assistant" => ParseAssistantMessage(root),
            "user" => ParseUserMessage(root),
            "system" => ParseSystemMessage(root),
            "result" => ParseResultMessage(root),
            "stream_event" => ParseStreamEvent(root),
            "rate_limit_event" => ParseRateLimitEvent(root),
            "control_response" => new ControlResponseMessage(root.Clone()),
            "control_request" => new ControlRequestMessage(root.Clone()),
            var unknown => throw new JsonException($"Unknown message type: '{unknown}'")
        };
    }

    private static IReadOnlyList<ContentBlock> ParseContentBlocks(JsonElement contentArray)
    {
        var blocks = new List<ContentBlock>();
        foreach (var block in contentArray.EnumerateArray())
        {
            blocks.Add(ContentBlockConverter.Parse(block));
        }
        return blocks.AsReadOnly();
    }

    private static AssistantMessage ParseAssistantMessage(JsonElement root)
    {
        var message = root.GetProperty("message");

        return new AssistantMessage
        {
            Content = ParseContentBlocks(message.GetProperty("content")),
            Model = message.GetProperty("model").GetString()!,
            ParentToolUseId = root.TryGetProperty("parent_tool_use_id", out var ptui) ? ptui.GetString() : null,
            Error = root.TryGetProperty("error", out var err) ? err.GetString() : null,
            Usage = message.TryGetProperty("usage", out var usage) ? usage.Clone() : null
        };
    }

    private static UserMessage ParseUserMessage(JsonElement root)
    {
        var message = root.GetProperty("message");
        var contentElement = message.GetProperty("content");

        object content;
        if (contentElement.ValueKind == JsonValueKind.String)
        {
            content = contentElement.GetString()!;
        }
        else
        {
            content = ParseContentBlocks(contentElement);
        }

        return new UserMessage
        {
            Content = content,
            Uuid = root.TryGetProperty("uuid", out var uuid) ? uuid.GetString() : null,
            ParentToolUseId = root.TryGetProperty("parent_tool_use_id", out var ptui) ? ptui.GetString() : null,
            ToolUseResult = root.TryGetProperty("tool_use_result", out var tur) ? tur.Clone() : null
        };
    }

    private static SystemMessage ParseSystemMessage(JsonElement root)
    {
        var subtype = root.GetProperty("subtype").GetString()!;

        return subtype switch
        {
            "task_started" => ParseTaskStartedMessage(root, subtype),
            "task_progress" => ParseTaskProgressMessage(root, subtype),
            "task_notification" => ParseTaskNotificationMessage(root, subtype),
            _ => new SystemMessage { Subtype = subtype, Data = root.Clone() }
        };
    }

    private static TaskStartedMessage ParseTaskStartedMessage(JsonElement root, string subtype)
    {
        var data = root.GetProperty("data");
        return new TaskStartedMessage
        {
            Subtype = subtype,
            Data = root.Clone(),
            TaskId = data.GetProperty("task_id").GetString()!,
            Description = data.GetProperty("description").GetString()!,
            Uuid = data.GetProperty("uuid").GetString()!,
            SessionId = data.GetProperty("session_id").GetString()!,
            ToolUseId = data.TryGetProperty("tool_use_id", out var tui) ? tui.GetString() : null,
            TaskType = data.TryGetProperty("task_type", out var tt) ? tt.GetString() : null
        };
    }

    private static TaskProgressMessage ParseTaskProgressMessage(JsonElement root, string subtype)
    {
        var data = root.GetProperty("data");
        var usage = data.GetProperty("usage");
        return new TaskProgressMessage
        {
            Subtype = subtype,
            Data = root.Clone(),
            TaskId = data.GetProperty("task_id").GetString()!,
            Description = data.GetProperty("description").GetString()!,
            Uuid = data.GetProperty("uuid").GetString()!,
            SessionId = data.GetProperty("session_id").GetString()!,
            Usage = new TaskUsage(
                usage.GetProperty("total_tokens").GetInt32(),
                usage.GetProperty("tool_uses").GetInt32(),
                usage.GetProperty("duration_ms").GetInt32()),
            ToolUseId = data.TryGetProperty("tool_use_id", out var tui) ? tui.GetString() : null,
            LastToolName = data.TryGetProperty("last_tool_name", out var ltn) ? ltn.GetString() : null
        };
    }

    private static TaskNotificationMessage ParseTaskNotificationMessage(JsonElement root, string subtype)
    {
        var data = root.GetProperty("data");
        TaskUsage? usage = null;
        if (data.TryGetProperty("usage", out var usageEl))
        {
            usage = new TaskUsage(
                usageEl.GetProperty("total_tokens").GetInt32(),
                usageEl.GetProperty("tool_uses").GetInt32(),
                usageEl.GetProperty("duration_ms").GetInt32());
        }

        return new TaskNotificationMessage
        {
            Subtype = subtype,
            Data = root.Clone(),
            TaskId = data.GetProperty("task_id").GetString()!,
            Status = data.GetProperty("status").GetString()!,
            OutputFile = data.GetProperty("output_file").GetString()!,
            Summary = data.GetProperty("summary").GetString()!,
            Uuid = data.GetProperty("uuid").GetString()!,
            SessionId = data.GetProperty("session_id").GetString()!,
            ToolUseId = data.TryGetProperty("tool_use_id", out var tui) ? tui.GetString() : null,
            Usage = usage
        };
    }

    private static ResultMessage ParseResultMessage(JsonElement root)
    {
        return new ResultMessage
        {
            Subtype = root.GetProperty("subtype").GetString()!,
            DurationMs = root.GetProperty("duration_ms").GetInt32(),
            DurationApiMs = root.GetProperty("duration_api_ms").GetInt32(),
            IsError = root.GetProperty("is_error").GetBoolean(),
            NumTurns = root.GetProperty("num_turns").GetInt32(),
            SessionId = root.GetProperty("session_id").GetString()!,
            StopReason = root.TryGetProperty("stop_reason", out var sr) ? sr.GetString() : null,
            TotalCostUsd = root.TryGetProperty("total_cost_usd", out var cost) && cost.ValueKind != JsonValueKind.Null ? cost.GetDouble() : null,
            Usage = root.TryGetProperty("usage", out var usage) ? usage.Clone() : null,
            Result = root.TryGetProperty("result", out var result) ? result.GetString() : null,
            StructuredOutput = root.TryGetProperty("structured_output", out var so) ? so.Clone() : null
        };
    }

    private static StreamEvent ParseStreamEvent(JsonElement root)
    {
        return new StreamEvent
        {
            Uuid = root.GetProperty("uuid").GetString()!,
            SessionId = root.GetProperty("session_id").GetString()!,
            Event = root.GetProperty("event").Clone(),
            ParentToolUseId = root.TryGetProperty("parent_tool_use_id", out var ptui) ? ptui.GetString() : null
        };
    }

    private static RateLimitEvent ParseRateLimitEvent(JsonElement root)
    {
        var info = root.GetProperty("rate_limit_info");

        return new RateLimitEvent
        {
            RateLimitInfo = new RateLimitInfo
            {
                Status = info.GetProperty("status").GetString()!,
                ResetsAt = info.TryGetProperty("resetsAt", out var ra) && ra.ValueKind != JsonValueKind.Null ? ra.GetInt64() : null,
                RateLimitType = info.TryGetProperty("rateLimitType", out var rlt) ? rlt.GetString() : null,
                Utilization = info.TryGetProperty("utilization", out var util) && util.ValueKind != JsonValueKind.Null ? util.GetDouble() : null,
                OverageStatus = info.TryGetProperty("overageStatus", out var os) ? os.GetString() : null,
                OverageResetsAt = info.TryGetProperty("overageResetsAt", out var ora) && ora.ValueKind != JsonValueKind.Null ? ora.GetInt64() : null,
                OverageDisabledReason = info.TryGetProperty("overageDisabledReason", out var odr) ? odr.GetString() : null
            },
            Uuid = root.GetProperty("uuid").GetString()!,
            SessionId = root.GetProperty("session_id").GetString()!
        };
    }
}
