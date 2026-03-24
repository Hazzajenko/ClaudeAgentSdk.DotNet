using ClaudeAgentSdk;
using ClaudeAgentSdk.Messages;

await AllowedToolsExample();
await DisallowedToolsExample();

async Task AllowedToolsExample()
{
    Console.WriteLine("=== Allowed Tools Example ===");
    Console.WriteLine("Only Read and Grep are allowed — Claude cannot write or run commands.\n");

    var options = new ClaudeClientOptions
    {
        AllowedTools = ["Read", "Grep"],
        MaxTurns = 2,
    };

    await foreach (var message in Claude.QueryAsync(
        "Search for any TODO comments in the current directory", options))
    {
        switch (message)
        {
            case AssistantMessage { Content: var content }:
                foreach (var block in content)
                {
                    switch (block)
                    {
                        case TextBlock { Text: var text }:
                            Console.WriteLine($"Claude: {text}");
                            break;
                        case ToolUseBlock { Name: var name }:
                            Console.WriteLine($"  Using tool: {name}");
                            break;
                    }
                }
                break;

            case ResultMessage result:
                if (result.TotalCostUsd is > 0 and var cost)
                    Console.WriteLine($"\nCost: ${cost:F4}");
                break;
        }
    }

    Console.WriteLine();
}

async Task DisallowedToolsExample()
{
    Console.WriteLine("=== Disallowed Tools Example ===");
    Console.WriteLine("Bash is disallowed — Claude can use other tools but not run commands.\n");

    var options = new ClaudeClientOptions
    {
        DisallowedTools = ["Bash"],
        MaxTurns = 1,
    };

    await foreach (var message in Claude.QueryAsync("What is 2 + 2?", options))
    {
        if (message is AssistantMessage { Text: var text })
            Console.WriteLine($"Claude: {text}");
    }

    Console.WriteLine();
}
