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
        Usage = wire.Message.Usage,
        MessageId = wire.Message.Id,
        StopReason = wire.Message.StopReason,
        SessionId = wire.SessionId,
        Uuid = wire.Uuid
    };

    public static UserMessage MapUserMessage(UserMessageWire wire) => new()
    {
        Content = MapUserContent(wire.Message.Content),
        Uuid = wire.Uuid,
        ParentToolUseId = wire.ParentToolUseId,
        ToolUseResult = wire.ToolUseResult,
        Origin = MapOrigin(wire.Origin)
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
        StructuredOutput = wire.StructuredOutput,
        ModelUsage = wire.ModelUsage?.AsReadOnly(),
        PermissionDenials = wire.PermissionDenials,
        DeferredToolUse = wire.DeferredToolUse,
        Errors = wire.Errors?.AsReadOnly(),
        ApiErrorStatus = wire.ApiErrorStatus,
        TerminalReason = wire.TerminalReason,
        Uuid = wire.Uuid,
        Origin = MapOrigin(wire.Origin)
    };

    public static StreamEvent MapStreamEvent(StreamEventWire wire) => new()
    {
        Uuid = wire.Uuid,
        SessionId = wire.SessionId,
        Event = wire.Event,
        ParentToolUseId = wire.ParentToolUseId
    };

    public static RateLimitEvent MapRateLimitEvent(RateLimitEventWire wire)
    {
        var info = wire.RateLimitInfo.Deserialize(ClaudeJsonContext.Default.RateLimitInfoWire)
            ?? throw new JsonException("rate_limit_info must be an object");
        return new RateLimitEvent
        {
            Uuid = wire.Uuid,
            SessionId = wire.SessionId,
            RateLimitInfo = new RateLimitInfo
            {
                Status = info.Status,
                ResetsAt = info.ResetsAt,
                RateLimitType = info.RateLimitType,
                Utilization = info.Utilization,
                OverageStatus = info.OverageStatus,
                OverageResetsAt = info.OverageResetsAt,
                OverageDisabledReason = info.OverageDisabledReason,
                Raw = wire.RateLimitInfo
            }
        };
    }

    public static Message MapSystemMessage(SystemMessageWire wire, JsonElement rootClone)
    {
        return wire.Subtype switch
        {
            "task_started" => MapTaskStarted(wire, rootClone),
            "task_progress" => MapTaskProgress(wire, rootClone),
            "task_notification" => MapTaskNotification(wire, rootClone),
            "hook_started" or "hook_response" => MapHookEvent(wire, rootClone),
            _ => new SystemMessage { Subtype = wire.Subtype, Data = rootClone }
        };
    }

    private static HookEventMessage MapHookEvent(SystemMessageWire wire, JsonElement rootClone) => new()
    {
        Subtype = wire.Subtype,
        Data = rootClone,
        HookEventName = ReadNonEmptyString(rootClone, "hook_event")
            ?? ReadNonEmptyString(rootClone, "hook_name")
            ?? ReadNonEmptyString(rootClone, "hook_event_name")
            ?? "",
        SessionId = ReadNonEmptyString(rootClone, "session_id"),
        Uuid = ReadNonEmptyString(rootClone, "uuid")
    };

    private static string? ReadNonEmptyString(JsonElement element, string name) =>
        element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
        && value.GetString() is { Length: > 0 } text
            ? text
            : null;

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

    // Like the Python SDK, an origin that is not an object with a string "kind" reads as null.
    private static MessageOrigin? MapOrigin(JsonElement? origin)
    {
        if (origin is not { ValueKind: JsonValueKind.Object } element
            || !element.TryGetProperty("kind", out var kind)
            || kind.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        try
        {
            return element.Deserialize(ClaudeJsonContext.Default.MessageOrigin);
        }
        catch (JsonException)
        {
            return null;
        }
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
