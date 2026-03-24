namespace TestingWithTransport;

/// <summary>
/// Demonstrates testing YOUR application code that wraps the Claude SDK.
/// The SummaryService accepts ITransport? — in production it's null (real CLI),
/// in tests you pass a FakeTransport.
/// </summary>
public class ServiceTests
{
    [Test]
    public async Task SummarizeAsync_ReturnsSummaryText()
    {
        var transport = FakeTransport.WithMessages("""
            {"type":"assistant","message":{"content":[{"type":"text","text":"Brief summary of the input."}],"model":"m"}}
            {"type":"result","subtype":"success","duration_ms":300,"duration_api_ms":250,"is_error":false,"num_turns":1,"session_id":"s1","result":"Brief summary of the input."}
            """);

        var service = new SummaryService();
        string summary = await service.SummarizeAsync("Long article text here...", transport);

        await Assert.That(summary).IsEqualTo("Brief summary of the input.");
    }

    [Test]
    public async Task SummarizeAsync_SendsCorrectPrompt()
    {
        var transport = FakeTransport.WithMessages("""
            {"type":"result","subtype":"success","duration_ms":0,"duration_api_ms":0,"is_error":false,"num_turns":0,"session_id":"s1","result":"ok"}
            """);

        var service = new SummaryService();
        await service.SummarizeAsync("my input text", transport);

        // Verify the prompt includes the user's input
        await Assert.That(transport.WrittenMessages[1]).Contains("Summarize this: my input text");
    }

    [Test]
    public async Task SummarizeWithMetadataAsync_ReturnsStructuredResult()
    {
        var transport = FakeTransport.WithMessages("""
            {"type":"assistant","message":{"content":[{"type":"text","text":"A concise summary."}],"model":"m"}}
            {"type":"result","subtype":"success","duration_ms":500,"duration_api_ms":450,"is_error":false,"num_turns":1,"session_id":"s1","result":"A concise summary."}
            """);

        var service = new SummaryService();
        var result = await service.SummarizeWithMetadataAsync("Some long text", transport);

        await Assert.That(result.Text).IsEqualTo("A concise summary.");
        await Assert.That(result.Turns).IsEqualTo(1);
        await Assert.That(result.IsError).IsFalse();
    }

    [Test]
    public async Task SummarizeWithMetadataAsync_ErrorResponse_SetsIsError()
    {
        var transport = FakeTransport.WithMessages("""
            {"type":"result","subtype":"error","duration_ms":50,"duration_api_ms":0,"is_error":true,"num_turns":0,"session_id":"s1"}
            """);

        var service = new SummaryService();
        var result = await service.SummarizeWithMetadataAsync("anything", transport);

        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Turns).IsEqualTo(0);
    }
}
