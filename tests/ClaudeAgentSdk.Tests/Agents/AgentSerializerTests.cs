using ClaudeAgentSdk.Agents;

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
}
