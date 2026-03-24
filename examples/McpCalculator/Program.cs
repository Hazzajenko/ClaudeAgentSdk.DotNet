using System.Text.Json.Serialization;
using ClaudeAgentSdk;
using ClaudeAgentSdk.Mcp;
using ClaudeAgentSdk.Mcp.Sdk;
using ClaudeAgentSdk.Messages;

// Old: manual schema + raw JsonElement input + Task.FromResult everywhere
//
// var twoNumberSchema = McpJsonSchema.Object(
//     McpJsonSchema.Property("a", McpJsonSchema.Number("First number")),
//     McpJsonSchema.Property("b", McpJsonSchema.Number("Second number")));
//
// SdkMcpTool.Create("add", "Add two numbers", twoNumberSchema,
//     (input, _) =>
//     {
//         var a = input.GetProperty("a").GetDouble();
//         var b = input.GetProperty("b").GetDouble();
//         return Task.FromResult(McpToolResult.Text($"{a + b}"));
//     })

// New: typed input records — schema auto-derived, no JsonElement, no Task.FromResult
var tools = new SdkMcpTool[]
{
    SdkMcpTool.Create<TwoNumbers>("add", "Add two numbers",
        CalcContext.Default.TwoNumbers,
        (input, _) => McpToolResult.Text($"{input.A} + {input.B} = {input.A + input.B}")),

    SdkMcpTool.Create<TwoNumbers>("subtract", "Subtract one number from another",
        CalcContext.Default.TwoNumbers,
        (input, _) => McpToolResult.Text($"{input.A} - {input.B} = {input.A - input.B}")),

    SdkMcpTool.Create<TwoNumbers>("multiply", "Multiply two numbers",
        CalcContext.Default.TwoNumbers,
        (input, _) => McpToolResult.Text($"{input.A} * {input.B} = {input.A * input.B}")),

    SdkMcpTool.Create<TwoNumbers>("divide", "Divide one number by another",
        CalcContext.Default.TwoNumbers,
        (input, _) => input.B == 0
            ? McpToolResult.Error("Division by zero is not allowed")
            : McpToolResult.Text($"{input.A} / {input.B} = {input.A / input.B}")),

    SdkMcpTool.Create<SingleNumber>("sqrt", "Calculate square root",
        CalcContext.Default.SingleNumber,
        (input, _) => input.N < 0
            ? McpToolResult.Error($"Cannot calculate square root of negative number {input.N}")
            : McpToolResult.Text($"sqrt({input.N}) = {Math.Sqrt(input.N)}")),

    SdkMcpTool.Create<PowerInput>("power", "Raise a number to a power",
        CalcContext.Default.PowerInput,
        (input, _) => McpToolResult.Text($"{input.Base}^{input.Exponent} = {Math.Pow(input.Base, input.Exponent)}")),
};

// Create the calculator server
var calculator = McpSdkServerConfig.Create("calculator", tools, "2.0.0");

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

// Input records — schema auto-derived from these types
public sealed record TwoNumbers
{
    [JsonPropertyName("a")]
    public double A { get; init; }

    [JsonPropertyName("b")]
    public double B { get; init; }
}

public sealed record SingleNumber
{
    [JsonPropertyName("n")]
    public double N { get; init; }
}

public sealed record PowerInput
{
    [JsonPropertyName("base")]
    public double Base { get; init; }

    [JsonPropertyName("exponent")]
    public double Exponent { get; init; }
}

[JsonSerializable(typeof(TwoNumbers))]
[JsonSerializable(typeof(SingleNumber))]
[JsonSerializable(typeof(PowerInput))]
internal sealed partial class CalcContext : JsonSerializerContext;
