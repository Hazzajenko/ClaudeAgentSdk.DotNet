using System.Text.Json;
using ClaudeAgentSdk.Mcp.Sdk;

namespace ClaudeAgentSdk.Tests.Mcp.Sdk;

public class McpToolResultTests
{
    [Test]
    public async Task Text_CreatesTextResult()
    {
        var result = McpToolResult.Text("hello");

        await Assert.That(result).IsTypeOf<McpTextToolResult>();
        await Assert.That(((McpTextToolResult)result).Value).IsEqualTo("hello");
        await Assert.That(result.IsError).IsFalse();
    }

    [Test]
    public async Task Error_CreatesErrorResult()
    {
        var result = McpToolResult.Error("something broke");

        await Assert.That(result).IsTypeOf<McpTextToolResult>();
        await Assert.That(((McpTextToolResult)result).Value).IsEqualTo("something broke");
        await Assert.That(result.IsError).IsTrue();
    }

    [Test]
    public async Task TextResult_WriteContentTo_WritesTextContentArray()
    {
        var result = McpToolResult.Text("hello world");

        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartArray();
            ((McpTextToolResult)result).WriteContentTo(w);
            w.WriteEndArray();
        }

        string json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement;

        await Assert.That(arr.GetArrayLength()).IsEqualTo(1);

        var item = arr[0];
        await Assert.That(item.GetProperty("type").GetString()).IsEqualTo("text");
        await Assert.That(item.GetProperty("text").GetString()).IsEqualTo("hello world");
    }
}
