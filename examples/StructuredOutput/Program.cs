using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeAgentSdk;
using ClaudeAgentSdk.Messages;

Console.WriteLine("=== Structured Output Example ===");
Console.WriteLine("""
Demonstrates parsing structured JSON from Claude's response
using System.Text.Json source generation — AOT-safe, no reflection.

Note: OutputFormatSchema (--json-schema) requires multi-turn tool interaction
that the SDK doesn't yet support in one-shot mode. This example uses a system
prompt to instruct Claude to return JSON, then deserializes with source-gen.
""");

var options = new ClaudeClientOptions
{
    SystemPrompt = "You are a data extraction assistant. Always respond with ONLY valid JSON, no markdown fences, no explanation.",
    MaxTurns = 1,
};

var response = await Claude.QueryTextAsync(
    """Extract person info as JSON with fields "name" (string), "age" (integer), "occupation" (string), "city" (string): John Smith is 35 years old, works as a software engineer, and lives in Seattle.""",
    options);

Console.WriteLine($"Raw response: {response}\n");

var person = JsonSerializer.Deserialize(response, ExampleJsonContext.Default.PersonInfo);
if (person is not null)
{
    Console.WriteLine($"Name: {person.Name}");
    Console.WriteLine($"Age: {person.Age}");
    Console.WriteLine($"Occupation: {person.Occupation}");
    Console.WriteLine($"City: {person.City}");
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
