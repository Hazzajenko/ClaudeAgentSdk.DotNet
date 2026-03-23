using System.Text.Json;
using ClaudeAgentSdk.Mcp.Sdk;

namespace ClaudeAgentSdk.Tests.Mcp.Sdk;

public class McpJsonSchemaTests
{
    [Test]
    public async Task String_ReturnsStringTypeSchema()
    {
        var schema = McpJsonSchema.String();

        await Assert.That(schema.GetProperty("type").GetString()).IsEqualTo("string");
    }

    [Test]
    public async Task String_WithDescription_IncludesDescription()
    {
        var schema = McpJsonSchema.String("A name");

        await Assert.That(schema.GetProperty("type").GetString()).IsEqualTo("string");
        await Assert.That(schema.GetProperty("description").GetString()).IsEqualTo("A name");
    }

    [Test]
    public async Task Number_ReturnsNumberTypeSchema()
    {
        var schema = McpJsonSchema.Number();

        await Assert.That(schema.GetProperty("type").GetString()).IsEqualTo("number");
    }

    [Test]
    public async Task Integer_ReturnsIntegerTypeSchema()
    {
        var schema = McpJsonSchema.Integer();

        await Assert.That(schema.GetProperty("type").GetString()).IsEqualTo("integer");
    }

    [Test]
    public async Task Boolean_ReturnsBooleanTypeSchema()
    {
        var schema = McpJsonSchema.Boolean();

        await Assert.That(schema.GetProperty("type").GetString()).IsEqualTo("boolean");
    }

    [Test]
    public async Task Object_WithProperties_ReturnsObjectSchema()
    {
        var schema = McpJsonSchema.Object(
            McpJsonSchema.Property("name", McpJsonSchema.String()),
            McpJsonSchema.Property("age", McpJsonSchema.Integer())
        );

        await Assert.That(schema.GetProperty("type").GetString()).IsEqualTo("object");

        var props = schema.GetProperty("properties");
        await Assert.That(props.GetProperty("name").GetProperty("type").GetString()).IsEqualTo("string");
        await Assert.That(props.GetProperty("age").GetProperty("type").GetString()).IsEqualTo("integer");

        // All properties required by default
        var required = schema.GetProperty("required");
        await Assert.That(required.GetArrayLength()).IsEqualTo(2);
    }

    [Test]
    public async Task Object_NoProperties_ReturnsEmptyObjectSchema()
    {
        var schema = McpJsonSchema.Object();

        await Assert.That(schema.GetProperty("type").GetString()).IsEqualTo("object");
        await Assert.That(schema.GetProperty("properties").EnumerateObject().Count()).IsEqualTo(0);
        await Assert.That(schema.GetProperty("required").GetArrayLength()).IsEqualTo(0);
    }

    [Test]
    public async Task Property_CreatesNamedProperty()
    {
        var prop = McpJsonSchema.Property("foo", McpJsonSchema.Boolean());

        await Assert.That(prop.Name).IsEqualTo("foo");
        await Assert.That(prop.Schema.GetProperty("type").GetString()).IsEqualTo("boolean");
    }
}
