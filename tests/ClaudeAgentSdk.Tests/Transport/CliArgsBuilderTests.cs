using ClaudeAgentSdk;
using ClaudeAgentSdk.Transport;

namespace ClaudeAgentSdk.Tests.Transport;

public class CliArgsBuilderTests
{
    [Test]
    public async Task DefaultOptions_HasRequiredBaseArgs()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions());

        await Assert.That(args).Contains("--output-format");
        await Assert.That(args).Contains("stream-json");
        await Assert.That(args).Contains("--verbose");
        await Assert.That(args).Contains("--input-format");
        await Assert.That(args).Contains("--system-prompt");
    }

    [Test]
    public async Task Model_AddsModelArg()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions { Model = "claude-sonnet-4-5-20250929" });

        int idx = args.IndexOf("--model");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).IsEqualTo("claude-sonnet-4-5-20250929");
    }

    [Test]
    public async Task AllowedTools_JoinedWithComma()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            AllowedTools = ["Read", "Edit", "Bash"]
        });

        int idx = args.IndexOf("--allowedTools");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).IsEqualTo("Read,Edit,Bash");
    }

    [Test]
    public async Task MaxTurns_AddsArg()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions { MaxTurns = 5 });

        int idx = args.IndexOf("--max-turns");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).IsEqualTo("5");
    }

    [Test]
    public async Task PermissionMode_AcceptEdits_MapsCorrectly()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            PermissionMode = PermissionMode.AcceptEdits
        });

        int idx = args.IndexOf("--permission-mode");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).IsEqualTo("acceptEdits");
    }

    [Test]
    public async Task PermissionMode_Default_OmitsArg()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions());

        await Assert.That(args).DoesNotContain("--permission-mode");
    }

    [Test]
    public async Task SystemPrompt_SetExplicitly()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            SystemPrompt = "You are a coding assistant."
        });

        int idx = args.IndexOf("--system-prompt");
        await Assert.That(args[idx + 1]).IsEqualTo("You are a coding assistant.");
    }

    [Test]
    public async Task SystemPrompt_DefaultIsEmpty()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions());

        int idx = args.IndexOf("--system-prompt");
        await Assert.That(args[idx + 1]).IsEqualTo("");
    }

    [Test]
    public async Task ContinueConversation_AddsContinueFlag()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions { ContinueConversation = true });

        await Assert.That(args).Contains("--continue");
    }

    [Test]
    public async Task Resume_AddsResumeArg()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions { Resume = "sess-123" });

        int idx = args.IndexOf("--resume");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).IsEqualTo("sess-123");
    }

    [Test]
    public async Task MaxThinkingTokens_AddsArg()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions { MaxThinkingTokens = 10000 });

        int idx = args.IndexOf("--max-thinking-tokens");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).IsEqualTo("10000");
    }

    [Test]
    [Arguments(EffortLevel.Low, "low")]
    [Arguments(EffortLevel.Medium, "medium")]
    [Arguments(EffortLevel.High, "high")]
    [Arguments(EffortLevel.Max, "max")]
    public async Task Effort_MapsToCliString(EffortLevel level, string expected)
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions { Effort = level });

        int idx = args.IndexOf("--effort");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).IsEqualTo(expected);
    }

    [Test]
    public async Task Effort_Default_OmitsArg()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions());

        await Assert.That(args).DoesNotContain("--effort");
    }

    [Test]
    public async Task Thinking_Adaptive_DefaultsTo32000()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Thinking = new ThinkingConfigAdaptive()
        });

        int idx = args.IndexOf("--max-thinking-tokens");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).IsEqualTo("32000");
    }

    [Test]
    public async Task Thinking_Enabled_UsesBudgetTokens()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Thinking = new ThinkingConfigEnabled(16000)
        });

        int idx = args.IndexOf("--max-thinking-tokens");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).IsEqualTo("16000");
    }

    [Test]
    public async Task Thinking_Disabled_SetsZero()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Thinking = new ThinkingConfigDisabled()
        });

        int idx = args.IndexOf("--max-thinking-tokens");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).IsEqualTo("0");
    }

    [Test]
    public async Task Thinking_TakesPrecedenceOverMaxThinkingTokens()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            MaxThinkingTokens = 5000,
            Thinking = new ThinkingConfigEnabled(20000)
        });

        int idx = args.IndexOf("--max-thinking-tokens");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).IsEqualTo("20000");

        // Should only appear once
        int secondIdx = args.IndexOf("--max-thinking-tokens", idx + 1);
        await Assert.That(secondIdx).IsEqualTo(-1);
    }

    [Test]
    public async Task ForkSession_True_AddsFlag()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions { ForkSession = true });

        await Assert.That(args).Contains("--fork-session");
    }

    [Test]
    public async Task ForkSession_Default_OmitsFlag()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions());

        await Assert.That(args).DoesNotContain("--fork-session");
    }

    [Test]
    public async Task Betas_JoinedWithComma()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Betas = ["context-1m-2025-08-07", "another-beta"]
        });

        int idx = args.IndexOf("--betas");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).IsEqualTo("context-1m-2025-08-07,another-beta");
    }

    [Test]
    public async Task Betas_Default_OmitsArg()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions());

        await Assert.That(args).DoesNotContain("--betas");
    }

    [Test]
    public async Task EnableFileCheckpointing_SetsEnvVar()
    {
        var env = CliArgsBuilder.BuildEnvironmentVariables(new ClaudeClientOptions
        {
            EnableFileCheckpointing = true
        });

        await Assert.That(env).ContainsKey("CLAUDE_CODE_ENABLE_SDK_FILE_CHECKPOINTING");
        await Assert.That(env["CLAUDE_CODE_ENABLE_SDK_FILE_CHECKPOINTING"]).IsEqualTo("true");
    }

    [Test]
    public async Task EnableFileCheckpointing_Default_NoEnvVar()
    {
        var env = CliArgsBuilder.BuildEnvironmentVariables(new ClaudeClientOptions());

        await Assert.That(env).DoesNotContainKey("CLAUDE_CODE_ENABLE_SDK_FILE_CHECKPOINTING");
    }

    [Test]
    public async Task BuildEnvironmentVariables_MergesUserEnvVars()
    {
        var env = CliArgsBuilder.BuildEnvironmentVariables(new ClaudeClientOptions
        {
            EnvironmentVariables = new Dictionary<string, string> { ["MY_VAR"] = "hello" }
        });

        await Assert.That(env).ContainsKey("MY_VAR");
        await Assert.That(env["MY_VAR"]).IsEqualTo("hello");
    }
}
