using System.Text.Json;
using ClaudeAgentSdk.Mcp;
using ClaudeAgentSdk.Mcp.Sdk;

namespace ClaudeAgentSdk.Tests.Mcp.Sdk;

public class SdkMcpRequestHandlerTests
{
    private static SdkMcpTool CreateAddTool() => SdkMcpTool.Create(
        "add", "Add two numbers",
        McpJsonSchema.Object(
            McpJsonSchema.Property("a", McpJsonSchema.Number()),
            McpJsonSchema.Property("b", McpJsonSchema.Number())
        ),
        async (args, ct) =>
        {
            double sum = args.GetProperty("a").GetDouble() + args.GetProperty("b").GetDouble();
            return McpToolResult.Text(sum.ToString());
        });

    private static McpSdkServerConfig CreateServerWithTools(params SdkMcpTool[] tools) =>
        McpSdkServerConfig.Create("test-server", tools, "2.0.0");

    [Test]
    public async Task Initialize_ReturnsCapabilitiesAndServerInfo()
    {
        var handler = new SdkMcpRequestHandler(CreateServerWithTools());

        using var msg = JsonDocument.Parse("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""");
        var response = await handler.HandleAsync(msg.RootElement, CancellationToken.None);

        await Assert.That(response.GetProperty("jsonrpc").GetString()).IsEqualTo("2.0");
        await Assert.That(response.GetProperty("id").GetInt32()).IsEqualTo(1);

        var result = response.GetProperty("result");
        await Assert.That(result.GetProperty("protocolVersion").GetString()).IsEqualTo("2024-11-05");

        var capabilities = result.GetProperty("capabilities");
        await Assert.That(capabilities.TryGetProperty("tools", out _)).IsTrue();

        var serverInfo = result.GetProperty("serverInfo");
        await Assert.That(serverInfo.GetProperty("name").GetString()).IsEqualTo("test-server");
        await Assert.That(serverInfo.GetProperty("version").GetString()).IsEqualTo("2.0.0");
    }

    [Test]
    public async Task ToolsList_ReturnsRegisteredTools()
    {
        var handler = new SdkMcpRequestHandler(CreateServerWithTools(CreateAddTool()));

        using var msg = JsonDocument.Parse("""{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}""");
        var response = await handler.HandleAsync(msg.RootElement, CancellationToken.None);

        var tools = response.GetProperty("result").GetProperty("tools");
        await Assert.That(tools.GetArrayLength()).IsEqualTo(1);

        var tool = tools[0];
        await Assert.That(tool.GetProperty("name").GetString()).IsEqualTo("add");
        await Assert.That(tool.GetProperty("description").GetString()).IsEqualTo("Add two numbers");
        await Assert.That(tool.GetProperty("inputSchema").GetProperty("type").GetString()).IsEqualTo("object");
    }

    [Test]
    public async Task ToolsList_WithAnnotations_WritesMcpHintNames()
    {
        var tool = SdkMcpTool.Create("reader", "Read data",
            McpJsonSchema.Object(),
            async (args, ct) => McpToolResult.Text("data"),
            new McpToolAnnotations
            {
                Title = "Reader",
                ReadOnly = true,
                Destructive = false,
                Idempotent = true,
                OpenWorld = false
            });

        var handler = new SdkMcpRequestHandler(CreateServerWithTools(tool));

        using var msg = JsonDocument.Parse("""{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""");
        var response = await handler.HandleAsync(msg.RootElement, CancellationToken.None);

        var annotations = response.GetProperty("result").GetProperty("tools")[0].GetProperty("annotations");
        await Assert.That(annotations.GetProperty("title").GetString()).IsEqualTo("Reader");
        await Assert.That(annotations.GetProperty("readOnlyHint").GetBoolean()).IsTrue();
        await Assert.That(annotations.GetProperty("destructiveHint").GetBoolean()).IsFalse();
        await Assert.That(annotations.GetProperty("idempotentHint").GetBoolean()).IsTrue();
        await Assert.That(annotations.GetProperty("openWorldHint").GetBoolean()).IsFalse();
        await Assert.That(annotations.TryGetProperty("readOnly", out _)).IsFalse();
        await Assert.That(annotations.TryGetProperty("destructive", out _)).IsFalse();
        await Assert.That(annotations.TryGetProperty("openWorld", out _)).IsFalse();
    }

    [Test]
    public async Task ToolsList_WithPartialAnnotations_OmitsUnsetHints()
    {
        var tool = SdkMcpTool.Create("reader", "Read data",
            McpJsonSchema.Object(),
            async (args, ct) => McpToolResult.Text("data"),
            new McpToolAnnotations { ReadOnly = true });

        var handler = new SdkMcpRequestHandler(CreateServerWithTools(tool));

        using var msg = JsonDocument.Parse("""{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""");
        var response = await handler.HandleAsync(msg.RootElement, CancellationToken.None);

        var annotations = response.GetProperty("result").GetProperty("tools")[0].GetProperty("annotations");
        await Assert.That(annotations.EnumerateObject().Count()).IsEqualTo(1);
        await Assert.That(annotations.GetProperty("readOnlyHint").GetBoolean()).IsTrue();
    }

    [Test]
    public async Task ToolsCall_InvokesHandlerAndReturnsResult()
    {
        var handler = new SdkMcpRequestHandler(CreateServerWithTools(CreateAddTool()));

        using var msg = JsonDocument.Parse("""
            {"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"add","arguments":{"a":2,"b":3}}}
            """);
        var response = await handler.HandleAsync(msg.RootElement, CancellationToken.None);

        var result = response.GetProperty("result");
        var content = result.GetProperty("content");
        await Assert.That(content.GetArrayLength()).IsEqualTo(1);
        await Assert.That(content[0].GetProperty("type").GetString()).IsEqualTo("text");
        await Assert.That(content[0].GetProperty("text").GetString()).IsEqualTo("5");

        await Assert.That(result.TryGetProperty("isError", out _)).IsFalse();
        await Assert.That(result.TryGetProperty("is_error", out _)).IsFalse();
    }

    [Test]
    public async Task ToolsCall_ErrorResult_SetsIsError()
    {
        var errorTool = SdkMcpTool.Create("fail", "Always fails",
            McpJsonSchema.Object(),
            async (args, ct) => McpToolResult.Error("something broke"));

        var handler = new SdkMcpRequestHandler(CreateServerWithTools(errorTool));

        using var msg = JsonDocument.Parse("""
            {"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"fail","arguments":{}}}
            """);
        var response = await handler.HandleAsync(msg.RootElement, CancellationToken.None);

        var result = response.GetProperty("result");
        await Assert.That(result.GetProperty("isError").GetBoolean()).IsTrue();
        await Assert.That(result.TryGetProperty("is_error", out _)).IsFalse();
        await Assert.That(result.GetProperty("content")[0].GetProperty("text").GetString()).IsEqualTo("something broke");
    }

    [Test]
    public async Task ToolsCall_UnknownTool_ReturnsIsErrorResult()
    {
        var handler = new SdkMcpRequestHandler(CreateServerWithTools());

        using var msg = JsonDocument.Parse("""
            {"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"nonexistent","arguments":{}}}
            """);
        var response = await handler.HandleAsync(msg.RootElement, CancellationToken.None);

        await Assert.That(response.TryGetProperty("error", out _)).IsFalse();
        await Assert.That(response.GetProperty("id").GetInt32()).IsEqualTo(5);
        var result = response.GetProperty("result");
        await Assert.That(result.GetProperty("isError").GetBoolean()).IsTrue();
        await Assert.That(result.GetProperty("content")[0].GetProperty("type").GetString()).IsEqualTo("text");
        await Assert.That(result.GetProperty("content")[0].GetProperty("text").GetString())
            .IsEqualTo("Tool 'nonexistent' not found");
    }

    [Test]
    public async Task ToolsCall_HandlerThrows_ReturnsIsErrorResultWithMessage()
    {
        var throwTool = SdkMcpTool.Create("boom", "Throws",
            McpJsonSchema.Object(),
            async (args, ct) => throw new InvalidOperationException("kaboom"));

        var handler = new SdkMcpRequestHandler(CreateServerWithTools(throwTool));

        using var msg = JsonDocument.Parse("""
            {"jsonrpc":"2.0","id":6,"method":"tools/call","params":{"name":"boom","arguments":{}}}
            """);
        var response = await handler.HandleAsync(msg.RootElement, CancellationToken.None);

        await Assert.That(response.TryGetProperty("error", out _)).IsFalse();
        var result = response.GetProperty("result");
        await Assert.That(result.GetProperty("isError").GetBoolean()).IsTrue();
        await Assert.That(result.GetProperty("content")[0].GetProperty("text").GetString()).IsEqualTo("kaboom");
    }

    [Test]
    public async Task ToolsCall_HandlerCancelledByToken_ReturnsJsonRpcError()
    {
        using var cts = new CancellationTokenSource();
        var tool = SdkMcpTool.Create("slow", "Cancelled",
            McpJsonSchema.Object(),
            async (args, ct) =>
            {
                await cts.CancelAsync();
                ct.ThrowIfCancellationRequested();
                return McpToolResult.Text("unreachable");
            });

        var handler = new SdkMcpRequestHandler(CreateServerWithTools(tool));

        using var msg = JsonDocument.Parse("""
            {"jsonrpc":"2.0","id":9,"method":"tools/call","params":{"name":"slow","arguments":{}}}
            """);
        var response = await handler.HandleAsync(msg.RootElement, cts.Token);

        await Assert.That(response.TryGetProperty("result", out _)).IsFalse();
        await Assert.That(response.GetProperty("error").GetProperty("code").GetInt32()).IsEqualTo(-32603);
    }

    [Test]
    public async Task NotificationsInitialized_ReturnsEmptyResult()
    {
        var handler = new SdkMcpRequestHandler(CreateServerWithTools());

        using var msg = JsonDocument.Parse("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
        var response = await handler.HandleAsync(msg.RootElement, CancellationToken.None);

        await Assert.That(response.GetProperty("jsonrpc").GetString()).IsEqualTo("2.0");
        await Assert.That(response.TryGetProperty("result", out _)).IsTrue();
    }

    [Test]
    public async Task UnknownMethod_ReturnsMethodNotFoundError()
    {
        var handler = new SdkMcpRequestHandler(CreateServerWithTools());

        using var msg = JsonDocument.Parse("""{"jsonrpc":"2.0","id":7,"method":"unknown/method","params":{}}""");
        var response = await handler.HandleAsync(msg.RootElement, CancellationToken.None);

        var error = response.GetProperty("error");
        await Assert.That(error.GetProperty("code").GetInt32()).IsEqualTo(-32601);
        await Assert.That(error.GetProperty("message").GetString()!).Contains("unknown/method");
    }

    [Test]
    public async Task ToolsCall_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        bool? cancelledBefore = null;
        bool? cancelledAfter = null;

        var tool = SdkMcpTool.Create("check-ct", "Check CT",
            McpJsonSchema.Object(),
            async (args, ct) =>
            {
                cancelledBefore = ct.IsCancellationRequested;
                await cts.CancelAsync();
                cancelledAfter = ct.IsCancellationRequested;
                return McpToolResult.Text("ok");
            });

        var handler = new SdkMcpRequestHandler(CreateServerWithTools(tool));

        using var msg = JsonDocument.Parse("""
            {"jsonrpc":"2.0","id":8,"method":"tools/call","params":{"name":"check-ct","arguments":{}}}
            """);
        await handler.HandleAsync(msg.RootElement, cts.Token);

        await Assert.That(cancelledBefore).IsFalse();
        await Assert.That(cancelledAfter).IsTrue();
    }

    [Test]
    [Arguments("8", "8")]
    [Arguments("\"call-8\"", "\"call-8\"")]
    public async Task NotificationsCancelled_CancelsRunningHandlerToken(string callId, string requestId)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tool = SdkMcpTool.Create("wait", "Wait until cancelled",
            McpJsonSchema.Object(),
            async (args, ct) =>
            {
                started.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
                return McpToolResult.Text("unreachable");
            });

        var handler = new SdkMcpRequestHandler(CreateServerWithTools(tool));

        using var call = JsonDocument.Parse(
            """{"jsonrpc":"2.0","id":""" + callId
            + ""","method":"tools/call","params":{"name":"wait","arguments":{}}}""");
        Task<JsonElement> callTask = handler.HandleAsync(call.RootElement, CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using var cancel = JsonDocument.Parse($$$"""
            {"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":{{{requestId}}},"reason":"interrupt"}}
            """);
        var ack = await handler.HandleAsync(cancel.RootElement, CancellationToken.None);
        var response = await callTask.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(ack.TryGetProperty("result", out _)).IsTrue();
        await Assert.That(ack.TryGetProperty("id", out _)).IsFalse();
        await Assert.That(response.GetProperty("id").GetRawText()).IsEqualTo(callId);
        await Assert.That(response.TryGetProperty("result", out _)).IsFalse();
        var error = response.GetProperty("error");
        await Assert.That(error.GetProperty("code").GetInt32()).IsEqualTo(-32800);
        await Assert.That(error.GetProperty("message").GetString()).IsEqualTo("Request cancelled");
    }

    [Test]
    public async Task NotificationsCancelled_HandlerReturnsAnyway_ReturnsCancelledError()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tool = SdkMcpTool.Create("stubborn", "Ignores its token",
            McpJsonSchema.Object(),
            async (args, ct) =>
            {
                started.SetResult();
                await release.Task;
                return McpToolResult.Text("late result");
            });

        var handler = new SdkMcpRequestHandler(CreateServerWithTools(tool));

        using var call = JsonDocument.Parse("""
            {"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"stubborn","arguments":{}}}
            """);
        Task<JsonElement> callTask = handler.HandleAsync(call.RootElement, CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using var cancel = JsonDocument.Parse("""
            {"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":3}}
            """);
        await handler.HandleAsync(cancel.RootElement, CancellationToken.None);
        release.SetResult();
        var response = await callTask.WaitAsync(TimeSpan.FromSeconds(5));

        await Assert.That(response.GetProperty("error").GetProperty("code").GetInt32()).IsEqualTo(-32800);
    }

    [Test]
    public async Task NotificationsCancelled_UnknownRequest_ReturnsEmptyResult()
    {
        var handler = new SdkMcpRequestHandler(CreateServerWithTools());

        using var cancel = JsonDocument.Parse("""
            {"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":99}}
            """);
        var ack = await handler.HandleAsync(cancel.RootElement, CancellationToken.None);

        await Assert.That(ack.TryGetProperty("error", out _)).IsFalse();
        await Assert.That(ack.TryGetProperty("result", out _)).IsTrue();
    }

    [Test]
    public async Task NotificationsCancelled_AfterCallEnds_DoesNotThrow()
    {
        var handler = new SdkMcpRequestHandler(CreateServerWithTools(CreateAddTool()));

        using var call = JsonDocument.Parse("""
            {"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"add","arguments":{"a":1,"b":2}}}
            """);
        var response = await handler.HandleAsync(call.RootElement, CancellationToken.None);

        using var cancel = JsonDocument.Parse("""
            {"jsonrpc":"2.0","method":"notifications/cancelled","params":{"requestId":4}}
            """);
        var ack = await handler.HandleAsync(cancel.RootElement, CancellationToken.None);

        await Assert.That(response.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()).IsEqualTo("3");
        await Assert.That(ack.TryGetProperty("result", out _)).IsTrue();
    }

    [Test]
    [Arguments("""{"jsonrpc":"2.0","id":"req-abc","method":"initialize","params":{}}""")]
    [Arguments("""{"jsonrpc":"2.0","id":"req-abc","method":"tools/list","params":{}}""")]
    [Arguments("""{"jsonrpc":"2.0","id":"req-abc","method":"tools/call","params":{"name":"add","arguments":{"a":1,"b":2}}}""")]
    [Arguments("""{"jsonrpc":"2.0","id":"req-abc","method":"tools/call","params":{"name":"missing","arguments":{}}}""")]
    [Arguments("""{"jsonrpc":"2.0","id":"req-abc","method":"unknown/method","params":{}}""")]
    public async Task StringId_IsEchoedUnchanged(string json)
    {
        var handler = new SdkMcpRequestHandler(CreateServerWithTools(CreateAddTool()));

        using var msg = JsonDocument.Parse(json);
        var response = await handler.HandleAsync(msg.RootElement, CancellationToken.None);

        var id = response.GetProperty("id");
        await Assert.That(id.ValueKind).IsEqualTo(JsonValueKind.String);
        await Assert.That(id.GetString()).IsEqualTo("req-abc");
    }

    [Test]
    public async Task LargeNumericId_IsEchoedUnchanged()
    {
        var handler = new SdkMcpRequestHandler(CreateServerWithTools());

        using var msg = JsonDocument.Parse("""{"jsonrpc":"2.0","id":12345678901234,"method":"initialize","params":{}}""");
        var response = await handler.HandleAsync(msg.RootElement, CancellationToken.None);

        await Assert.That(response.GetProperty("id").GetRawText()).IsEqualTo("12345678901234");
    }

    [Test]
    public async Task ToolsCall_MissingParams_ReturnsJsonRpcErrorWithId()
    {
        var handler = new SdkMcpRequestHandler(CreateServerWithTools());

        using var msg = JsonDocument.Parse("""{"jsonrpc":"2.0","id":"x","method":"tools/call"}""");
        var response = await handler.HandleAsync(msg.RootElement, CancellationToken.None);

        await Assert.That(response.GetProperty("id").GetString()).IsEqualTo("x");
        await Assert.That(response.GetProperty("error").GetProperty("code").GetInt32()).IsEqualTo(-32603);
    }
}
