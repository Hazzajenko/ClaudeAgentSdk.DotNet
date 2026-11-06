using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using ClaudeAgentSdk.Json.Wire;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Json;

internal static class WireMapper
{
    /// <summary>
    /// Maps one element of a "content" array. Returns null for an unknown "type",
    /// so a new block type from the CLI does not end the stream.
    /// </summary>
    public static ContentBlock? MapContentBlock(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException($"Content block must be an object, got {element.ValueKind}");
        }

        if (!element.TryGetProperty("type", out JsonElement typeProp) || typeProp.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("Content block missing 'type' field");
        }

        string type = typeProp.GetString()!;
        switch (type)
        {
            case "text":
                TextBlockWire text = Read(element, ClaudeJsonContext.Default.TextBlockWire);
                return new TextBlock { Text = text.Text };
            case "thinking":
                ThinkingBlockWire thinking = Read(element, ClaudeJsonContext.Default.ThinkingBlockWire);
                return new ThinkingBlock { Thinking = thinking.Thinking, Signature = thinking.Signature };
            case "tool_use":
                ToolUseBlockWire toolUse = Read(element, ClaudeJsonContext.Default.ToolUseBlockWire);
                return new ToolUseBlock { Id = toolUse.Id, Name = toolUse.Name, Input = toolUse.Input };
            case "tool_result":
                ToolResultBlockWire toolResult = Read(element, ClaudeJsonContext.Default.ToolResultBlockWire);
                return new ToolResultBlock { ToolUseId = toolResult.ToolUseId, Content = toolResult.Content, IsError = toolResult.IsError };
            default:
                return null;
        }
    }

    private static IReadOnlyList<ContentBlock> MapContentBlocks(IEnumerable<JsonElement> elements) => elements
        .Select(MapContentBlock)
        .OfType<ContentBlock>()
        .ToList()
        .AsReadOnly();

    private static T Read<T>(JsonElement element, JsonTypeInfo<T> typeInfo) =>
        element.Deserialize(typeInfo) ?? throw new JsonException($"Content block deserialized to null: {typeof(T).Name}");

    public static AssistantMessage MapAssistantMessage(AssistantMessageWire wire) => new()
    {
        Content = MapContentBlocks(wire.Message.Content),
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
        var data = rootClone.Deserialize(ClaudeJsonContext.Default.TaskStartedDataWire)!;
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
        var data = rootClone.Deserialize(ClaudeJsonContext.Default.TaskProgressDataWire)!;
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
        var data = rootClone.Deserialize(ClaudeJsonContext.Default.TaskNotificationDataWire)!;
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
        JsonValueKind.Array => MapContentBlocks(content.EnumerateArray()),
        _ => throw new JsonException($"UserMessage.Content must be string or array, got {content.ValueKind}")
    };
}
