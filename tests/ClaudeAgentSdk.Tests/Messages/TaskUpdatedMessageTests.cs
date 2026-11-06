using ClaudeAgentSdk.Json;
using ClaudeAgentSdk.Messages;

namespace ClaudeAgentSdk.Tests.Messages;

public class TaskUpdatedMessageTests
{
    [Test]
    public async Task TaskUpdated_WithPatchStatus_YieldsTaskUpdatedMessage()
    {
        const string json = """{"type":"system","subtype":"task_updated","task_id":"task-001","patch":{"status":"killed","end_time":1700000000},"uuid":"uuid-1","session_id":"sess-1"}""";

        var msg = ClaudeJsonOptions.DeserializeMessage(json);

        await Assert.That(msg).IsTypeOf<TaskUpdatedMessage>();
        var task = (TaskUpdatedMessage)msg!;
        await Assert.That(task.Subtype).IsEqualTo("task_updated");
        await Assert.That(task.TaskId).IsEqualTo("task-001");
        await Assert.That(task.Status).IsEqualTo("killed");
        await Assert.That(task.Patch.GetProperty("end_time").GetInt64()).IsEqualTo(1700000000);
        await Assert.That(task.Uuid).IsEqualTo("uuid-1");
        await Assert.That(task.SessionId).IsEqualTo("sess-1");
        await Assert.That(task.Data.GetProperty("task_id").GetString()).IsEqualTo("task-001");
    }

    [Test]
    public async Task TaskUpdated_WithoutPatch_ParsesWithEmptyPatch()
    {
        const string json = """{"type":"system","subtype":"task_updated","task_id":"task-001"}""";

        var task = (TaskUpdatedMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(task.TaskId).IsEqualTo("task-001");
        await Assert.That(task.Patch.ValueKind).IsEqualTo(System.Text.Json.JsonValueKind.Object);
        await Assert.That(task.Patch.EnumerateObject().Any()).IsFalse();
        await Assert.That(task.Status).IsNull();
        await Assert.That(task.Uuid).IsNull();
        await Assert.That(task.SessionId).IsNull();
    }

    [Test]
    public async Task TaskUpdated_WithNonObjectPatch_ParsesWithEmptyPatch()
    {
        const string json = """{"type":"system","subtype":"task_updated","task_id":"task-001","patch":"bad"}""";

        var task = (TaskUpdatedMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(task.Patch.ValueKind).IsEqualTo(System.Text.Json.JsonValueKind.Object);
        await Assert.That(task.Status).IsNull();
    }

    [Test]
    public async Task TaskUpdated_WithWrongFieldTypes_DoesNotThrow()
    {
        const string json = """{"type":"system","subtype":"task_updated","task_id":42,"patch":{"status":3},"uuid":null,"session_id":false}""";

        var task = (TaskUpdatedMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(task.TaskId).IsEqualTo("");
        await Assert.That(task.Status).IsNull();
        await Assert.That(task.Uuid).IsNull();
        await Assert.That(task.SessionId).IsNull();
    }

    [Test]
    [Arguments("completed", true)]
    [Arguments("failed", true)]
    [Arguments("stopped", true)]
    [Arguments("killed", true)]
    [Arguments("running", false)]
    [Arguments("pending", false)]
    [Arguments("paused", false)]
    [Arguments(null, false)]
    public async Task TaskStatuses_IsTerminal_MatchesTerminalSet(string? status, bool expected)
    {
        await Assert.That(TaskStatuses.IsTerminal(status)).IsEqualTo(expected);
    }

    [Test]
    public async Task TaskStatuses_Terminal_HasFourStatuses()
    {
        await Assert.That(TaskStatuses.Terminal.Count).IsEqualTo(4);
    }

    [Test]
    public async Task TaskUpdated_IsTerminal_UsesPatchStatus()
    {
        const string json = """{"type":"system","subtype":"task_updated","task_id":"t","patch":{"status":"completed"}}""";

        var task = (TaskUpdatedMessage)ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(task.IsTerminal).IsTrue();
    }

    [Test]
    public async Task FormatMessage_TaskUpdated_ShowsTaskIdAndStatus()
    {
        const string json = """{"type":"system","subtype":"task_updated","task_id":"task-001","patch":{"status":"failed"}}""";

        var msg = ClaudeJsonOptions.DeserializeMessage(json)!;

        await Assert.That(Claude.FormatMessage(msg)).IsEqualTo("[task_updated] task-001: failed");
    }
}
