using System.Text.Json;
using ClaudeAgentSdk;

namespace ClaudeAgentSdk.Tests;

public class SetPermissionModeTests
{
    [Test]
    [Arguments(PermissionMode.Default, "default")]
    [Arguments(PermissionMode.AcceptEdits, "acceptEdits")]
    [Arguments(PermissionMode.Plan, "plan")]
    [Arguments(PermissionMode.BypassPermissions, "bypassPermissions")]
    public async Task SetPermissionModeAsync_SendsModeInControlRequest(PermissionMode mode, string expected)
    {
        var transport = CreateMockThatAcceptsControlRequests();
        await using var client = new ClaudeClient(transport: transport);
        await foreach (var _ in client.SendAsync("connect")) { }

        await client.SetPermissionModeAsync(mode);

        string request = transport.WrittenMessages.Single(m => m.Contains("set_permission_mode"));
        using var doc = JsonDocument.Parse(request);
        JsonElement body = doc.RootElement.GetProperty("request");
        await Assert.That(body.GetProperty("subtype").GetString()).IsEqualTo("set_permission_mode");
        await Assert.That(body.GetProperty("mode").GetString()).IsEqualTo(expected);
    }

    private static Transport.MockTransport CreateMockThatAcceptsControlRequests()
    {
        var transport = new Transport.MockTransport("");

        transport.OnWrite = json =>
        {
            if (json.Contains("\"type\":\"control_request\""))
            {
                using var doc = JsonDocument.Parse(json);
                string requestId = doc.RootElement.GetProperty("request_id").GetString()!;
                transport.EnqueueOutput("{\"type\":\"control_response\",\"response\":{\"subtype\":\"success\",\"request_id\":\"" + requestId + "\",\"response\":{}}}");
            }
            else if (json.Contains("\"type\":\"user\""))
            {
                transport.EnqueueOutput("""{"type":"result","subtype":"success","duration_ms":0,"duration_api_ms":0,"is_error":false,"num_turns":0,"session_id":"s1"}""");
            }
        };

        return transport;
    }
}
