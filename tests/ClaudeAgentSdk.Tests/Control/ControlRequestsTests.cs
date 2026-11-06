using System.Text.Json;
using ClaudeAgentSdk.Control;

namespace ClaudeAgentSdk.Tests.Control;

public class ControlRequestsTests
{
    private const string Hostile = "a\"b\\c\n}{\"x\":1";

    [Test]
    public async Task SetModel_ModelWithQuote_ProducesValidJson()
    {
        JsonElement request = ControlRequests.SetModel("claude\"evil");

        await Assert.That(request.GetProperty("subtype").GetString()).IsEqualTo("set_model");
        await Assert.That(request.GetProperty("model").GetString()).IsEqualTo("claude\"evil");
    }

    [Test]
    public async Task SetModel_Null_WritesJsonNull()
    {
        JsonElement request = ControlRequests.SetModel(null);

        await Assert.That(request.GetProperty("model").ValueKind).IsEqualTo(JsonValueKind.Null);
    }

    [Test]
    public async Task SetPermissionMode_WritesMode()
    {
        JsonElement request = ControlRequests.SetPermissionMode("acceptEdits");

        await Assert.That(request.GetProperty("subtype").GetString()).IsEqualTo("set_permission_mode");
        await Assert.That(request.GetProperty("mode").GetString()).IsEqualTo("acceptEdits");
    }

    [Test]
    public async Task RewindFiles_EscapesId()
    {
        JsonElement request = ControlRequests.RewindFiles(Hostile);

        await Assert.That(request.GetProperty("subtype").GetString()).IsEqualTo("rewind_files");
        await Assert.That(request.GetProperty("user_message_id").GetString()).IsEqualTo(Hostile);
        await Assert.That(request.EnumerateObject().Count()).IsEqualTo(2);
    }

    [Test]
    public async Task McpReconnect_EscapesServerName()
    {
        JsonElement request = ControlRequests.McpReconnect(Hostile);

        await Assert.That(request.GetProperty("subtype").GetString()).IsEqualTo("mcp_reconnect");
        await Assert.That(request.GetProperty("serverName").GetString()).IsEqualTo(Hostile);
        await Assert.That(request.EnumerateObject().Count()).IsEqualTo(2);
    }

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task McpToggle_EscapesServerNameAndWritesBool(bool enabled)
    {
        JsonElement request = ControlRequests.McpToggle(Hostile, enabled);

        await Assert.That(request.GetProperty("subtype").GetString()).IsEqualTo("mcp_toggle");
        await Assert.That(request.GetProperty("serverName").GetString()).IsEqualTo(Hostile);
        await Assert.That(request.GetProperty("enabled").GetBoolean()).IsEqualTo(enabled);
        await Assert.That(request.EnumerateObject().Count()).IsEqualTo(3);
    }

    [Test]
    public async Task StopTask_EscapesTaskId()
    {
        JsonElement request = ControlRequests.StopTask(Hostile);

        await Assert.That(request.GetProperty("subtype").GetString()).IsEqualTo("stop_task");
        await Assert.That(request.GetProperty("task_id").GetString()).IsEqualTo(Hostile);
        await Assert.That(request.EnumerateObject().Count()).IsEqualTo(2);
    }

    [Test]
    [Arguments("interrupt")]
    [Arguments("mcp_status")]
    public async Task SubtypeOnly_WritesSubtype(string subtype)
    {
        JsonElement request = ControlRequests.SubtypeOnly(subtype);

        await Assert.That(request.GetProperty("subtype").GetString()).IsEqualTo(subtype);
        await Assert.That(request.EnumerateObject().Count()).IsEqualTo(1);
    }
}
