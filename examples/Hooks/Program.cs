using ClaudeAgentSdk;
using ClaudeAgentSdk.Hooks;
using ClaudeAgentSdk.Messages;

Console.WriteLine("Starting Claude SDK Hooks Examples...");
Console.WriteLine(new string('=', 50) + "\n");

await PreToolUseExample();
await UserPromptSubmitExample();
await PostToolUseExample();

void DisplayMessage(Message msg)
{
    switch (msg)
    {
        case AssistantMessage { Content: var content }:
            foreach (var block in content)
            {
                if (block is TextBlock { Text: var text })
                    Console.WriteLine($"Claude: {text}");
            }
            break;
        case ResultMessage:
            Console.WriteLine("Result ended");
            break;
    }
}

async Task PreToolUseExample()
{
    Console.WriteLine("=== PreToolUse Example ===");
    Console.WriteLine("Demonstrates how PreToolUse can block dangerous bash commands.\n");

    Task<HookOutput> CheckBashCommand(HookInput input, CancellationToken ct)
    {
        if (input is not PreToolUseInput { ToolName: "Bash" } pre)
            return Task.FromResult(HookOutput.Empty);

        string command = pre.ToolInput.TryGetProperty("command", out var cmd) ? cmd.GetString() ?? "" : "";

        if (command.Contains("foo.sh"))
        {
            Console.WriteLine($"  [Hook] Blocked command: {command}");
            return Task.FromResult(new HookOutput
            {
                HookSpecificOutput = new PreToolUseOutput
                {
                    PermissionDecision = "deny",
                    PermissionDecisionReason = "Command contains blocked pattern: foo.sh",
                },
            });
        }

        return Task.FromResult(HookOutput.Empty);
    }

    var options = new ClaudeClientOptions
    {
        AllowedTools = ["Bash"],
        Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.PreToolUse] =
            [
                new HookMatcher
                {
                    Matcher = "Bash",
                    Hooks = [CheckBashCommand],
                },
            ],
        },
    };

    await using var client = new ClaudeClient(options);

    // Test 1: blocked command
    Console.WriteLine("Test 1: Command that hook should block...");
    Console.WriteLine("User: Run the bash command: ./foo.sh --help");
    await foreach (var msg in client.SendAsync("Run the bash command: ./foo.sh --help"))
        DisplayMessage(msg);

    Console.WriteLine("\n" + new string('=', 50) + "\n");

    // Test 2: allowed command
    Console.WriteLine("Test 2: Command that hook should allow...");
    Console.WriteLine("User: Run the bash command: echo 'Hello from hooks!'");
    await foreach (var msg in client.SendAsync("Run the bash command: echo 'Hello from hooks example!'"))
        DisplayMessage(msg);

    Console.WriteLine("\n");
}

async Task UserPromptSubmitExample()
{
    Console.WriteLine("=== UserPromptSubmit Example ===");
    Console.WriteLine("Adds custom context when a session starts.\n");

    Task<HookOutput> AddCustomInstructions(HookInput input, CancellationToken ct)
    {
        return Task.FromResult(new HookOutput
        {
            HookSpecificOutput = new UserPromptSubmitOutput
            {
                AdditionalContext = "My favorite color is hot pink",
            },
        });
    }

    var options = new ClaudeClientOptions
    {
        Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.UserPromptSubmit] =
            [
                new HookMatcher
                {
                    Matcher = null,
                    Hooks = [AddCustomInstructions],
                },
            ],
        },
    };

    await using var client = new ClaudeClient(options);

    Console.WriteLine("User: What's my favorite color?");
    await foreach (var msg in client.SendAsync("What's my favorite color?"))
        DisplayMessage(msg);

    Console.WriteLine("\n");
}

async Task PostToolUseExample()
{
    Console.WriteLine("=== PostToolUse Example ===");
    Console.WriteLine("Reviews tool output and adds context on errors.\n");

    Task<HookOutput> ReviewToolOutput(HookInput input, CancellationToken ct)
    {
        if (input is not PostToolUseInput post)
            return Task.FromResult(HookOutput.Empty);

        string toolResponse = post.ToolResponse.ValueKind == System.Text.Json.JsonValueKind.String
            ? post.ToolResponse.GetString() ?? ""
            : post.ToolResponse.ToString();

        if (toolResponse.Contains("No such file or directory", StringComparison.OrdinalIgnoreCase)
            || toolResponse.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new HookOutput
            {
                SystemMessage = "The command produced an error",
                Reason = "Tool execution failed - consider checking the command syntax",
                HookSpecificOutput = new PostToolUseOutput
                {
                    AdditionalContext = "The command encountered an error. Try a different approach.",
                },
            });
        }

        return Task.FromResult(HookOutput.Empty);
    }

    var options = new ClaudeClientOptions
    {
        AllowedTools = ["Bash"],
        Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
        {
            [HookEvent.PostToolUse] =
            [
                new HookMatcher
                {
                    Matcher = "Bash",
                    Hooks = [ReviewToolOutput],
                },
            ],
        },
    };

    await using var client = new ClaudeClient(options);

    Console.WriteLine("User: Run a command that will produce an error: ls /nonexistent_directory");
    await foreach (var msg in client.SendAsync("Run this command: ls /nonexistent_directory"))
        DisplayMessage(msg);

    Console.WriteLine("\n");
}
