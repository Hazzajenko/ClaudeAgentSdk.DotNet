using System.Text;
using System.Text.Json;
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

// Build a JSON response for hook output (AOT-safe with Utf8JsonWriter)
JsonElement BuildHookResponse(Action<Utf8JsonWriter> write)
{
    using var stream = new MemoryStream();
    using (var w = new Utf8JsonWriter(stream))
    {
        w.WriteStartObject();
        write(w);
        w.WriteEndObject();
    }

    using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(stream.ToArray()));
    return doc.RootElement.Clone();
}

async Task PreToolUseExample()
{
    Console.WriteLine("=== PreToolUse Example ===");
    Console.WriteLine("Demonstrates how PreToolUse can block dangerous bash commands.\n");

    // Hook callback: block commands containing "foo.sh"
    Task<JsonElement> CheckBashCommand(JsonElement input, string? toolUseId, CancellationToken ct)
    {
        var toolName = input.TryGetProperty("tool_name", out var tn) ? tn.GetString() : "";
        if (toolName != "Bash")
            return Task.FromResult(BuildHookResponse(_ => { }));

        var toolInput = input.TryGetProperty("tool_input", out var ti) ? ti : default;
        var command = toolInput.TryGetProperty("command", out var cmd) ? cmd.GetString() ?? "" : "";

        if (command.Contains("foo.sh"))
        {
            Console.WriteLine($"  [Hook] Blocked command: {command}");
            return Task.FromResult(BuildHookResponse(w =>
            {
                w.WriteStartObject("hookSpecificOutput");
                w.WriteString("hookEventName", "PreToolUse");
                w.WriteString("permissionDecision", "deny");
                w.WriteString("permissionDecisionReason", "Command contains blocked pattern: foo.sh");
                w.WriteEndObject();
            }));
        }

        return Task.FromResult(BuildHookResponse(_ => { }));
    }

    var options = new ClaudeClientOptions
    {
        AllowedTools = ["Bash"],
        MessageCallback = msg => Console.Error.WriteLine(Claude.FormatMessage(msg)),
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

    Task<JsonElement> AddCustomInstructions(JsonElement input, string? toolUseId, CancellationToken ct)
    {
        return Task.FromResult(BuildHookResponse(w =>
        {
            w.WriteStartObject("hookSpecificOutput");
            w.WriteString("hookEventName", "UserPromptSubmit");
            w.WriteString("additionalContext", "My favorite color is hot pink");
            w.WriteEndObject();
        }));
    }

    var options = new ClaudeClientOptions
    {
        MessageCallback = msg => Console.Error.WriteLine(Claude.FormatMessage(msg)),
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

    Task<JsonElement> ReviewToolOutput(JsonElement input, string? toolUseId, CancellationToken ct)
    {
        var toolResponse = input.TryGetProperty("tool_response", out var tr) ? tr.GetString() ?? "" : "";

        if (toolResponse.Contains("No such file or directory", StringComparison.OrdinalIgnoreCase)
            || toolResponse.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(BuildHookResponse(w =>
            {
                w.WriteString("systemMessage", "The command produced an error");
                w.WriteString("reason", "Tool execution failed - consider checking the command syntax");
                w.WriteStartObject("hookSpecificOutput");
                w.WriteString("hookEventName", "PostToolUse");
                w.WriteString("additionalContext", "The command encountered an error. Try a different approach.");
                w.WriteEndObject();
            }));
        }

        return Task.FromResult(BuildHookResponse(_ => { }));
    }

    var options = new ClaudeClientOptions
    {
        AllowedTools = ["Bash"],
        MessageCallback = msg => Console.Error.WriteLine(Claude.FormatMessage(msg)),
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
