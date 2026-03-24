using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeAgentSdk.Json;
using ClaudeAgentSdk.Messages;
using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk.Tests.Json;

public class JsonSchemaDeriverTests
{
    [Test]
    public async Task FromTypeInfo_FlatDto_ProducesObjectSchema()
    {
        var schema = JsonSchemaDeriver.FromTypeInfo(SchemaTestContext.Default.PersonDto2);

        await Assert.That(schema.ValueKind).IsEqualTo(JsonValueKind.Object);
        // type is ["object","null"] for reference types
        await Assert.That(schema.TryGetProperty("type", out _)).IsTrue();
        await Assert.That(schema.TryGetProperty("properties", out _)).IsTrue();
    }

    [Test]
    public async Task FromTypeInfo_StringProperty_HasStringType()
    {
        var schema = JsonSchemaDeriver.FromTypeInfo(SchemaTestContext.Default.PersonDto2);
        var props = schema.GetProperty("properties");

        await Assert.That(props.TryGetProperty("name", out var nameProp)).IsTrue();
        await Assert.That(nameProp.GetProperty("type").GetString()).IsEqualTo("string");
    }

    [Test]
    public async Task FromTypeInfo_IntProperty_HasIntegerType()
    {
        var schema = JsonSchemaDeriver.FromTypeInfo(SchemaTestContext.Default.PersonDto2);
        var props = schema.GetProperty("properties");

        await Assert.That(props.TryGetProperty("age", out var ageProp)).IsTrue();
        await Assert.That(ageProp.GetProperty("type").GetString()).IsEqualTo("integer");
    }

    [Test]
    public async Task FromTypeInfo_RespectsJsonPropertyName()
    {
        var schema = JsonSchemaDeriver.FromTypeInfo(SchemaTestContext.Default.PersonDto2);
        var props = schema.GetProperty("properties");

        // PersonDto2 has [JsonPropertyName("name")] — should use that, not "Name"
        await Assert.That(props.TryGetProperty("name", out _)).IsTrue();
    }

    [Test]
    public async Task QueryAsyncT_AutoDerivesSchema_WhenNotExplicitlySet()
    {
        var contentMessages = """
            {"type":"assistant","message":{"content":[{"type":"text","text":"{}"}],"model":"m"}}
            {"type":"result","subtype":"success","duration_ms":100,"duration_api_ms":90,"is_error":false,"num_turns":1,"session_id":"s1","structured_output":{"name":"Bob","age":25}}
            """;

        var transport = CreateStreamingMock(contentMessages);

        // Call QueryCoreAsync which is the testable path
        PersonDto2? result = null;
        await foreach (var message in Claude.QueryCoreAsync(transport, "extract"))
        {
            if (message is ResultMessage { StructuredOutput: { } output })
            {
                result = output.Deserialize(SchemaTestContext.Default.PersonDto2);
            }
        }

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("Bob");
        await Assert.That(result.Age).IsEqualTo(25);
    }

    private const string InitControlResponse =
        """{"type":"control_response","response":{"subtype":"success","request_id":"init_1","response":{}}}""";

    private static Transport.MockTransport CreateStreamingMock(string contentMessages)
    {
        var transport = new Transport.MockTransport("");

        transport.OnWrite = json =>
        {
            if (json.Contains("initialize"))
            {
                transport.EnqueueOutput(InitControlResponse);
            }
            else if (json.Contains("\"type\":\"user\""))
            {
                foreach (var line in contentMessages.Split('\n',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (line.Length > 0)
                        transport.EnqueueOutput(line);
                }
                transport.Complete();
            }
        };

        return transport;
    }
}

public sealed record PersonDto2
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("age")]
    public int Age { get; init; }
}

[JsonSerializable(typeof(PersonDto2))]
internal sealed partial class SchemaTestContext : JsonSerializerContext;
