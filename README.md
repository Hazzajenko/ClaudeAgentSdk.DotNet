# Claude Agent SDK for .NET

[![NuGet](https://img.shields.io/nuget/v/ClaudeAgentSdk.DotNet?style=flat-square)](https://www.nuget.org/packages/ClaudeAgentSdk.DotNet)
[![Build](https://img.shields.io/github/actions/workflow/status/Hazzajenko/ClaudeAgentSdk.DotNet/ci.yml?branch=main&style=flat-square)](https://github.com/Hazzajenko/ClaudeAgentSdk.DotNet/actions)
[![License](https://img.shields.io/github/license/Hazzajenko/ClaudeAgentSdk.DotNet?style=flat-square)](LICENSE)

Typed, AOT-compatible C# SDK for the [Claude Code CLI](https://docs.anthropic.com/en/docs/claude-code) agent protocol. Port of the official [Python Claude Agent SDK](https://github.com/anthropics/claude-agent-sdk-python).

## Installation

```bash
dotnet add package ClaudeAgentSdk.DotNet
```

> [!NOTE]
> Requires .NET 10+ and the [Claude Code CLI](https://docs.anthropic.com/en/docs/claude-code/getting-started) installed and available on your PATH.

## Quick Start

```csharp
using ClaudeAgentSdk;
using ClaudeAgentSdk.Messages;

await foreach (var message in Claude.QueryAsync("What is 2 + 2?"))
{
    if (message is AssistantMessage { Text: var text })
        Console.WriteLine($"Claude: {text}");
}
```

## One-Shot API

`Claude.QueryAsync` streams messages from a single prompt. `Claude.QueryTextAsync` returns just the final text.

```csharp
// Stream all messages
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

// Get text only — no streaming
string reply = await Claude.QueryTextAsync("What is 2 + 2?");
```

### Structured Output

`Claude.QueryAsync<T>` auto-derives a JSON schema from your source-generated `JsonTypeInfo<T>` and deserializes the result — one line, fully AOT-safe.

```csharp
using System.Text.Json.Serialization;
using ClaudeAgentSdk;

var person = await Claude.QueryAsync(
    "Extract person info: John Smith is 35 years old, works as a software engineer.",
    ExampleJsonContext.Default.PersonInfo);

Console.WriteLine($"{person?.Name}, age {person?.Age}");

public sealed record PersonInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("age")]
    public int Age { get; init; }
}

[JsonSerializable(typeof(PersonInfo))]
internal sealed partial class ExampleJsonContext : JsonSerializerContext;
```

## Stateful Client

`ClaudeClient` supports multi-turn conversations with session persistence, interrupts, and runtime control.

```csharp
await using var client = new ClaudeClient(new ClaudeClientOptions
{
    SystemPrompt = "You are a helpful coding assistant.",
    Model = "sonnet",
});

// Turn 1
await foreach (var msg in client.SendAsync("What's the capital of France?"))
    if (msg is AssistantMessage { Text: var text }) Console.WriteLine($"Claude: {text}");

// Turn 2 — Claude remembers context
await foreach (var msg in client.SendAsync("What's the population of that city?"))
    if (msg is AssistantMessage { Text: var text }) Console.WriteLine($"Claude: {text}");
```

### Message Handling

```csharp
await foreach (var message in client.SendAsync("Create a hello.py file"))
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
                    case ToolUseBlock { Name: var name, Input: var input }:
                        Console.WriteLine($"Tool: {name}");
                        break;
                }
            }
            break;

        case ResultMessage { TotalCostUsd: > 0 and var cost }:
            Console.WriteLine($"Cost: ${cost:F4}");
            break;
    }
}
```

## Tools

By default, Claude has access to the full [Claude Code toolset](https://code.claude.com/docs/en/settings#tools-available-to-claude). `AllowedTools` is a permission allowlist that auto-approves listed tools. `DisallowedTools` blocks specific tools entirely.

```csharp
var options = new ClaudeClientOptions
{
    AllowedTools = ["Read", "Write", "Bash"],
    PermissionMode = PermissionMode.AcceptEdits,
};
```

## Custom Tools (In-Process MCP Servers)

Define tools that run inside your application with zero IPC overhead. No subprocess management, no external server processes.

```csharp
using System.Text.Json.Serialization;
using ClaudeAgentSdk.Mcp;
using ClaudeAgentSdk.Mcp.Sdk;

// Define input type — schema auto-derived from JsonTypeInfo
public sealed record TwoNumbers
{
    [JsonPropertyName("a")] public double A { get; init; }
    [JsonPropertyName("b")] public double B { get; init; }
}

[JsonSerializable(typeof(TwoNumbers))]
internal sealed partial class CalcContext : JsonSerializerContext;

var tools = new SdkMcpTool[]
{
    SdkMcpTool.Create<TwoNumbers>("add", "Add two numbers",
        CalcContext.Default.TwoNumbers,
        (input, _) => McpToolResult.Text($"{input.A + input.B}")),
};

var calculator = McpSdkServerConfig.Create("calculator", tools);

var options = new ClaudeClientOptions
{
    McpServers = new Dictionary<string, McpServerConfig>
    {
        ["calc"] = calculator,
    },
    AllowedTools = calculator.GetQualifiedToolNames("calc"), // mcp__calc__add, etc.
};
```

You can mix SDK and external MCP servers:

```csharp
var options = new ClaudeClientOptions
{
    McpServers = new Dictionary<string, McpServerConfig>
    {
        ["internal"] = sdkServer,                           // in-process
        ["external"] = new McpStdioServerConfig("my-tool"), // subprocess
    },
};
```

## Hooks

Hooks are functions invoked at specific points of the agent loop for deterministic processing and automated feedback. Supports both sync and async callbacks.

```csharp
using ClaudeAgentSdk.Hooks;

HookOutput CheckBashCommand(HookInput input)
{
    if (input is not PreToolUseInput { ToolName: "Bash" } pre)
        return HookOutput.Empty;

    string command = pre.ToolInput.TryGetProperty("command", out var cmd)
        ? cmd.GetString() ?? "" : "";

    if (command.Contains("rm -rf"))
    {
        return new HookOutput
        {
            HookSpecificOutput = new PreToolUseOutput
            {
                PermissionDecision = "deny",
                PermissionDecisionReason = "Dangerous command blocked",
            },
        };
    }

    return HookOutput.Empty;
}

var options = new ClaudeClientOptions
{
    AllowedTools = ["Bash"],
    Hooks = new Dictionary<HookEvent, IReadOnlyList<HookMatcher>>
    {
        [HookEvent.PreToolUse] = [HookMatcher.Create("Bash", CheckBashCommand)],
    },
};
```

Hook events: `PreToolUse`, `PostToolUse`, `PostToolUseFailure`, `UserPromptSubmit`, `Stop`, `SubagentStop`, `PreCompact`, `Notification`, `SubagentStart`, `PermissionRequest`.

## Permission Callbacks

Fine-grained tool permission control via `CanUseTool`:

```csharp
using ClaudeAgentSdk.Permissions;

var options = new ClaudeClientOptions
{
    CanUseTool = async (toolName, input, context, ct) =>
    {
        if (toolName is "Read" or "Grep")
            return new PermissionResultAllow();

        if (toolName is "Bash")
        {
            var command = input.TryGetProperty("command", out var cmd)
                ? cmd.GetString() ?? "" : "";

            if (command.Contains("rm -rf"))
                return new PermissionResultDeny { Message = "Dangerous command blocked" };
        }

        return new PermissionResultAllow();
    },
};
```

## Agents

Define specialized sub-agents with their own tools, models, and prompts:

```csharp
using ClaudeAgentSdk.Agents;

var options = new ClaudeClientOptions
{
    Agents = new Dictionary<string, AgentDefinition>
    {
        ["code-reviewer"] = new()
        {
            Description = "Reviews code for best practices",
            Prompt = "You are a code reviewer. Be concise.",
            Tools = ["Read", "Grep"],
            Model = AgentModel.Sonnet,
        },
    },
};

await foreach (var msg in Claude.QueryAsync(
    "Use the code-reviewer agent to review src/Program.cs", options))
{
    if (msg is AssistantMessage { Text: var text })
        Console.WriteLine(text);
}
```

## Types

**Messages** — `AssistantMessage`, `UserMessage`, `SystemMessage`, `ResultMessage`, `StreamEvent`, `RateLimitEvent`

**Content Blocks** — `TextBlock`, `ThinkingBlock`, `ToolUseBlock`, `ToolResultBlock`

**Task Messages** — `TaskStartedMessage`, `TaskProgressMessage`, `TaskNotificationMessage`

See [Messages](src/ClaudeAgentSdk/Messages/) and [Content Blocks](src/ClaudeAgentSdk/Messages/ContentBlocks/) for full definitions.

## Error Handling

```csharp
using ClaudeAgentSdk.Exceptions;

try
{
    await foreach (var msg in Claude.QueryAsync("Hello")) { }
}
catch (CliNotFoundException)
{
    Console.WriteLine("Claude Code CLI not found — install it first");
}
catch (CliConnectionException)
{
    Console.WriteLine("Failed to connect to Claude CLI process");
}
catch (MessageParseException e)
{
    Console.WriteLine($"Failed to parse response: {e.Message}");
}
```

## Examples

See the [examples/](examples/) directory for 12 complete projects:

| Example | Description |
|---|---|
| [QuickStart](examples/QuickStart) | Basic queries, options, and tool usage |
| [StructuredOutput](examples/StructuredOutput) | Typed JSON responses with AOT source generation |
| [StreamingClient](examples/StreamingClient) | Multi-turn conversations, interrupts, message handling |
| [McpCalculator](examples/McpCalculator) | In-process MCP server with calculator tools |
| [Hooks](examples/Hooks) | PreToolUse, PostToolUse, and UserPromptSubmit hooks |
| [ToolPermissions](examples/ToolPermissions) | Fine-grained permission callbacks |
| [Agents](examples/Agents) | Custom agent definitions |
| [ToolsConfiguration](examples/ToolsConfiguration) | Allowed and disallowed tools |
| [SystemPrompt](examples/SystemPrompt) | Custom system prompts |
| [MaxBudget](examples/MaxBudget) | Budget limits and cost control |
| [PartialMessages](examples/PartialMessages) | Real-time streaming with partial updates |
| [StderrCallback](examples/StderrCallback) | CLI debug output capture |
