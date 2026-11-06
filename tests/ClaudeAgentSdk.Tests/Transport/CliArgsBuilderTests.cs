using System.Text.Json;
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
    public async Task Resume_AddsResumeArgAsOneToken()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions { Resume = "sess-123" });

        await Assert.That(args).Contains("--resume=sess-123");
        await Assert.That(args).DoesNotContain("--resume");
    }

    [Test]
    public async Task Resume_DashPrefixedValue_StaysOneToken()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Resume = "--permission-mode=bypassPermissions"
        });

        await Assert.That(args).Contains("--resume=--permission-mode=bypassPermissions");
        await Assert.That(args).DoesNotContain("--permission-mode=bypassPermissions");
        await Assert.That(args).DoesNotContain("--permission-mode");
    }

    [Test]
    [Arguments("sess&calc")]
    [Arguments("sess|calc")]
    [Arguments("sess<in")]
    [Arguments("sess>out")]
    [Arguments("sess^x")]
    [Arguments("sess%PATH%")]
    [Arguments("sess!x!")]
    [Arguments("sess\"x")]
    [Arguments("sess\rx")]
    [Arguments("sess\nx")]
    public async Task Resume_CmdMetacharacterOnWindows_Throws(string resume)
    {
        var options = new ClaudeClientOptions { Resume = resume };

        await Assert.That(() => CliArgsBuilder.Build(options, isWindows: true))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task Resume_CmdMetacharacterOffWindows_IsAllowed()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions { Resume = "sess&x" }, isWindows: false);

        await Assert.That(args).Contains("--resume=sess&x");
    }

    [Test]
    public async Task Resume_PlainSessionIdOnWindows_IsAllowed()
    {
        var args = CliArgsBuilder.Build(
            new ClaudeClientOptions { Resume = "0b6f6f4e-1c2d-4e5f-8a9b-0c1d2e3f4a5b" },
            isWindows: true);

        await Assert.That(args).Contains("--resume=0b6f6f4e-1c2d-4e5f-8a9b-0c1d2e3f4a5b");
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

    [Test]
    public async Task SettingSources_WhenSet_JoinedWithComma()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            SettingSources = ["user", "project"]
        });

        int idx = args.IndexOf("--setting-sources");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).IsEqualTo("user,project");
    }

    [Test]
    public async Task SettingSources_Default_EmptyString()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions());

        int idx = args.IndexOf("--setting-sources");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).IsEqualTo("");
    }

    [Test]
    public async Task Plugins_AddsPluginDirForEach()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Plugins =
            [
                new SdkPluginConfig("local", "/path/to/plugin1"),
                new SdkPluginConfig("local", "/path/to/plugin2")
            ]
        });

        var pluginDirIndices = new List<int>();
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i] == "--plugin-dir") pluginDirIndices.Add(i);
        }

        await Assert.That(pluginDirIndices).Count().IsEqualTo(2);
        await Assert.That(args[pluginDirIndices[0] + 1]).IsEqualTo("/path/to/plugin1");
        await Assert.That(args[pluginDirIndices[1] + 1]).IsEqualTo("/path/to/plugin2");
    }

    [Test]
    public async Task Plugins_Default_OmitsArg()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions());

        await Assert.That(args).DoesNotContain("--plugin-dir");
    }

    [Test]
    public async Task Sandbox_AddsSettingsJsonArg()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Sandbox = new SandboxSettings
            {
                Enabled = true,
                AutoAllowBashIfSandboxed = true,
                ExcludedCommands = ["docker", "git"]
            }
        });

        int idx = args.IndexOf("--settings");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);

        string json = args[idx + 1];
        await Assert.That(json).Contains("\"sandbox\"");
        await Assert.That(json).Contains("\"enabled\":true");
        await Assert.That(json).Contains("\"autoAllowBashIfSandboxed\":true");
        await Assert.That(json).Contains("\"excludedCommands\"");
        await Assert.That(json).Contains("\"docker\"");
    }

    [Test]
    public async Task Sandbox_Default_OmitsSettingsArg()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions());

        await Assert.That(args).DoesNotContain("--settings");
    }

    [Test]
    public async Task Sandbox_WithNetwork_IncludesNetworkConfig()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Sandbox = new SandboxSettings
            {
                Enabled = true,
                Network = new SandboxNetworkConfig
                {
                    AllowLocalBinding = true,
                    AllowUnixSockets = ["/var/run/docker.sock"]
                }
            }
        });

        int idx = args.IndexOf("--settings");
        string json = args[idx + 1];
        await Assert.That(json).Contains("\"network\"");
        await Assert.That(json).Contains("\"allowLocalBinding\":true");
        await Assert.That(json).Contains("/var/run/docker.sock");
    }

    private static string JoinStrings(JsonElement array) =>
        string.Join("|", array.EnumerateArray().Select(e => e.GetString()));

    [Test]
    public async Task Sandbox_IgnoreViolations_WritesEachCommandPatternInOrder()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Sandbox = new SandboxSettings
            {
                IgnoreViolations = new Dictionary<string, IReadOnlyList<string>>
                {
                    ["*"] = ["/etc/hosts"],
                    ["npm install"] = ["registry.npmjs.org", "/tmp"],
                    ["git"] = []
                }
            }
        });

        string json = args[args.IndexOf("--settings") + 1];
        using var doc = JsonDocument.Parse(json);
        JsonElement violations = doc.RootElement.GetProperty("sandbox").GetProperty("ignoreViolations");

        string keys = string.Join("|", violations.EnumerateObject().Select(p => p.Name));
        await Assert.That(keys).IsEqualTo("*|npm install|git");
        await Assert.That(JoinStrings(violations.GetProperty("*"))).IsEqualTo("/etc/hosts");
        await Assert.That(JoinStrings(violations.GetProperty("npm install"))).IsEqualTo("registry.npmjs.org|/tmp");
        await Assert.That(violations.GetProperty("git").GetArrayLength()).IsEqualTo(0);
    }

    [Test]
    public async Task Sandbox_WithNetwork_WritesDomainAndMachLookupFields()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Sandbox = new SandboxSettings
            {
                Network = new SandboxNetworkConfig
                {
                    AllowedDomains = ["github.com", "*.npmjs.org"],
                    DeniedDomains = ["evil.example"],
                    AllowManagedDomainsOnly = true,
                    AllowMachLookup = ["com.apple.*"]
                }
            }
        });

        string json = args[args.IndexOf("--settings") + 1];
        using var doc = JsonDocument.Parse(json);
        JsonElement network = doc.RootElement.GetProperty("sandbox").GetProperty("network");

        await Assert.That(JoinStrings(network.GetProperty("allowedDomains"))).IsEqualTo("github.com|*.npmjs.org");
        await Assert.That(JoinStrings(network.GetProperty("deniedDomains"))).IsEqualTo("evil.example");
        await Assert.That(network.GetProperty("allowManagedDomainsOnly").GetBoolean()).IsTrue();
        await Assert.That(JoinStrings(network.GetProperty("allowMachLookup"))).IsEqualTo("com.apple.*");
    }

    [Test]
    public async Task Sandbox_WithNetwork_OmitsUnsetDomainFields()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Sandbox = new SandboxSettings
            {
                Network = new SandboxNetworkConfig { AllowLocalBinding = true }
            }
        });

        string json = args[args.IndexOf("--settings") + 1];
        await Assert.That(json).DoesNotContain("allowedDomains");
        await Assert.That(json).DoesNotContain("deniedDomains");
        await Assert.That(json).DoesNotContain("allowManagedDomainsOnly");
        await Assert.That(json).DoesNotContain("allowMachLookup");
    }

    [Test]
    public async Task ExtraArgs_WithValue_AddsKeyValue()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            ExtraArgs = new Dictionary<string, string?> { ["custom-flag"] = "value1" }
        });

        int idx = args.IndexOf("--custom-flag");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).IsEqualTo("value1");
    }

    [Test]
    public async Task ExtraArgs_NullValue_AddsBooleanFlag()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            ExtraArgs = new Dictionary<string, string?> { ["debug-to-stderr"] = null }
        });

        await Assert.That(args).Contains("--debug-to-stderr");
    }

    [Test]
    public async Task ExtraArgs_DashPrefixedValue_AddsOneEqualsToken()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            ExtraArgs = new Dictionary<string, string?> { ["custom-flag"] = "--permission-mode=bypassPermissions" }
        });

        await Assert.That(args).Contains("--custom-flag=--permission-mode=bypassPermissions");
        await Assert.That(args).DoesNotContain("--custom-flag");
        await Assert.That(args).DoesNotContain("--permission-mode=bypassPermissions");
    }

    [Test]
    public async Task ExtraArgs_DashPrefixedValue_PrintMode_AddsOneEqualsToken()
    {
        var args = CliArgsBuilder.BuildForPrintMode("hi", new ClaudeClientOptions
        {
            ExtraArgs = new Dictionary<string, string?> { ["custom-flag"] = "-x" }
        });

        await Assert.That(args).Contains("--custom-flag=-x");
        await Assert.That(args).DoesNotContain("--custom-flag");
        await Assert.That(args).DoesNotContain("-x");
    }

    [Test]
    public async Task ExtraArgs_Default_OmitsArg()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions());

        // Should not have any --extra* args (just the normal ones)
        await Assert.That(args).DoesNotContain("--custom-flag");
    }

    [Test]
    public async Task OutputFormatSchema_AddsJsonSchemaArg()
    {
        using var doc = JsonDocument.Parse("""{"type":"object","properties":{"name":{"type":"string"}}}""");
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            OutputFormatSchema = doc.RootElement.Clone()
        });

        int idx = args.IndexOf("--json-schema");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);

        string schema = args[idx + 1];
        await Assert.That(schema).Contains("\"type\":\"object\"");
        await Assert.That(schema).Contains("\"name\"");
    }

    [Test]
    public async Task OutputFormatSchema_Default_OmitsArg()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions());

        await Assert.That(args).DoesNotContain("--json-schema");
    }

    [Test]
    public async Task Settings_RawJson_PassedDirectly()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Settings = """{"permissions":{"allow":["Read"]}}"""
        });

        int idx = args.IndexOf("--settings");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).Contains("\"permissions\"");
    }

    [Test]
    public async Task Settings_FilePath_PassedDirectly()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Settings = "/path/to/settings.json"
        });

        int idx = args.IndexOf("--settings");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).IsEqualTo("/path/to/settings.json");
    }

    [Test]
    public async Task Settings_Default_OmitsArg()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions());

        await Assert.That(args).DoesNotContain("--settings");
    }

    [Test]
    public async Task Settings_WithSandbox_MergesSandboxIntoSettings()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Settings = """{"permissions":{"allow":["Read"]}}""",
            Sandbox = new SandboxSettings { Enabled = true }
        });

        int idx = args.IndexOf("--settings");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);

        string json = args[idx + 1];
        // Both permissions and sandbox present in merged output
        await Assert.That(json).Contains("\"permissions\"");
        await Assert.That(json).Contains("\"sandbox\"");
        await Assert.That(json).Contains("\"enabled\":true");
    }

    [Test]
    public async Task Settings_OnlyAppearsOnce_WhenBothSettingsAndSandbox()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            Settings = """{"foo":"bar"}""",
            Sandbox = new SandboxSettings { Enabled = true }
        });

        int count = args.Count(a => a == "--settings");
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task AddDirectories_SingleDir_EmitsAddDirArg()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            AddDirectories = ["/extra/project"]
        });

        int idx = args.IndexOf("--add-dir");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);
        await Assert.That(args[idx + 1]).IsEqualTo("/extra/project");
    }

    [Test]
    public async Task AddDirectories_MultipleDirs_EmitsMultipleAddDirArgs()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            AddDirectories = ["/dir1", "/dir2", "/dir3"]
        });

        var addDirIndices = new List<int>();
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i] == "--add-dir") addDirIndices.Add(i);
        }

        await Assert.That(addDirIndices).Count().IsEqualTo(3);
        await Assert.That(args[addDirIndices[0] + 1]).IsEqualTo("/dir1");
        await Assert.That(args[addDirIndices[1] + 1]).IsEqualTo("/dir2");
        await Assert.That(args[addDirIndices[2] + 1]).IsEqualTo("/dir3");
    }

    [Test]
    public async Task AddDirectories_Default_OmitsArg()
    {
        var args = CliArgsBuilder.Build(new ClaudeClientOptions());

        await Assert.That(args).DoesNotContain("--add-dir");
    }

    [Test]
    public async Task OutputFormat_WithSchema_ExtractsJsonSchema()
    {
        using var doc = JsonDocument.Parse("""{"type":"json","schema":{"type":"object","properties":{"name":{"type":"string"}}}}""");
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            OutputFormat = doc.RootElement.Clone()
        });

        int idx = args.IndexOf("--json-schema");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);

        string schema = args[idx + 1];
        await Assert.That(schema).Contains("\"type\":\"object\"");
        await Assert.That(schema).Contains("\"name\"");
    }

    [Test]
    public async Task OutputFormatSchema_TakesPrecedence_OverOutputFormat()
    {
        using var formatDoc = JsonDocument.Parse("""{"type":"json","schema":{"type":"string"}}""");
        using var schemaDoc = JsonDocument.Parse("""{"type":"object","properties":{"id":{"type":"number"}}}""");
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            OutputFormat = formatDoc.RootElement.Clone(),
            OutputFormatSchema = schemaDoc.RootElement.Clone()
        });

        int idx = args.IndexOf("--json-schema");
        await Assert.That(idx).IsGreaterThanOrEqualTo(0);

        string schema = args[idx + 1];
        // OutputFormatSchema wins — should have "id", not "name"
        await Assert.That(schema).Contains("\"id\"");
        await Assert.That(schema).DoesNotContain("\"string\"");
    }

    [Test]
    public async Task OutputFormat_WithoutSchema_NoJsonSchemaArg()
    {
        using var doc = JsonDocument.Parse("""{"type":"text"}""");
        var args = CliArgsBuilder.Build(new ClaudeClientOptions
        {
            OutputFormat = doc.RootElement.Clone()
        });

        await Assert.That(args).DoesNotContain("--json-schema");
    }

    [Test]
    public async Task MaxBufferSize_ExposedOnOptions()
    {
        var options = new ClaudeClientOptions { MaxBufferSize = 2048 };

        await Assert.That(options.MaxBufferSize).IsEqualTo(2048);
    }

    [Test]
    public async Task MaxBufferSize_DefaultIsNull()
    {
        var options = new ClaudeClientOptions();

        await Assert.That(options.MaxBufferSize).IsNull();
    }

    [Test]
    public async Task BuildEnvironmentVariables_AlwaysSetsClaudeAgentSdkVersion()
    {
        var env = CliArgsBuilder.BuildEnvironmentVariables(new ClaudeClientOptions());

        await Assert.That(env).ContainsKey("CLAUDE_AGENT_SDK_VERSION");
        await Assert.That(env["CLAUDE_AGENT_SDK_VERSION"]).IsNotNull();
        await Assert.That(env["CLAUDE_AGENT_SDK_VERSION"]).IsNotEqualTo("");
        await Assert.That(env["CLAUDE_AGENT_SDK_VERSION"]).IsNotEqualTo("unknown");
    }
}
