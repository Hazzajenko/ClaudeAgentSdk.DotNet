using System.Text.Json.Serialization;
using ClaudeAgentSdk;

Console.WriteLine("=== Structured Output Example ===\n");

var prompt = "Extract person info: John Smith is 35 years old, works as a software engineer, and lives in Seattle.";

// Old: manual schema definition duplicating the C# type, then stream + fish out StructuredOutput
//
// var schema = McpJsonSchema.Object(
//     McpJsonSchema.Property("name", McpJsonSchema.String()),
//     McpJsonSchema.Property("age", McpJsonSchema.Integer()),
//     McpJsonSchema.Property("occupation", McpJsonSchema.String()),
//     McpJsonSchema.Property("city", McpJsonSchema.String()));
//
// var options = new ClaudeClientOptions { OutputFormatSchema = schema, MaxTurns = 3 };
//
// PersonInfo? person = null;
// await foreach (var message in Claude.QueryAsync(prompt, options))
// {
//     if (message is ResultMessage { StructuredOutput: { } output })
//         person = output.Deserialize(ExampleJsonContext.Default.PersonInfo);
// }

// New: one-liner — schema auto-derived from JsonTypeInfo, deserialization handled internally
var person = await Claude.QueryAsync(prompt, ExampleJsonContext.Default.PersonInfo);

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
