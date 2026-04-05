using ClaudeAgentSdk.Exceptions;

namespace ClaudeAgentSdk.Tests.Exceptions;

public class ExceptionTests
{
    [Test]
    public async Task CliConnectionException_ExposesExitCodeAndStderr()
    {
        var ex = new CliConnectionException("Process failed") { ExitCode = 1, Stderr = "error output" };

        await Assert.That(ex.ExitCode).IsEqualTo(1);
        await Assert.That(ex.Stderr).IsEqualTo("error output");
        await Assert.That(ex.Message).IsEqualTo("Process failed");
    }

    [Test]
    public async Task CliNotFoundException_ExposesSearchedPath()
    {
        var ex = new CliNotFoundException("Not found") { SearchedPath = "/usr/bin/claude" };

        await Assert.That(ex.SearchedPath).IsEqualTo("/usr/bin/claude");
    }

    [Test]
    public async Task MessageParseException_ExposesLine()
    {
        var ex = new MessageParseException("Parse failed") { Line = "{invalid json" };

        await Assert.That(ex.Line).IsEqualTo("{invalid json");
    }

    [Test]
    public async Task MessageParseException_LineCanBeTruncated()
    {
        string longLine = new string('x', 200);
        string truncated = longLine[..100];
        var ex = new MessageParseException("Parse failed") { Line = truncated };

        await Assert.That(ex.Line!.Length).IsEqualTo(100);
    }

    [Test]
    public async Task StructuredFields_DefaultToNull()
    {
        var conn = new CliConnectionException("msg");
        var notFound = new CliNotFoundException("msg");
        var parse = new MessageParseException("msg");

        await Assert.That(conn.ExitCode).IsNull();
        await Assert.That(conn.Stderr).IsNull();
        await Assert.That(notFound.SearchedPath).IsNull();
        await Assert.That(parse.Line).IsNull();
    }

    [Test]
    public async Task AllExceptions_PreserveInnerException()
    {
        var inner = new InvalidOperationException("inner");

        var conn = new CliConnectionException("msg", inner) { ExitCode = 42 };
        var notFound = new CliNotFoundException("msg", inner) { SearchedPath = "/path" };

        await Assert.That(conn.InnerException).IsEqualTo(inner);
        await Assert.That(conn.ExitCode).IsEqualTo(42);
        await Assert.That(notFound.InnerException).IsEqualTo(inner);
        await Assert.That(notFound.SearchedPath).IsEqualTo("/path");
    }
}
