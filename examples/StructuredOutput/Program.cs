using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeAgentSdk;
using ClaudeAgentSdk.Mcp.Sdk;

Console.WriteLine("=== Structured Output Example ===");
Console.WriteLine("""
Demonstrates two approaches to structured JSON output, both AOT-safe:

1. OutputFormatSchema (--json-schema) — Claude uses an internal StructuredOutput
   tool to guarantee the response matches the schema exactly.
2. System prompt workaround — instruct Claude to return JSON, deserialize manually.
""");

// --- Approach 1: OutputFormatSchema (recommended) ---

Console.WriteLine("--- Approach 1: OutputFormatSchema ---\n");

var schema = McpJsonSchema.Object(
    McpJsonSchema.Property("name", McpJsonSchema.String()),
    McpJsonSchema.Property("age", McpJsonSchema.Integer()),
    McpJsonSchema.Property("occupation", McpJsonSchema.String()),
    McpJsonSchema.Property("city", McpJsonSchema.String()));

var schemaOptions = new ClaudeClientOptions
{
    OutputFormatSchema = schema,
    MaxTurns = 3, // StructuredOutput tool needs 2+ turns
};

var person = await Claude.QueryAsync(
    "Extract person info: John Smith is 35 years old, works as a software engineer, and lives in Seattle.",
    ExampleJsonContext.Default.PersonInfo,
    schemaOptions);

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

// --- Approach 2: System prompt workaround ---

Console.WriteLine("\n--- Approach 2: System Prompt Workaround ---\n");

var textOptions = new ClaudeClientOptions
{
    SystemPrompt = "You are a data extraction assistant. Always respond with ONLY valid JSON, no markdown fences, no explanation.",
    MaxTurns = 1,
};

var response = await Claude.QueryTextAsync(
    """Extract person info as JSON with fields "name" (string), "age" (integer), "occupation" (string), "city" (string): Jane Doe is 28 years old, works as a designer, and lives in Portland.""",
    textOptions);

Console.WriteLine($"Raw response: {response}\n");

var person2 = JsonSerializer.Deserialize(response, ExampleJsonContext.Default.PersonInfo);
if (person2 is not null)
{
    Console.WriteLine($"Name: {person2.Name}");
    Console.WriteLine($"Age: {person2.Age}");
    Console.WriteLine($"Occupation: {person2.Occupation}");
    Console.WriteLine($"City: {person2.City}");
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
