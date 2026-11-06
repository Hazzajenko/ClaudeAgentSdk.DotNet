using ClaudeAgentSdk;
using ClaudeAgentSdk.Agents;
using ClaudeAgentSdk.Messages;

await CodeReviewerExample();
await DocumentationWriterExample();
await MultipleAgentsExample();

async Task CodeReviewerExample()
{
    Console.WriteLine("=== Code Reviewer Agent Example ===");

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

    var options = new ClaudeClientOptions
    {
        PermissionMode = PermissionMode.AcceptEdits,
        MaxTurns = 5,
        // MessageCallback = msg => Console.Error.WriteLine(Claude.FormatMessage(msg)),
        Agents = new Dictionary<string, AgentDefinition>
        {
            ["code-reviewer"] = new()
            {
                Description = "Reviews code for best practices and potential issues",
                Prompt = "You are a code reviewer. Be concise.",
                Tools = ["Read"],
                Model = AgentModel.Sonnet,
            },
        },
    };

    try
    {
        await foreach (var message in Claude.QueryAsync(
            "Use the code-reviewer agent to summarize src/ClaudeAgentSdk/Claude.cs in 2 sentences",
            options, cancellationToken: cts.Token))
        {
            switch (message)
            {
                case AssistantMessage { Text: var text }:
                    Console.WriteLine($"Claude: {text}");
                    break;

                case ResultMessage { TotalCostUsd: > 0 and var cost }:
                    Console.WriteLine($"\nCost: ${cost:F4}");
                    break;
            }
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("\nTIMEOUT: exceeded 60s");
    }

    Console.WriteLine();
}

async Task DocumentationWriterExample()
{
    Console.WriteLine("=== Documentation Writer Agent Example ===");

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

    var options = new ClaudeClientOptions
    {
        PermissionMode = PermissionMode.AcceptEdits,
        MaxTurns = 5,
        Agents = new Dictionary<string, AgentDefinition>
        {
            ["doc-writer"] = new()
            {
                Description = "Explains code concepts concisely",
                Prompt = "You are a documentation writer. Be concise.",
                Tools = ["Read"],
                Model = AgentModel.Sonnet,
            },
        },
    };

    try
    {
        await foreach (var message in Claude.QueryAsync(
            "Use the doc-writer agent to explain what AgentDefinition is in 1 sentence",
            options, cancellationToken: cts.Token))
        {
            switch (message)
            {
                case AssistantMessage { Text: var text }:
                    Console.WriteLine($"Claude: {text}");
                    break;

                case ResultMessage { TotalCostUsd: > 0 and var cost }:
                    Console.WriteLine($"\nCost: ${cost:F4}");
                    break;
            }
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("\nTIMEOUT: exceeded 60s");
    }

    Console.WriteLine();
}

async Task MultipleAgentsExample()
{
    Console.WriteLine("=== Multiple Agents Example ===");

    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

    var options = new ClaudeClientOptions
    {
        PermissionMode = PermissionMode.AcceptEdits,
        MaxTurns = 5,
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

    try
    {
        await foreach (var message in Claude.QueryAsync(
            "Use the analyzer agent to find all C# files in the examples/ directory",
            options, cancellationToken: cts.Token))
        {
            switch (message)
            {
                case AssistantMessage { Text: var text }:
                    Console.WriteLine($"Claude: {text}");
                    break;

                case ResultMessage { TotalCostUsd: > 0 and var cost }:
                    Console.WriteLine($"\nCost: ${cost:F4}");
                    break;
            }
        }
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("\nTIMEOUT: exceeded 60s");
    }

    Console.WriteLine();
}
