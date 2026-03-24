using ClaudeAgentSdk;
using ClaudeAgentSdk.Messages;

Console.WriteLine("=== Stderr Callback Example ===");
Console.WriteLine("Captures CLI debug output via StderrCallback.\n");

var stderrLines = new List<string>();

var options = new ClaudeClientOptions
{
    StderrCallback = line =>
    {
        stderrLines.Add(line);
        Console.WriteLine($"[stderr] {line}");
    },
    // Enable debug output to stderr so there's actually something to capture
    ExtraArgs = new Dictionary<string, string?> { ["debug-to-stderr"] = null },
};

await foreach (var message in Claude.QueryAsync("What is 2 + 2?", options))
{
    if (message is AssistantMessage { Text: var text })
        Console.WriteLine($"Claude: {text}");
}

Console.WriteLine($"\nTotal stderr lines captured: {stderrLines.Count}");
