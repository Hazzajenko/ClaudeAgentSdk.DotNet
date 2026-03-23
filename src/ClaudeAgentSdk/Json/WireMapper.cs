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
        JsonValueKind.Array => content
            .Deserialize(ClaudeJsonContext.Default.ListContentBlockWire)!
            .Select(MapContentBlock)
            .ToList()
            .AsReadOnly(),
        _ => throw new JsonException($"UserMessage.Content must be string or array, got {content.ValueKind}")
    };
}
