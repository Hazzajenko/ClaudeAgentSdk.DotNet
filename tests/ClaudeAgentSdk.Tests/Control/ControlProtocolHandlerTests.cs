using System.Text.Json;
using ClaudeAgentSdk.Control;
using ClaudeAgentSdk.Transport;
using NSubstitute;

namespace ClaudeAgentSdk.Tests.Control;

public class ControlProtocolHandlerTests
{
    [Test]
    public async Task HandleControlResponse_RoutesToPendingRequest()
    {
        var transport = Substitute.For<ITransport>();
        var writtenJson = new List<string>();
        transport.WriteAsync(Arg.Do<string>(s => writtenJson.Add(s)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);

        // Start a control request (won't complete until response received)
        using var doc = JsonDocument.Parse("""{"subtype":"interrupt"}""");
        var requestTask = handler.SendControlRequestAsync(doc.RootElement.Clone());

        // Give it a moment to send
        await Task.Delay(50);

        // Extract the request_id from what was written
        await Assert.That(writtenJson.Count).IsEqualTo(1);
        using var sentDoc = JsonDocument.Parse(writtenJson[0]);
        string requestId = sentDoc.RootElement.GetProperty("request_id").GetString()!;

        // Simulate CLI response
        using var responseDoc = JsonDocument.Parse($$"""
            {
                "type": "control_response",
                "response": {
                    "subtype": "success",
                    "request_id": "{{requestId}}",
                    "response": {"status": "ok"}
                }
            }
            """);
        handler.HandleControlResponse(responseDoc.RootElement);

        // Request should now complete
        var result = await requestTask;
        await Assert.That(result.GetProperty("status").GetString()).IsEqualTo("ok");
    }

    [Test]
    public async Task HandleControlResponse_ErrorResponse_ThrowsException()
    {
        var transport = Substitute.For<ITransport>();
        var writtenJson = new List<string>();
        transport.WriteAsync(Arg.Do<string>(s => writtenJson.Add(s)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);

        using var doc = JsonDocument.Parse("""{"subtype":"interrupt"}""");
        var requestTask = handler.SendControlRequestAsync(doc.RootElement.Clone());

        await Task.Delay(50);
        using var sentDoc = JsonDocument.Parse(writtenJson[0]);
        string requestId = sentDoc.RootElement.GetProperty("request_id").GetString()!;

        using var responseDoc = JsonDocument.Parse($$"""
            {
                "type": "control_response",
                "response": {
                    "subtype": "error",
                    "request_id": "{{requestId}}",
                    "error": "Something went wrong"
                }
            }
            """);
        handler.HandleControlResponse(responseDoc.RootElement);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await requestTask);
    }

    [Test]
    public async Task SendControlRequest_WritesCorrectJsonFormat()
    {
        var transport = Substitute.For<ITransport>();
        var writtenJson = new List<string>();
        transport.WriteAsync(Arg.Do<string>(s => writtenJson.Add(s)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);

        using var doc = JsonDocument.Parse("""{"subtype":"set_model","model":"claude-sonnet-4-5"}""");
        // Don't await — we just want to verify what was sent
        _ = handler.SendControlRequestAsync(doc.RootElement.Clone(), TimeSpan.FromMilliseconds(100));

        await Task.Delay(50);

        await Assert.That(writtenJson.Count).IsEqualTo(1);
        using var sentDoc = JsonDocument.Parse(writtenJson[0]);
        var root = sentDoc.RootElement;
        await Assert.That(root.GetProperty("type").GetString()).IsEqualTo("control_request");
        await Assert.That(root.TryGetProperty("request_id", out _)).IsTrue();
        await Assert.That(root.GetProperty("request").GetProperty("subtype").GetString()).IsEqualTo("set_model");
    }

    [Test]
    public async Task HandleControlRequestAsync_HookCallback_InvokesCallback()
    {
        var transport = Substitute.For<ITransport>();
        transport.WriteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);

        string? receivedCallbackId = null;
        handler.OnHookCallback = (callbackId, input, toolUseId, ct) =>
        {
            receivedCallbackId = callbackId;
            using var stream = new MemoryStream();
            using (var w = new Utf8JsonWriter(stream))
            {
                w.WriteStartObject();
                w.WriteBoolean("continue", true);
                w.WriteEndObject();
            }
            return Task.FromResult(JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(stream.ToArray())).RootElement.Clone());
        };

        using var requestDoc = JsonDocument.Parse("""
            {
                "request_id": "req_1",
                "request": {
                    "subtype": "hook_callback",
                    "callback_id": "hook_0",
                    "input": {"tool_name": "Bash"},
                    "tool_use_id": "tool_1"
                }
            }
            """);

        await handler.HandleControlRequestAsync(requestDoc.RootElement, CancellationToken.None);

        await Assert.That(receivedCallbackId).IsEqualTo("hook_0");
        // Verify response was written back
        await transport.Received(1).WriteAsync(Arg.Is<string>(s => s.Contains("success")), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task FailAllPending_CancelsOutstandingRequests()
    {
        var transport = Substitute.For<ITransport>();
        transport.WriteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

        var handler = new ControlProtocolHandler(transport);

        using var doc = JsonDocument.Parse("""{"subtype":"interrupt"}""");
        var task = handler.SendControlRequestAsync(doc.RootElement.Clone());

        await Task.Delay(50);
        handler.FailAllPending(new OperationCanceledException("Shutdown"));

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
    }
}
