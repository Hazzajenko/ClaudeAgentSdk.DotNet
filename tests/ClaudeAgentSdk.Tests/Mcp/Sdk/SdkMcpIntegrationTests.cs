using System.Text;
using System.Text.Json;
using ClaudeAgentSdk.Control;
using ClaudeAgentSdk.Mcp;
using ClaudeAgentSdk.Mcp.Sdk;
using ClaudeAgentSdk.Transport;
using NSubstitute;

namespace ClaudeAgentSdk.Tests.Mcp.Sdk;

/// <summary>
/// End-to-end tests: tool definition → server creation → control protocol → JSONRPC → result.
/// </summary>
public class SdkMcpIntegrationTests
{
    [Test]
    public async Task FullRoundTrip_DefineToolAndCallViaControlProtocol()
    {
        // 1. Define tool
        var addTool = SdkMcpTool.Create("add", "Add two numbers",
            McpJsonSchema.Object(
                McpJsonSchema.Property("a", McpJsonSchema.Number()),
                McpJsonSchema.Property("b", McpJsonSchema.Number())
            ),
            async (args, ct) =>
            {
                double sum = args.GetProperty("a").GetDouble() + args.GetProperty("b").GetDouble();
                return McpToolResult.Text(sum.ToString());
            });

        // 2. Create server config
        var serverConfig = McpSdkServerConfig.Create("calculator", [addTool]);

        // 3. Create handler (simulating what ClaudeClient.WireSdkMcpServers does)
        var requestHandler = new SdkMcpRequestHandler(serverConfig);

        // 4. Wire to control protocol handler
        var transport = Substitute.For<ITransport>();
        var writtenJson = new List<string>();
        transport.WriteAsync(Arg.Do<string>(s => writtenJson.Add(s)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controlHandler = new ControlProtocolHandler(transport);
        controlHandler.OnMcpMessage = async (serverName, message, ct) =>
        {
            await Assert.That(serverName).IsEqualTo("calculator");
            return await requestHandler.HandleAsync(message, ct);
        };

        // 5. Simulate CLI sending initialize
        using var initDoc = JsonDocument.Parse("""
            {
                "request_id": "req_init",
                "request": {
                    "subtype": "mcp_message",
                    "server_name": "calculator",
                    "message": {"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}
                }
            }
            """);
        await controlHandler.HandleControlRequestAsync(initDoc.RootElement, CancellationToken.None);

        // Verify initialize response
        using var initResponse = JsonDocument.Parse(writtenJson[0]);
        var initResult = initResponse.RootElement
            .GetProperty("response").GetProperty("response")
            .GetProperty("mcp_response").GetProperty("result");
        await Assert.That(initResult.GetProperty("serverInfo").GetProperty("name").GetString())
            .IsEqualTo("calculator");

        writtenJson.Clear();

        // 6. Simulate CLI sending tools/list
        using var listDoc = JsonDocument.Parse("""
            {
                "request_id": "req_list",
                "request": {
                    "subtype": "mcp_message",
                    "server_name": "calculator",
                    "message": {"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}
                }
            }
            """);
        await controlHandler.HandleControlRequestAsync(listDoc.RootElement, CancellationToken.None);

        using var listResponse = JsonDocument.Parse(writtenJson[0]);
        var tools = listResponse.RootElement
            .GetProperty("response").GetProperty("response")
            .GetProperty("mcp_response").GetProperty("result").GetProperty("tools");
        await Assert.That(tools.GetArrayLength()).IsEqualTo(1);
        await Assert.That(tools[0].GetProperty("name").GetString()).IsEqualTo("add");

        writtenJson.Clear();

        // 7. Simulate CLI sending tools/call
        using var callDoc = JsonDocument.Parse("""
            {
                "request_id": "req_call",
                "request": {
                    "subtype": "mcp_message",
                    "server_name": "calculator",
                    "message": {"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"add","arguments":{"a":7,"b":3}}}
                }
            }
            """);
        await controlHandler.HandleControlRequestAsync(callDoc.RootElement, CancellationToken.None);

        using var callResponse = JsonDocument.Parse(writtenJson[0]);
        var callResult = callResponse.RootElement
            .GetProperty("response").GetProperty("response")
            .GetProperty("mcp_response").GetProperty("result");
        var content = callResult.GetProperty("content");
        await Assert.That(content[0].GetProperty("text").GetString()).IsEqualTo("10");
    }

    [Test]
    public async Task SdkServerConfig_ExcludedFromCliArgs()
    {
        var tool = SdkMcpTool.Create("echo", "Echo", McpJsonSchema.Object(),
            async (args, ct) => McpToolResult.Text("ok"));

        var options = new ClaudeClientOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["sdk-server"] = McpSdkServerConfig.Create("sdk-server", [tool]),
                ["external"] = new McpStdioServerConfig(Command: "my-cmd")
            }
        };

        var args = CliArgsBuilder.Build(options);

        int idx = args.IndexOf("--mcp-config");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);

        string mcpConfig = args[idx + 1];
        await Assert.That(mcpConfig).Contains("\"external\"");
        await Assert.That(mcpConfig).DoesNotContain("\"sdk-server\"");
    }

    [Test]
    public async Task MultipleToolServer_AllToolsAccessible()
    {
        var addTool = SdkMcpTool.Create("add", "Add", McpJsonSchema.Object(),
            async (args, ct) => McpToolResult.Text("sum"));
        var mulTool = SdkMcpTool.Create("multiply", "Multiply", McpJsonSchema.Object(),
            async (args, ct) => McpToolResult.Text("product"));

        var handler = new SdkMcpRequestHandler(
            McpSdkServerConfig.Create("math", [addTool, mulTool]));

        // Verify both tools listed
        using var listMsg = JsonDocument.Parse("""{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}""");
        var listResp = await handler.HandleAsync(listMsg.RootElement, CancellationToken.None);
        await Assert.That(listResp.GetProperty("result").GetProperty("tools").GetArrayLength()).IsEqualTo(2);

        // Call first tool
        using var call1 = JsonDocument.Parse("""{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"add","arguments":{}}}""");
        var resp1 = await handler.HandleAsync(call1.RootElement, CancellationToken.None);
        await Assert.That(resp1.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString())
            .IsEqualTo("sum");

        // Call second tool
        using var call2 = JsonDocument.Parse("""{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"multiply","arguments":{}}}""");
        var resp2 = await handler.HandleAsync(call2.RootElement, CancellationToken.None);
        await Assert.That(resp2.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString())
            .IsEqualTo("product");
    }

    [Test]
    public async Task ErrorTool_ReturnsIsError()
    {
        var tool = SdkMcpTool.Create("validate", "Validate",
            McpJsonSchema.Object(McpJsonSchema.Property("input", McpJsonSchema.String())),
            async (args, ct) =>
            {
                string input = args.GetProperty("input").GetString()!;
                return input.Length == 0
                    ? McpToolResult.Error("Input cannot be empty")
                    : McpToolResult.Text($"Valid: {input}");
            });

        var handler = new SdkMcpRequestHandler(McpSdkServerConfig.Create("validator", [tool]));

        // Call with empty input
        using var callMsg = JsonDocument.Parse("""
            {"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"validate","arguments":{"input":""}}}
            """);
        var resp = await handler.HandleAsync(callMsg.RootElement, CancellationToken.None);

        var result = resp.GetProperty("result");
        await Assert.That(result.GetProperty("isError").GetBoolean()).IsTrue();
        await Assert.That(result.GetProperty("content")[0].GetProperty("text").GetString())
            .IsEqualTo("Input cannot be empty");
    }
}
