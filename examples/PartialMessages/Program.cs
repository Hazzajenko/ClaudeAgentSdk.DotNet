using ClaudeAgentSdk;
using ClaudeAgentSdk.Messages;

Console.WriteLine("=== Partial Message Streaming Example ===");
Console.WriteLine("""
Streams incremental updates as Claude generates responses.
Useful for real-time UIs or monitoring tool use progress.
""");

var options = new ClaudeClientOptions
{
    IncludePartialMessages = true,
    Model = "sonnet",
    MaxTurns = 2,
};

await using var client = new ClaudeClient(options);

var prompt = "Think of three jokes, then tell one";
Console.WriteLine($"Prompt: {prompt}\n");
Console.WriteLine(new string('=', 50));

await foreach (var message in client.SendAsync(prompt))
{
    switch (message)
    {
        case StreamEvent { Event: var evt }:
            // StreamEvent contains incremental content deltas
            Console.WriteLine($"[StreamEvent] {evt}");
            break;

        case AssistantMessage { Text: var text }:
            Console.WriteLine($"Claude: {text}");
            break;

        case ResultMessage result:
            Console.WriteLine("Result ended");
            if (result.TotalCostUsd is > 0 and var cost)
                Console.WriteLine($"Cost: ${cost:F6}");
            break;
    }
}
