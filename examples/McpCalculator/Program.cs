using System.Text.Json;
using ClaudeAgentSdk;
using ClaudeAgentSdk.Mcp;
using ClaudeAgentSdk.Mcp.Sdk;
using ClaudeAgentSdk.Messages;

// Define calculator tools using SdkMcpTool
var twoNumberSchema = McpJsonSchema.Object(
    McpJsonSchema.Property("a", McpJsonSchema.Number("First number")),
    McpJsonSchema.Property("b", McpJsonSchema.Number("Second number")));

var tools = new SdkMcpTool[]
{
    SdkMcpTool.Create("add", "Add two numbers", twoNumberSchema,
        (input, _) =>
        {
            var a = input.GetProperty("a").GetDouble();
            var b = input.GetProperty("b").GetDouble();
            return Task.FromResult(McpToolResult.Text($"{a} + {b} = {a + b}"));
        }),

    SdkMcpTool.Create("subtract", "Subtract one number from another", twoNumberSchema,
        (input, _) =>
        {
            var a = input.GetProperty("a").GetDouble();
            var b = input.GetProperty("b").GetDouble();
            return Task.FromResult(McpToolResult.Text($"{a} - {b} = {a - b}"));
        }),

    SdkMcpTool.Create("multiply", "Multiply two numbers", twoNumberSchema,
        (input, _) =>
        {
            var a = input.GetProperty("a").GetDouble();
            var b = input.GetProperty("b").GetDouble();
            return Task.FromResult(McpToolResult.Text($"{a} * {b} = {a * b}"));
        }),

    SdkMcpTool.Create("divide", "Divide one number by another", twoNumberSchema,
        (input, _) =>
        {
            var a = input.GetProperty("a").GetDouble();
            var b = input.GetProperty("b").GetDouble();
            return b == 0
                ? Task.FromResult(McpToolResult.Error("Division by zero is not allowed"))
                : Task.FromResult(McpToolResult.Text($"{a} / {b} = {a / b}"));
        }),

    SdkMcpTool.Create("sqrt", "Calculate square root",
        McpJsonSchema.Object(McpJsonSchema.Property("n", McpJsonSchema.Number("Number to take square root of"))),
        (input, _) =>
        {
            var n = input.GetProperty("n").GetDouble();
            return n < 0
                ? Task.FromResult(McpToolResult.Error($"Cannot calculate square root of negative number {n}"))
                : Task.FromResult(McpToolResult.Text($"sqrt({n}) = {Math.Sqrt(n)}"));
        }),

    SdkMcpTool.Create("power", "Raise a number to a power",
        McpJsonSchema.Object(
            McpJsonSchema.Property("base", McpJsonSchema.Number("Base number")),
            McpJsonSchema.Property("exponent", McpJsonSchema.Number("Exponent"))),
        (input, _) =>
        {
            var b = input.GetProperty("base").GetDouble();
            var e = input.GetProperty("exponent").GetDouble();
            return Task.FromResult(McpToolResult.Text($"{b}^{e} = {Math.Pow(b, e)}"));
        }),
};

// Create the calculator server
var calculator = McpSdkServerConfig.Create("calculator", tools, "2.0.0");

// Old: manually construct mcp__{serverKey}__{toolName} magic strings
// AllowedTools =
// [
//     "mcp__calc__add",
//     "mcp__calc__subtract",
//     "mcp__calc__multiply",
//     "mcp__calc__divide",
//     "mcp__calc__sqrt",
//     "mcp__calc__power",
// ],

// New: derive from the server config — no magic strings
var options = new ClaudeClientOptions
{
    McpServers = new Dictionary<string, McpServerConfig>
    {
        ["calc"] = calculator,
    },
    AllowedTools = calculator.GetQualifiedToolNames("calc"),
};

void DisplayMessage(Message msg)
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
                    case ToolUseBlock { Name: var name, Input: var input }:
                        Console.WriteLine($"  Using tool: {name}");
                        Console.WriteLine($"  Input: {input}");
                        break;
                }
            }
            break;

        // Old: ugly runtime downcast on object Content
        // case UserMessage { Content: var content } when content is IReadOnlyList<ContentBlock> blocks:
        //     foreach (var block in blocks) { ... }

        // New: typed accessor
        case UserMessage { ContentBlocks: { } blocks }:
            foreach (var block in blocks)
            {
                if (block is ToolResultBlock { Content: { } resultContent })
                    Console.WriteLine($"  Tool Result: {resultContent}");
            }
            break;

        case ResultMessage result:
            Console.WriteLine("Result ended");
            if (result.TotalCostUsd is > 0 and var cost)
                Console.WriteLine($"Cost: ${cost:F6}");
            break;
    }
}

// Run calculator prompts
string[] prompts =
[
    "List your tools",
    "Calculate 15 + 27",
    "What is 100 divided by 7?",
    "Calculate the square root of 144",
    "What is 2 raised to the power of 8?",
    "Calculate (12 + 8) * 3 - 10",
];

foreach (var prompt in prompts)
{
    Console.WriteLine($"\n{new string('=', 50)}");
    Console.WriteLine($"Prompt: {prompt}");
    Console.WriteLine(new string('=', 50));

    await using var client = new ClaudeClient(options);
    await foreach (var message in client.SendAsync(prompt))
        DisplayMessage(message);
}
