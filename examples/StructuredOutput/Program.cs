using System.Text.Json.Serialization;
using ClaudeAgentSdk;
using ClaudeAgentSdk.Mcp.Sdk;

Console.WriteLine("=== Structured Output Example ===\n");

var schema = McpJsonSchema.Object(
    McpJsonSchema.Property("name", McpJsonSchema.String()),
    McpJsonSchema.Property("age", McpJsonSchema.Integer()),
    McpJsonSchema.Property("occupation", McpJsonSchema.String()),
    McpJsonSchema.Property("city", McpJsonSchema.String()));

var options = new ClaudeClientOptions
{
    OutputFormatSchema = schema,
    MaxTurns = 3, // StructuredOutput tool needs 2+ turns
};

var person = await Claude.QueryAsync(
    "Extract person info: John Smith is 35 years old, works as a software engineer, and lives in Seattle.",
    ExampleJsonContext.Default.PersonInfo,
    options);

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
