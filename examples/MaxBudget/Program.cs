using ClaudeAgentSdk;
using ClaudeAgentSdk.Messages;

Console.WriteLine("This example demonstrates using MaxBudgetUsd to control API costs.\n");

await WithoutBudget();
await WithReasonableBudget();
await WithTightBudget();

Console.WriteLine("""
Note: Budget checking happens after each API call completes,
so the final cost may slightly exceed the specified budget.
""");

async Task WithoutBudget()
{
    Console.WriteLine("=== Without Budget Limit ===");

    await foreach (var message in Claude.QueryAsync("What is 2 + 2?"))
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

            case ResultMessage result:
                if (result.TotalCostUsd is > 0 and var cost)
                    Console.WriteLine($"Total cost: ${cost:F4}");
                Console.WriteLine($"Status: {result.Subtype}");
                break;
        }
    }

    Console.WriteLine();
}

async Task WithReasonableBudget()
{
    Console.WriteLine("=== With Reasonable Budget ($0.10) ===");

    var options = new ClaudeClientOptions
    {
        MaxBudgetUsd = 0.10, // 10 cents — plenty for a simple query
    };

    await foreach (var message in Claude.QueryAsync("What is 2 + 2?", options))
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

            case ResultMessage result:
                if (result.TotalCostUsd is > 0 and var cost)
                    Console.WriteLine($"Total cost: ${cost:F4}");
                Console.WriteLine($"Status: {result.Subtype}");
                break;
        }
    }

    Console.WriteLine();
}

async Task WithTightBudget()
{
    Console.WriteLine("=== With Tight Budget ($0.0001) ===");

    var options = new ClaudeClientOptions
    {
        MaxBudgetUsd = 0.0001, // Very small — will be exceeded quickly
    };

    await foreach (var message in Claude.QueryAsync("Read the README.md file and summarize it", options))
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

            case ResultMessage { Subtype: "error_max_budget_usd" } result:
                if (result.TotalCostUsd is > 0 and var cost)
                    Console.WriteLine($"Total cost: ${cost:F4}");
                Console.WriteLine("Budget limit exceeded!");
                break;

            case ResultMessage result:
                if (result.TotalCostUsd is > 0 and var cost2)
                    Console.WriteLine($"Total cost: ${cost2:F4}");
                Console.WriteLine($"Status: {result.Subtype}");
                break;
        }
    }

    Console.WriteLine();
}
