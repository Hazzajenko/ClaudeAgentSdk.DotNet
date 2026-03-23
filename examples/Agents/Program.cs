using ClaudeAgentSdk;
using ClaudeAgentSdk.Agents;
using ClaudeAgentSdk.Messages;

await CodeReviewerExample();
await DocumentationWriterExample();
await MultipleAgentsExample();

async Task CodeReviewerExample()
{
    Console.WriteLine("=== Code Reviewer Agent Example ===");

    var options = new ClaudeClientOptions
    {
        Agents = new Dictionary<string, AgentDefinition>
        {
            ["code-reviewer"] = new()
            {
                Description = "Reviews code for best practices and potential issues",
                Prompt = "You are a code reviewer. Analyze code for bugs, performance issues, "
                    + "security vulnerabilities, and adherence to best practices. "
                    + "Provide constructive feedback.",
                Tools = ["Read", "Grep"],
                Model = AgentModel.Sonnet,
            },
        },
    };

    await foreach (var message in Claude.QueryAsync(
        "Use the code-reviewer agent to review the code in src/ClaudeAgentSdk/Claude.cs",
        options))
    {
        switch (message)
        {
            case AssistantMessage { Content: var content }:
                foreach (var block in content)
                {
                    if (block is TextBlock { Text: var text })
                        Console.WriteLine($"Claude: {text}");
                }
                break;

            case ResultMessage { TotalCostUsd: > 0 and var cost }:
                Console.WriteLine($"\nCost: ${cost:F4}");
                break;
        }
    }

    Console.WriteLine();
}

async Task DocumentationWriterExample()
{
    Console.WriteLine("=== Documentation Writer Agent Example ===");

    var options = new ClaudeClientOptions
    {
        Agents = new Dictionary<string, AgentDefinition>
        {
            ["doc-writer"] = new()
            {
                Description = "Writes comprehensive documentation",
                Prompt = "You are a technical documentation expert. Write clear, comprehensive "
                    + "documentation with examples. Focus on clarity and completeness.",
                Tools = ["Read", "Write", "Edit"],
                Model = AgentModel.Sonnet,
            },
        },
    };

    await foreach (var message in Claude.QueryAsync(
        "Use the doc-writer agent to explain what AgentDefinition is used for",
        options))
    {
        switch (message)
        {
            case AssistantMessage { Content: var content }:
                foreach (var block in content)
                {
                    if (block is TextBlock { Text: var text })
                        Console.WriteLine($"Claude: {text}");
                }
                break;

            case ResultMessage { TotalCostUsd: > 0 and var cost }:
                Console.WriteLine($"\nCost: ${cost:F4}");
                break;
        }
    }

    Console.WriteLine();
}

async Task MultipleAgentsExample()
{
    Console.WriteLine("=== Multiple Agents Example ===");

    var options = new ClaudeClientOptions
    {
        Agents = new Dictionary<string, AgentDefinition>
        {
            ["analyzer"] = new()
            {
                Description = "Analyzes code structure and patterns",
                Prompt = "You are a code analyzer. Examine code structure, patterns, and architecture.",
                Tools = ["Read", "Grep", "Glob"],
            },
            ["tester"] = new()
            {
                Description = "Creates and runs tests",
                Prompt = "You are a testing expert. Write comprehensive tests and ensure code quality.",
                Tools = ["Read", "Write", "Bash"],
                Model = AgentModel.Sonnet,
            },
        },
    };

    await foreach (var message in Claude.QueryAsync(
        "Use the analyzer agent to find all C# files in the examples/ directory",
        options))
    {
        switch (message)
        {
            case AssistantMessage { Content: var content }:
                foreach (var block in content)
                {
                    if (block is TextBlock { Text: var text })
                        Console.WriteLine($"Claude: {text}");
                }
                break;

            case ResultMessage { TotalCostUsd: > 0 and var cost }:
                Console.WriteLine($"\nCost: ${cost:F4}");
                break;
        }
    }

    Console.WriteLine();
}
