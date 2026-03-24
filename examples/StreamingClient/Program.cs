using ClaudeAgentSdk;
using ClaudeAgentSdk.Messages;

await BasicStreamingExample();
await MultiTurnExample();
await WithInterruptExample();
await ManualMessageHandlingExample();
await WithOptionsExample();
await ErrorHandlingExample();

// Old: 8-line helper to extract text from AssistantMessage
// void DisplayMessage(Message msg)
// {
//     switch (msg)
//     {
//         case AssistantMessage { Content: var content }:
//             foreach (var block in content)
//                 if (block is TextBlock { Text: var text })
//                     Console.WriteLine($"Claude: {text}");
//             break;
//         case ResultMessage:
//             Console.WriteLine("Result ended");
//             break;
//     }
// }

// New: 1-line text access
void DisplayMessage(Message msg)
{
    switch (msg)
    {
        case AssistantMessage { Text: var text }:
            Console.WriteLine($"Claude: {text}");
            break;
        case ResultMessage:
            Console.WriteLine("Result ended");
            break;
    }
}

async Task BasicStreamingExample()
{
    Console.WriteLine("=== Basic Streaming Example ===");

    await using var client = new ClaudeClient();

    Console.WriteLine("User: What is 2+2?");
    await foreach (var msg in client.SendAsync("What is 2+2?"))
    {
        DisplayMessage(msg);
    }

    Console.WriteLine("\n");
}

async Task MultiTurnExample()
{
    Console.WriteLine("=== Multi-Turn Conversation Example ===");

    await using var client = new ClaudeClient();

    // First turn
    Console.WriteLine("User: What's the capital of France?");
    await foreach (var msg in client.SendAsync("What's the capital of France?"))
    {
        DisplayMessage(msg);
    }

    // Second turn — same session, Claude remembers context
    Console.WriteLine("\nUser: What's the population of that city?");
    await foreach (var msg in client.SendAsync("What's the population of that city?"))
    {
        DisplayMessage(msg);
    }

    Console.WriteLine("\n");
}

async Task WithInterruptExample()
{
    Console.WriteLine("=== Interrupt Example ===");

    await using var client = new ClaudeClient();

    Console.WriteLine("User: Count from 1 to 100 slowly");
    var sendEnumerable = client.SendAsync("Count from 1 to 100 slowly, with a brief pause between each number");

    // Consume messages in background so interrupt can be processed
    var messages = new List<Message>();
    var consumeTask = Task.Run(async () =>
    {
        await foreach (var msg in sendEnumerable)
        {
            messages.Add(msg);
            DisplayMessage(msg);
        }
    });

    // Wait then interrupt
    await Task.Delay(2000);
    Console.WriteLine("\n[After 2 seconds, sending interrupt...]");
    await client.InterruptAsync();

    await consumeTask;

    // Follow-up after interrupt
    Console.WriteLine("\nUser: Never mind, just tell me a quick joke");
    await foreach (var msg in client.SendAsync("Never mind, just tell me a quick joke"))
    {
        DisplayMessage(msg);
    }

    Console.WriteLine("\n");
}

async Task ManualMessageHandlingExample()
{
    Console.WriteLine("=== Manual Message Handling Example ===");

    await using var client = new ClaudeClient();

    Console.WriteLine("User: List 5 programming languages and their main use cases");
    var languagesFound = new List<string>();

    await foreach (var message in client.SendAsync("List 5 programming languages and their main use cases"))
    {
        switch (message)
        {
            case AssistantMessage { Text: var text } when text is not null:
                Console.WriteLine($"Claude: {text}");

                // Custom logic: extract language names
                foreach (var lang in (string[])["Python", "JavaScript", "Java", "C++", "Go", "Rust", "Ruby", "C#"])
                {
                    if (text.Contains(lang, StringComparison.OrdinalIgnoreCase) && !languagesFound.Contains(lang))
                    {
                        languagesFound.Add(lang);
                        Console.WriteLine($"  Found language: {lang}");
                    }
                }
                break;

            case ResultMessage:
                Console.WriteLine($"Total languages mentioned: {languagesFound.Count}");
                break;
        }
    }

    Console.WriteLine("\n");
}

async Task WithOptionsExample()
{
    Console.WriteLine("=== Custom Options Example ===");

    var options = new ClaudeClientOptions
    {
        AllowedTools = ["Read", "Bash"],
        SystemPrompt = "You are a helpful coding assistant.",
        Model = "sonnet",
    };

    await using var client = new ClaudeClient(options);

    Console.WriteLine("User: What shell am I using?");
    await foreach (var msg in client.SendAsync("Run 'echo $SHELL' and tell me what shell I'm using"))
    {
        switch (msg)
        {
            case AssistantMessage { Content: var content }:
                foreach (var block in content)
                {
                    switch (block)
                    {
                        case TextBlock { Text: var text }:
                            Console.WriteLine($"Claude: {text}");
                            break;
                        case ToolUseBlock { Name: var name, Id: var id }:
                            Console.WriteLine($"Tool Use: {name} (id: {id})");
                            break;
                    }
                }
                break;

            case ResultMessage result:
                Console.WriteLine("Result ended");
                if (result.TotalCostUsd is > 0 and var cost)
                    Console.WriteLine($"Cost: ${cost:F4}");
                break;
        }
    }

    Console.WriteLine("\n");
}

async Task ErrorHandlingExample()
{
    Console.WriteLine("=== Error Handling Example ===");

    await using var client = new ClaudeClient();

    Console.WriteLine("User: What is 2 + 2?");

    // Demonstrate timeout with CancellationTokenSource
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

    try
    {
        var messageCount = 0;
        await foreach (var msg in client.SendAsync("What is 2 + 2?", cts.Token))
        {
            messageCount++;
            DisplayMessage(msg);
        }

        Console.WriteLine($"Received {messageCount} messages");
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("Response timed out after 30 seconds");
    }

    Console.WriteLine("\n");
}
