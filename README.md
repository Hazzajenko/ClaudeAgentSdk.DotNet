# ClaudeAgentSdk

C# wrapper for the Claude Code CLI agent SDK. Provides a typed, AOT-compatible interface to Claude Code's JSON streaming protocol.

## Install

```bash
dotnet add package ClaudeAgentSdk.DotNet
```

## Usage

### One-shot query

```csharp
using ClaudeAgentSdk;

var messages = await Claude.QueryAsync("Explain what this code does", new ClaudeClientOptions
{
    Model = "sonnet",
    MaxTurns = 1,
});

foreach (var msg in messages)
{
    Console.WriteLine(msg);
}
```

### Stateful session

```csharp
await using var client = new ClaudeClient(new ClaudeClientOptions
{
    Model = "sonnet",
    SystemPrompt = "You are a helpful assistant.",
});

await foreach (var message in client.SendAsync("Hello!"))
{
    Console.WriteLine(message);
}
```

## License

[MIT](LICENSE)
