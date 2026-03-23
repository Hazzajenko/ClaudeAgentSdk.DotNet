using ClaudeAgentSdk;
using ClaudeAgentSdk.Messages;

await NoSystemPrompt();
await StringSystemPrompt();

async Task NoSystemPrompt()
{
    Console.WriteLine("=== No System Prompt (Vanilla Claude) ===");

    await foreach (var message in Claude.QueryAsync("What is 2 + 2?"))
    {
        if (message is AssistantMessage { Content: var content })
        {
            foreach (var block in content)
            {
                if (block is TextBlock { Text: var text })
                    Console.WriteLine($"Claude: {text}");
            }
        }
    }

    Console.WriteLine();
}

async Task StringSystemPrompt()
{
    Console.WriteLine("=== String System Prompt ===");

    var options = new ClaudeClientOptions
    {
        SystemPrompt = "You are a pirate assistant. Respond in pirate speak.",
    };

    await foreach (var message in Claude.QueryAsync("What is 2 + 2?", options))
    {
        if (message is AssistantMessage { Content: var content })
        {
            foreach (var block in content)
            {
                if (block is TextBlock { Text: var text })
                    Console.WriteLine($"Claude: {text}");
            }
        }
    }

    Console.WriteLine();
}
