using ClaudeAgentSdk;
using ClaudeAgentSdk.Agents;
using ClaudeAgentSdk.Mcp;

namespace ClaudeAgentSdk.Tests.Agents;

public class AgentSerializerTests
{
    [Test]
    public async Task Serialize_BasicAgent_ProducesCorrectJson()
    {
        var agents = new Dictionary<string, AgentDefinition>
        {
            ["code-reviewer"] = new AgentDefinition
            {
                Description = "Reviews code for quality",
                Prompt = "Review the code and suggest improvements"
            }
        };

        string json = AgentSerializer.Serialize(agents);

        await Assert.That(json).Contains("\"code-reviewer\"");
        await Assert.That(json).Contains("\"description\":\"Reviews code for quality\"");
        await Assert.That(json).Contains("\"prompt\":\"Review the code and suggest improvements\"");
    }

    [Test]
    public async Task Serialize_WithAllFields_IncludesOptionals()
    {
        var agents = new Dictionary<string, AgentDefinition>
        {
            ["full-agent"] = new AgentDefinition
            {
                Description = "Full agent",
                Prompt = "Do everything",
                Tools = ["Read", "Edit"],
                Model = AgentModel.Opus,
                Skills = ["tdd"],
                Memory = "project",
                McpServers = ["my-server"]
            }
        };

        string json = AgentSerializer.Serialize(agents);

        await Assert.That(json).Contains("\"tools\":[\"Read\",\"Edit\"]");
        await Assert.That(json).Contains("\"model\":\"opus\"");
        await Assert.That(json).Contains("\"skills\":[\"tdd\"]");
        await Assert.That(json).Contains("\"memory\":\"project\"");
        await Assert.That(json).Contains("\"mcpServers\":[\"my-server\"]");
    }

    [Test]
    public async Task Serialize_OmitsNullOptionals()
    {
        var agents = new Dictionary<string, AgentDefinition>
        {
            ["minimal"] = new AgentDefinition
            {
                Description = "Minimal",
                Prompt = "Do stuff"
            }
        };

        string json = AgentSerializer.Serialize(agents);

        await Assert.That(json).DoesNotContain("\"tools\"");
        await Assert.That(json).DoesNotContain("\"model\"");
        await Assert.That(json).DoesNotContain("\"skills\"");
        await Assert.That(json).DoesNotContain("\"memory\"");
        await Assert.That(json).DoesNotContain("\"mcpServers\"");
    }

    [Test]
    public async Task Serialize_MultipleAgents()
    {
        var agents = new Dictionary<string, AgentDefinition>
        {
            ["agent1"] = new AgentDefinition { Description = "First", Prompt = "P1" },
            ["agent2"] = new AgentDefinition { Description = "Second", Prompt = "P2" }
        };

        string json = AgentSerializer.Serialize(agents);

        await Assert.That(json).Contains("\"agent1\"");
        await Assert.That(json).Contains("\"agent2\"");
    }

    [Test]
    [Arguments(AgentModel.Sonnet, "sonnet")]
    [Arguments(AgentModel.Opus, "opus")]
    [Arguments(AgentModel.Haiku, "haiku")]
    [Arguments(AgentModel.Inherit, "inherit")]
    public async Task Serialize_ModelAlias_WritesAlias(string model, string expected)
    {
        var agents = new Dictionary<string, AgentDefinition>
        {
            ["a"] = new AgentDefinition { Description = "D", Prompt = "P", Model = model }
        };

        string json = AgentSerializer.Serialize(agents);
        await Assert.That(json).Contains($"\"model\":\"{expected}\"");
    }

    [Test]
    public async Task Serialize_FullModelId_WritesIdAsIs()
    {
        var agents = new Dictionary<string, AgentDefinition>
        {
            ["a"] = new AgentDefinition { Description = "D", Prompt = "P", Model = "claude-opus-4-5-20251101" }
        };

        string json = AgentSerializer.Serialize(agents);
        await Assert.That(json).Contains("\"model\":\"claude-opus-4-5-20251101\"");
    }

    [Test]
    public async Task Serialize_NewFields_UseCamelCaseKeysAsPython()
    {
        var agents = new Dictionary<string, AgentDefinition>
        {
            ["a"] = new AgentDefinition
            {
                Description = "D",
                Prompt = "P",
                DisallowedTools = ["Bash", "Write"],
                InitialPrompt = "Start here",
                MaxTurns = 5,
                Background = true,
                Effort = EffortLevel.High,
                PermissionMode = PermissionMode.AcceptEdits
            }
        };

        string json = AgentSerializer.Serialize(agents);

        await Assert.That(json).Contains("\"disallowedTools\":[\"Bash\",\"Write\"]");
        await Assert.That(json).Contains("\"initialPrompt\":\"Start here\"");
        await Assert.That(json).Contains("\"maxTurns\":5");
        await Assert.That(json).Contains("\"background\":true");
        await Assert.That(json).Contains("\"effort\":\"high\"");
        await Assert.That(json).Contains("\"permissionMode\":\"acceptEdits\"");
    }

    [Test]
    public async Task Serialize_BackgroundFalse_IsWritten()
    {
        var agents = new Dictionary<string, AgentDefinition>
        {
            ["a"] = new AgentDefinition { Description = "D", Prompt = "P", Background = false }
        };

        string json = AgentSerializer.Serialize(agents);
        await Assert.That(json).Contains("\"background\":false");
    }

    [Test]
    [Arguments(EffortLevel.Low, "low")]
    [Arguments(EffortLevel.Medium, "medium")]
    [Arguments(EffortLevel.High, "high")]
    [Arguments(EffortLevel.Max, "max")]
    public async Task Serialize_Effort_MapsToWireString(EffortLevel effort, string expected)
    {
        var agents = new Dictionary<string, AgentDefinition>
        {
            ["a"] = new AgentDefinition { Description = "D", Prompt = "P", Effort = effort }
        };

        string json = AgentSerializer.Serialize(agents);
        await Assert.That(json).Contains($"\"effort\":\"{expected}\"");
    }

    [Test]
    [Arguments(PermissionMode.Default, "default")]
    [Arguments(PermissionMode.AcceptEdits, "acceptEdits")]
    [Arguments(PermissionMode.Plan, "plan")]
    [Arguments(PermissionMode.BypassPermissions, "bypassPermissions")]
    public async Task Serialize_PermissionMode_MapsToWireString(PermissionMode mode, string expected)
    {
        var agents = new Dictionary<string, AgentDefinition>
        {
            ["a"] = new AgentDefinition { Description = "D", Prompt = "P", PermissionMode = mode }
        };

        string json = AgentSerializer.Serialize(agents);
        await Assert.That(json).Contains($"\"permissionMode\":\"{expected}\"");
    }

    [Test]
    public async Task Serialize_NewFieldsUnset_AreOmitted()
    {
        var agents = new Dictionary<string, AgentDefinition>
        {
            ["a"] = new AgentDefinition { Description = "D", Prompt = "P" }
        };

        string json = AgentSerializer.Serialize(agents);

        await Assert.That(json).DoesNotContain("\"disallowedTools\"");
        await Assert.That(json).DoesNotContain("\"initialPrompt\"");
        await Assert.That(json).DoesNotContain("\"maxTurns\"");
        await Assert.That(json).DoesNotContain("\"background\"");
        await Assert.That(json).DoesNotContain("\"effort\"");
        await Assert.That(json).DoesNotContain("\"permissionMode\"");
    }

    [Test]
    public async Task Serialize_InlineMcpServers_WritesNameThenInlineObjectsInOneArray()
    {
        var agents = new Dictionary<string, AgentDefinition>
        {
            ["a"] = new AgentDefinition
            {
                Description = "D",
                Prompt = "P",
                McpServers = ["shared"],
                InlineMcpServers = new Dictionary<string, McpServerConfig>
                {
                    ["local"] = new McpStdioServerConfig("node", ["server.js"]),
                    ["remote"] = new McpHttpServerConfig("https://example.com/mcp")
                }
            }
        };

        string json = AgentSerializer.Serialize(agents);

        await Assert.That(json).Contains(
            "\"mcpServers\":[\"shared\"," +
            "{\"local\":{\"type\":\"stdio\",\"command\":\"node\",\"args\":[\"server.js\"]}}," +
            "{\"remote\":{\"type\":\"http\",\"url\":\"https://example.com/mcp\"}}]");
    }

    [Test]
    public async Task Serialize_InlineMcpServersOnly_WritesArray()
    {
        var agents = new Dictionary<string, AgentDefinition>
        {
            ["a"] = new AgentDefinition
            {
                Description = "D",
                Prompt = "P",
                InlineMcpServers = new Dictionary<string, McpServerConfig>
                {
                    ["sse"] = new McpSseServerConfig("https://example.com/sse")
                }
            }
        };

        string json = AgentSerializer.Serialize(agents);

        await Assert.That(json).Contains(
            "\"mcpServers\":[{\"sse\":{\"type\":\"sse\",\"url\":\"https://example.com/sse\"}}]");
    }

    [Test]
    public async Task Serialize_InlineSdkServer_Throws()
    {
        var agents = new Dictionary<string, AgentDefinition>
        {
            ["a"] = new AgentDefinition
            {
                Description = "D",
                Prompt = "P",
                InlineMcpServers = new Dictionary<string, McpServerConfig>
                {
                    ["sdk"] = new McpSdkServerConfig { Name = "sdk", Tools = [] }
                }
            }
        };

        await Assert.That(() => AgentSerializer.Serialize(agents)).Throws<ArgumentException>();
    }
}
