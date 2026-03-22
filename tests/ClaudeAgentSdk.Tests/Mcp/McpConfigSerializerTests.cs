using ClaudeAgentSdk.Mcp;
using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk.Tests.Mcp;

public class McpConfigSerializerTests
{
    [Test]
    public async Task StdioServer_SerializesCorrectly()
    {
        var servers = new Dictionary<string, McpServerConfig>
        {
            ["my-server"] = new McpStdioServerConfig(
                Command: "npx",
                Args: ["-y", "@my/mcp-server"],
                Env: new Dictionary<string, string> { ["API_KEY"] = "secret" })
        };

        string json = McpConfigSerializer.Serialize(servers);

        await Assert.That(json).Contains("\"mcpServers\"");
        await Assert.That(json).Contains("\"my-server\"");
        await Assert.That(json).Contains("\"type\":\"stdio\"");
        await Assert.That(json).Contains("\"command\":\"npx\"");
        await Assert.That(json).Contains("\"-y\"");
        await Assert.That(json).Contains("\"API_KEY\":\"secret\"");
    }

    [Test]
    public async Task SseServer_SerializesCorrectly()
    {
        var servers = new Dictionary<string, McpServerConfig>
        {
            ["sse-server"] = new McpSseServerConfig(
                Url: "https://example.com/sse",
                Headers: new Dictionary<string, string> { ["Authorization"] = "Bearer tok" })
        };

        string json = McpConfigSerializer.Serialize(servers);

        await Assert.That(json).Contains("\"type\":\"sse\"");
        await Assert.That(json).Contains("\"url\":\"https://example.com/sse\"");
        await Assert.That(json).Contains("\"Authorization\":\"Bearer tok\"");
    }

    [Test]
    public async Task HttpServer_SerializesCorrectly()
    {
        var servers = new Dictionary<string, McpServerConfig>
        {
            ["http-server"] = new McpHttpServerConfig(Url: "https://example.com/mcp")
        };

        string json = McpConfigSerializer.Serialize(servers);

        await Assert.That(json).Contains("\"type\":\"http\"");
        await Assert.That(json).Contains("\"url\":\"https://example.com/mcp\"");
        // No headers key when null
        await Assert.That(json).DoesNotContain("\"headers\"");
    }

    [Test]
    public async Task MultipleServers_SerializesAll()
    {
        var servers = new Dictionary<string, McpServerConfig>
        {
            ["server1"] = new McpStdioServerConfig(Command: "cmd1"),
            ["server2"] = new McpHttpServerConfig(Url: "https://example.com")
        };

        string json = McpConfigSerializer.Serialize(servers);

        await Assert.That(json).Contains("\"server1\"");
        await Assert.That(json).Contains("\"server2\"");
    }

    [Test]
    public async Task StdioServer_NoOptionals_OmitsArgsAndEnv()
    {
        var servers = new Dictionary<string, McpServerConfig>
        {
            ["minimal"] = new McpStdioServerConfig(Command: "my-cmd")
        };

        string json = McpConfigSerializer.Serialize(servers);

        await Assert.That(json).Contains("\"command\":\"my-cmd\"");
        await Assert.That(json).DoesNotContain("\"args\"");
        await Assert.That(json).DoesNotContain("\"env\"");
    }

    [Test]
    public async Task CliArgsBuilder_McpServers_AddsMcpConfigArg()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            McpServers = new Dictionary<string, McpServerConfig>
            {
                ["test"] = new McpStdioServerConfig(Command: "test-cmd")
            }
        });

        int idx = args.IndexOf("--mcp-config");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).Contains("\"mcpServers\"");
        await Assert.That(args[idx + 1]).Contains("\"test\"");
    }

    [Test]
    public async Task CliArgsBuilder_NoMcpServers_OmitsArg()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions());

        await Assert.That(args).DoesNotContain("--mcp-config");
    }
}
