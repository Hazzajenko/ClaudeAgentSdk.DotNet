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

    [Test]
    public async Task Image_WritesBase64ImageContent()
    {
        var result = McpToolResult.Image([0x89, 0x50, 0x4E, 0x47], "image/png");

        await Assert.That(result).IsTypeOf<McpImageToolResult>();
        await Assert.That(((McpImageToolResult)result).Data).IsEqualTo("iVBORw==");

        var item = WriteSingleContent(result);
        await Assert.That(item.GetProperty("type").GetString()).IsEqualTo("image");
        await Assert.That(item.GetProperty("data").GetString()).IsEqualTo("iVBORw==");
        await Assert.That(item.GetProperty("mimeType").GetString()).IsEqualTo("image/png");
    }

    [Test]
    public async Task ResourceLink_WritesNameUriAndDescriptionAsText()
    {
        var result = McpToolResult.ResourceLink("file:///tmp/report.md", "report.md", "Weekly report");

        await Assert.That(result).IsTypeOf<McpResourceLinkToolResult>();

        var item = WriteSingleContent(result);
        await Assert.That(item.GetProperty("type").GetString()).IsEqualTo("text");
        await Assert.That(item.GetProperty("text").GetString())
            .IsEqualTo("report.md\nfile:///tmp/report.md\nWeekly report");
    }

    [Test]
    public async Task ResourceLink_WithoutDescription_WritesNameAndUri()
    {
        var item = WriteSingleContent(McpToolResult.ResourceLink("https://example.com/a", "a"));

        await Assert.That(item.GetProperty("text").GetString()).IsEqualTo("a\nhttps://example.com/a");
    }

    private static JsonElement WriteSingleContent(McpToolResult result)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartArray();
            result.WriteContentTo(w);
            w.WriteEndArray();
        }

        using var doc = JsonDocument.Parse(stream.ToArray());
        return doc.RootElement[0].Clone();
    }
}
