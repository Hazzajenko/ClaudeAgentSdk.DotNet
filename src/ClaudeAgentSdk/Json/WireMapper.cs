using System.Text.Json;
using ClaudeAgentSdk.Json.Wire;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Json;

internal static class WireMapper
{
    public static ContentBlock MapContentBlock(ContentBlockWire wire) => wire switch
    {
        TextBlockWire t => new TextBlock { Text = t.Text },
        ThinkingBlockWire t => new ThinkingBlock { Thinking = t.Thinking, Signature = t.Signature },
        ToolUseBlockWire t => new ToolUseBlock { Id = t.Id, Name = t.Name, Input = t.Input },
        ToolResultBlockWire t => new ToolResultBlock { ToolUseId = t.ToolUseId, Content = t.Content, IsError = t.IsError },
        _ => throw new JsonException($"Unknown content block wire type: {wire.GetType().Name}")
    };

    public static AssistantMessage MapAssistantMessage(AssistantMessageWire wire) => new()
    {
        Content = wire.Message.Content.Select(MapContentBlock).ToList().AsReadOnly(),
        Model = wire.Message.Model,
        ParentToolUseId = wire.ParentToolUseId,
        Error = wire.Error,
        Usage = wire.Message.Usage
    };

    public static UserMessage MapUserMessage(UserMessageWire wire) => new()
    {
        Content = MapUserContent(wire.Message.Content),
        Uuid = wire.Uuid,
        ParentToolUseId = wire.ParentToolUseId,
        ToolUseResult = wire.ToolUseResult
    };

    /// <summary>
    /// Parse a user message directly from JsonElement, bypassing the source-gen
    /// UserMessageWire deserializer. The CLI may emit tool_result content blocks
    /// with "type" not as the first property, which breaks STJ polymorphic
    /// deserialization of List&lt;ContentBlockWire&gt;.
    /// </summary>
    public static UserMessage MapUserMessageFromElement(JsonElement root)
    {
        var message = root.GetProperty("message");
        var content = message.GetProperty("content");

        string? uuid = root.TryGetProperty("uuid", out var u) ? u.GetString() : null;
        string? parentToolUseId = root.TryGetProperty("parent_tool_use_id", out var p)
            ? p.GetString() : null;
        JsonElement? toolUseResult = root.TryGetProperty("tool_use_result", out var t)
            ? t.Clone() : null;

        return new UserMessage
        {
            Content = MapUserContent(content),
            Uuid = uuid,
            ParentToolUseId = parentToolUseId,
            ToolUseResult = toolUseResult
        };
    }

    public static ResultMessage MapResultMessage(ResultMessageWire wire) => new()
    {
        Subtype = wire.Subtype,
        DurationMs = wire.DurationMs,
        DurationApiMs = wire.DurationApiMs,
        IsError = wire.IsError,
        NumTurns = wire.NumTurns,
        SessionId = wire.SessionId,
        StopReason = wire.StopReason,
        TotalCostUsd = wire.TotalCostUsd,
        Usage = wire.Usage,
        Result = wire.Result,
        StructuredOutput = wire.StructuredOutput
    };

    public static StreamEvent MapStreamEvent(StreamEventWire wire) => new()
    {
        Uuid = wire.Uuid,
        SessionId = wire.SessionId,
        Event = wire.Event,
        ParentToolUseId = wire.ParentToolUseId
    };

    public static RateLimitEvent MapRateLimitEvent(RateLimitEventWire wire) => new()
    {
        Uuid = wire.Uuid,
        SessionId = wire.SessionId,
        RateLimitInfo = new RateLimitInfo
        {
            Status = wire.RateLimitInfo.Status,
            ResetsAt = wire.RateLimitInfo.ResetsAt,
            RateLimitType = wire.RateLimitInfo.RateLimitType,
            Utilization = wire.RateLimitInfo.Utilization,
            OverageStatus = wire.RateLimitInfo.OverageStatus,
            OverageResetsAt = wire.RateLimitInfo.OverageResetsAt,
            OverageDisabledReason = wire.RateLimitInfo.OverageDisabledReason
        }
    };

    public static Message MapSystemMessage(SystemMessageWire wire, JsonElement rootClone)
    {
        return wire.Subtype switch
        {
            "task_started" => MapTaskStarted(wire, rootClone),
            "task_progress" => MapTaskProgress(wire, rootClone),
            "task_notification" => MapTaskNotification(wire, rootClone),
            _ => new SystemMessage { Subtype = wire.Subtype, Data = rootClone }
        };
    }

    private static TaskStartedMessage MapTaskStarted(SystemMessageWire wire, JsonElement rootClone)
    {
        var data = wire.Data!.Value.Deserialize(ClaudeJsonContext.Default.TaskStartedDataWire)!;
        return new TaskStartedMessage
        {
            Subtype = wire.Subtype,
            Data = rootClone,
            TaskId = data.TaskId,
            Description = data.Description,
            Uuid = data.Uuid,
            SessionId = data.SessionId,
            ToolUseId = data.ToolUseId,
            TaskType = data.TaskType
        };
    }

    private static TaskProgressMessage MapTaskProgress(SystemMessageWire wire, JsonElement rootClone)
    {
        var data = wire.Data!.Value.Deserialize(ClaudeJsonContext.Default.TaskProgressDataWire)!;
        return new TaskProgressMessage
        {
            Subtype = wire.Subtype,
            Data = rootClone,
            TaskId = data.TaskId,
            Description = data.Description,
            Usage = data.Usage,
            Uuid = data.Uuid,
            SessionId = data.SessionId,
            ToolUseId = data.ToolUseId,
            LastToolName = data.LastToolName
        };
    }

    private static TaskNotificationMessage MapTaskNotification(SystemMessageWire wire, JsonElement rootClone)
    {
        var data = wire.Data!.Value.Deserialize(ClaudeJsonContext.Default.TaskNotificationDataWire)!;
        return new TaskNotificationMessage
        {
            Subtype = wire.Subtype,
            Data = rootClone,
            TaskId = data.TaskId,
            Status = data.Status,
            OutputFile = data.OutputFile,
            Summary = data.Summary,
            Uuid = data.Uuid,
            SessionId = data.SessionId,
            ToolUseId = data.ToolUseId,
            Usage = data.Usage
        };
    }

    private static object MapUserContent(JsonElement content) => content.ValueKind switch
    {
        JsonValueKind.String => content.GetString()!,
        JsonValueKind.Array => content.EnumerateArray()
            .Select(DeserializeContentBlock)
            .ToList()
            .AsReadOnly(),
        _ => throw new JsonException($"UserMessage.Content must be string or array, got {content.ValueKind}")
    };

    /// <summary>
    /// Deserialize a content block by dispatching on its "type" property manually.
    /// The CLI may emit content blocks with "type" not as the first property
    /// (e.g. tool_result has tool_use_id first), which breaks STJ polymorphic
    /// deserialization that requires the discriminator to be the first property.
    /// </summary>
    private static ContentBlock DeserializeContentBlock(JsonElement el)
    {
        string type = el.GetProperty("type").GetString()
            ?? throw new JsonException("Content block missing 'type'");

        return type switch
        {
            "text" => new TextBlock { Text = el.GetProperty("text").GetString()! },
            "thinking" => new ThinkingBlock
            {
                Thinking = el.GetProperty("thinking").GetString()!,
                Signature = el.GetProperty("signature").GetString()!
            },
            "tool_use" => new ToolUseBlock
            {
                Id = el.GetProperty("id").GetString()!,
                Name = el.GetProperty("name").GetString()!,
                Input = el.GetProperty("input").Clone()
            },
            "tool_result" => new ToolResultBlock
            {
                ToolUseId = el.GetProperty("tool_use_id").GetString()!,
                Content = el.TryGetProperty("content", out var c) ? c.Clone() : null,
                IsError = el.TryGetProperty("is_error", out var e) ? e.GetBoolean() : null
            },
            _ => throw new JsonException($"Unknown content block type: '{type}'")
        };
    }
}
