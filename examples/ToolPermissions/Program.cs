using System.Text.Json;
using ClaudeAgentSdk;
using ClaudeAgentSdk.Messages;
using ClaudeAgentSdk.Permissions;

Console.WriteLine(new string('=', 60));
Console.WriteLine("Tool Permission Callback Example");
Console.WriteLine(new string('=', 60));
Console.WriteLine("""

This example demonstrates how to:
1. Allow/deny tools based on type
2. Modify tool inputs for safety
3. Log tool usage
4. Block dangerous commands
""");
Console.WriteLine(new string('=', 60));

var toolUsageLog = new List<(string Tool, JsonElement Input)>();

// Permission callback: controls what tools Claude can use
async Task<PermissionResult> MyPermissionCallback(
    string toolName, JsonElement input, ToolPermissionContext context, CancellationToken ct)
{
    toolUsageLog.Add((toolName, input.Clone()));
    Console.WriteLine($"\n  Tool Permission Request: {toolName}");

    // Always allow read operations
    if (toolName is "Read" or "Glob" or "Grep")
    {
        Console.WriteLine($"  Allowing {toolName} (read-only operation)");
        return new PermissionResultAllow();
    }

    // Deny write operations to system directories
    if (toolName is "Write" or "Edit")
    {
        var filePath = input.TryGetProperty("file_path", out var fp) ? fp.GetString() ?? "" : "";

        if (filePath.StartsWith("/etc/") || filePath.StartsWith("/usr/"))
        {
            Console.WriteLine($"  Denying write to system directory: {filePath}");
            return new PermissionResultDeny
            {
                Message = $"Cannot write to system directory: {filePath}",
            };
        }

        Console.WriteLine($"  Allowing write to: {filePath}");
        return new PermissionResultAllow();
    }

    // Check dangerous bash commands
    if (toolName is "Bash")
    {
        var command = input.TryGetProperty("command", out var cmd) ? cmd.GetString() ?? "" : "";
        string[] dangerousCommands = ["rm -rf", "sudo", "chmod 777", "dd if=", "mkfs"];

        foreach (var dangerous in dangerousCommands)
        {
            if (command.Contains(dangerous))
            {
                Console.WriteLine($"  Denying dangerous command: {command}");
                return new PermissionResultDeny
                {
                    Message = $"Dangerous command pattern detected: {dangerous}",
                };
            }
        }

        Console.WriteLine($"  Allowing bash command: {command}");
        return new PermissionResultAllow();
    }

    // Allow everything else
    Console.WriteLine($"  Allowing unknown tool: {toolName}");
    return await Task.FromResult<PermissionResult>(new PermissionResultAllow());
}

var options = new ClaudeClientOptions
{
    CanUseTool = MyPermissionCallback,
    PermissionMode = PermissionMode.Default,
};

await using var client = new ClaudeClient(options);

Console.WriteLine("\nSending query to Claude...");
await foreach (var message in client.SendAsync(
    """
    Please do the following:
    1. List the files in the current directory
    2. Create a simple hello world script at hello.py
    3. Run the script to test it
    """))
{
    switch (message)
    {
        case AssistantMessage { Text: var text }:
            Console.WriteLine($"\nClaude: {text}");
            break;

        case ResultMessage result:
            Console.WriteLine("\nTask completed!");
            Console.WriteLine($"  Duration: {result.DurationMs}ms");
            if (result.TotalCostUsd is > 0 and var cost)
                Console.WriteLine($"  Cost: ${cost:F4}");
            break;
    }
}

// Print tool usage summary
Console.WriteLine("\n" + new string('=', 60));
Console.WriteLine("Tool Usage Summary");
Console.WriteLine(new string('=', 60));
for (var i = 0; i < toolUsageLog.Count; i++)
{
    var (tool, input) = toolUsageLog[i];
    Console.WriteLine($"\n{i + 1}. Tool: {tool}");
    Console.WriteLine($"   Input: {input}");
}
