using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeAgentSdk;
using ClaudeAgentSdk.Messages;
using ClaudeAgentSdk.Mcp.Sdk;

Console.WriteLine("=== Structured Output Example (debug) ===\n");

var prompt = "Extract person info: John Smith is 35 years old, works as a software engineer, and lives in Seattle.";

var schema = McpJsonSchema.Object(
    McpJsonSchema.Property("name", McpJsonSchema.String()),
    McpJsonSchema.Property("age", McpJsonSchema.Integer()),
    McpJsonSchema.Property("occupation", McpJsonSchema.String()),
    McpJsonSchema.Property("city", McpJsonSchema.String()));

var options = new ClaudeClientOptions
{
    OutputFormatSchema = schema,
    MaxTurns = 3,
    StderrCallback = line => Console.Error.WriteLine($"[stderr] {line}"),
    // MessageCallback = msg => Console.Error.WriteLine(Claude.FormatMessage(msg)),
};

Console.WriteLine("CLI command:");
Console.WriteLine(Claude.GetCliCommand(prompt, options));
Console.WriteLine();

using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));

PersonInfo? person = null;

try
{
await foreach (var message in Claude.QueryAsync(prompt, options, cts.Token))
{
    switch (message)
    {
        case SystemMessage sys:
            Console.WriteLine($"[SystemMessage] subtype={sys.Subtype}");
            break;
        case AssistantMessage asst:
            Console.WriteLine($"[AssistantMessage] blocks={asst.Content.Count}");
            foreach (var block in asst.Content)
            {
                Console.WriteLine(block switch
                {
                    TextBlock t => $"  text: {t.Text[..Math.Min(t.Text.Length, 200)]}",
                    ThinkingBlock th => $"  thinking: (len={th.Thinking.Length})",
                    ToolUseBlock tu => $"  tool_use: name={tu.Name} id={tu.Id} input={tu.Input}",
                    ToolResultBlock tr => $"  tool_result: id={tr.ToolUseId}",
                    _ => $"  {block.GetType().Name}"
                });
            }
            break;
        case UserMessage usr:
            Console.WriteLine($"[UserMessage] parent_tool_use_id={usr.ParentToolUseId}");
            break;
        case ResultMessage res:
            Console.WriteLine($"[ResultMessage] subtype={res.Subtype} turns={res.NumTurns} is_error={res.IsError} stop={res.StopReason} result={res.Result?[..Math.Min(res.Result?.Length ?? 0, 200)]}");
            Console.WriteLine($"  structured_output={res.StructuredOutput}");
            if (res.StructuredOutput is { } output)
                person = output.Deserialize(ExampleJsonContext.Default.PersonInfo);
            break;
        case RateLimitEvent rle:
            Console.WriteLine($"[RateLimitEvent] status={rle.RateLimitInfo.Status}");
            break;
        default:
            Console.WriteLine($"[{message.GetType().Name}]");
            break;
    }
}
}
catch (OperationCanceledException)
{
    Console.WriteLine("\nTIMEOUT: Query exceeded 45 seconds.");
}

Console.WriteLine();

if (person is not null)
{
    Console.WriteLine($"Name: {person.Name}");
    Console.WriteLine($"Age: {person.Age}");
    Console.WriteLine($"Occupation: {person.Occupation}");
    Console.WriteLine($"City: {person.City}");
}
else
{
    Console.WriteLine("No structured output received.");
}

// Record type for structured output
public sealed record PersonInfo
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("age")]
    public int Age { get; init; }

    [JsonPropertyName("occupation")]
    public string Occupation { get; init; } = "";

    [JsonPropertyName("city")]
    public string City { get; init; } = "";
}

// Source-generated JSON context — AOT-safe, no reflection
[JsonSerializable(typeof(PersonInfo))]
internal sealed partial class ExampleJsonContext : JsonSerializerContext;
