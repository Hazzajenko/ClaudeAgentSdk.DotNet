using ClaudeAgentSdk;
using ClaudeAgentSdk.Messages;

await BasicExample();
await WithOptionsExample();
await WithToolsExample();

async Task BasicExample()
{
    Console.WriteLine("=== Basic Example ===");

    await foreach (var message in Claude.QueryAsync("What is 2 + 2?"))
    {
        // Old: nested pattern match + loop to extract text
        // if (message is AssistantMessage { Content: var content })
        //     foreach (var block in content)
        //         if (block is TextBlock { Text: var text })
        //             Console.WriteLine($"Claude: {text}");

        // New: single property access
        if (message is AssistantMessage { Text: var text })
            Console.WriteLine($"Claude: {text}");
    }

    Console.WriteLine();
}

async Task WithOptionsExample()
{
    Console.WriteLine("=== With Options Example ===");

    var options = new ClaudeClientOptions
    {
        SystemPrompt = "You are a helpful assistant that explains things simply.",
        MaxTurns = 1,
    };

    await foreach (var message in Claude.QueryAsync("Explain what C# is in one sentence.", options))
    {
        if (message is AssistantMessage { Text: var text })
            Console.WriteLine($"Claude: {text}");
    }

    Console.WriteLine();
}

async Task WithToolsExample()
{
    Console.WriteLine("=== With Tools Example ===");

    var options = new ClaudeClientOptions
    {
        AllowedTools = ["Read", "Write"],
        SystemPrompt = "You are a helpful file assistant.",
    };

    await foreach (var message in Claude.QueryAsync(
        "Create a file called hello.txt with 'Hello, World!' in it", options))
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

    Console.WriteLine();
}
