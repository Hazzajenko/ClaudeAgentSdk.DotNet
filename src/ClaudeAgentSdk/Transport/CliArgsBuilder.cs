using System.Reflection;
using System.Text;
using System.Text.Json;
using ClaudeAgentSdk.Mcp;

namespace ClaudeAgentSdk.Transport;

/// <summary>
/// Builds CLI command-line arguments from ClaudeClientOptions.
/// </summary>
internal static class CliArgsBuilder
{
    /// <summary>
    /// Build args for one-shot print mode (-p). Prompt goes on command line, no stdin needed.
    /// Only adds flags that were explicitly set — avoids --system-prompt "" and --setting-sources ""
    /// which can interfere with the CLI's internal tool handling.
    /// </summary>
    public static List<string> BuildForPrintMode(string prompt, ClaudeClientOptions options)
    {
        var args = new List<string>
        {
            "-p", prompt,
            "--output-format", "stream-json",
            "--verbose"
        };

        // Only add system-prompt if explicitly set (non-null, non-empty)
        if (!string.IsNullOrEmpty(options.SystemPrompt))
        {
            args.Add("--system-prompt");
            args.Add(options.SystemPrompt);
        }

        if (options.MaxTurns is not null)
        {
            args.Add("--max-turns");
            args.Add(options.MaxTurns.Value.ToString());
        }

        if (options.Model is not null)
        {
            args.Add("--model");
            args.Add(options.Model);
        }

        AddJsonSchemaArgs(args, options);

        if (options.MaxBudgetUsd is not null)
        {
            args.Add("--max-budget-usd");
            args.Add(options.MaxBudgetUsd.Value.ToString());
        }

        if (options.PermissionMode != PermissionMode.Default)
        {
            args.Add("--permission-mode");
            args.Add(options.PermissionMode switch
            {
                PermissionMode.AcceptEdits => "acceptEdits",
                PermissionMode.Plan => "plan",
                PermissionMode.BypassPermissions => "bypassPermissions",
                _ => "default"
            });
        }

        if (options.ExtraArgs is { Count: > 0 })
        {
            foreach (var (flag, value) in options.ExtraArgs)
            {
                args.Add($"--{flag}");
                if (value is not null)
                    args.Add(value);
            }
        }

        return args;
    }

    /// <summary>
    /// Build args for streaming mode (--input-format stream-json). Used by ClaudeClient.
    /// </summary>
    public static List<string> Build(ClaudeClientOptions options)
    {
        var args = new List<string>
        {
            "--output-format", "stream-json",
            "--verbose",
            "--input-format", "stream-json"
        };

        AddCommonArgs(args, options);
        return args;
    }

    private static void AddCommonArgs(List<string> args, ClaudeClientOptions options)
    {
        if (options.SystemPrompt is not null)
        {
            args.Add("--system-prompt");
            args.Add(options.SystemPrompt);
        }
        else
        {
            args.Add("--system-prompt");
            args.Add("");
        }

        if (options.Tools is { Count: > 0 })
        {
            args.Add("--tools");
            args.Add(string.Join(",", options.Tools));
        }

        if (options.AllowedTools is { Count: > 0 })
        {
            args.Add("--allowedTools");
            args.Add(string.Join(",", options.AllowedTools));
        }

        if (options.DisallowedTools is { Count: > 0 })
        {
            args.Add("--disallowedTools");
            args.Add(string.Join(",", options.DisallowedTools));
        }

        if (options.Model is not null)
        {
            args.Add("--model");
            args.Add(options.Model);
        }

        if (options.FallbackModel is not null)
        {
            args.Add("--fallback-model");
            args.Add(options.FallbackModel);
        }

        if (options.MaxTurns is not null)
        {
            args.Add("--max-turns");
            args.Add(options.MaxTurns.Value.ToString());
        }

        if (options.MaxBudgetUsd is not null)
        {
            args.Add("--max-budget-usd");
            args.Add(options.MaxBudgetUsd.Value.ToString());
        }

        // Thinking config takes precedence over MaxThinkingTokens
        int? resolvedThinkingTokens = options.MaxThinkingTokens;
        if (options.Thinking is not null)
        {
            resolvedThinkingTokens = options.Thinking switch
            {
                ThinkingConfigAdaptive => 32_000,
                ThinkingConfigEnabled e => e.BudgetTokens,
                ThinkingConfigDisabled => 0,
                _ => resolvedThinkingTokens
            };
        }

        if (resolvedThinkingTokens is not null)
        {
            args.Add("--max-thinking-tokens");
            args.Add(resolvedThinkingTokens.Value.ToString());
        }

        if (options.PermissionMode != PermissionMode.Default)
        {
            args.Add("--permission-mode");
            args.Add(options.PermissionMode switch
            {
                PermissionMode.AcceptEdits => "acceptEdits",
                PermissionMode.Plan => "plan",
                PermissionMode.BypassPermissions => "bypassPermissions",
                _ => "default"
            });
        }

        if (options.CanUseTool is not null)
        {
            args.Add("--permission-prompt-tool");
            args.Add("stdio");
        }

        if (options.ContinueConversation)
        {
            args.Add("--continue");
        }

        if (options.ForkSession)
        {
            args.Add("--fork-session");
        }

        if (options.Betas is { Count: > 0 })
        {
            args.Add("--betas");
            args.Add(string.Join(",", options.Betas));
        }

        if (options.Resume is not null)
        {
            args.Add("--resume");
            args.Add(options.Resume);
        }

        if (options.IncludePartialMessages)
        {
            args.Add("--include-partial-messages");
        }

        if (options.Effort is { } effort)
        {
            args.Add("--effort");
            args.Add(effort switch
            {
                EffortLevel.Low => "low",
                EffortLevel.Medium => "medium",
                EffortLevel.High => "high",
                EffortLevel.Max => "max",
                _ => "medium"
            });
        }

        if (options.Settings is not null && options.Sandbox is not null)
        {
            args.Add("--settings");
            args.Add(MergeSettingsWithSandbox(options.Settings, options.Sandbox));
        }
        else if (options.Settings is not null)
        {
            args.Add("--settings");
            args.Add(options.Settings);
        }
        else if (options.Sandbox is not null)
        {
            args.Add("--settings");
            args.Add(SerializeSandboxSettings(options.Sandbox));
        }

        if (options.AddDirectories is { Count: > 0 })
        {
            foreach (string dir in options.AddDirectories)
            {
                args.Add("--add-dir");
                args.Add(dir);
            }
        }

        if (options.Plugins is { Count: > 0 })
        {
            foreach (var plugin in options.Plugins)
            {
                if (plugin.Type == "local")
                {
                    args.Add("--plugin-dir");
                    args.Add(plugin.Path);
                }
            }
        }

        if (options.McpServers is { Count: > 0 } &&
            options.McpServers.Any(kvp => kvp.Value is not McpSdkServerConfig))
        {
            args.Add("--mcp-config");
            args.Add(McpConfigSerializer.Serialize(options.McpServers));
        }

        AddJsonSchemaArgs(args, options);

        if (options.ExtraArgs is { Count: > 0 })
        {
            foreach (var (flag, value) in options.ExtraArgs)
            {
                args.Add($"--{flag}");
                if (value is not null)
                    args.Add(value);
            }
        }

        args.Add("--setting-sources");
        args.Add(options.SettingSources is { Count: > 0 }
            ? string.Join(",", options.SettingSources)
            : "");
    }

    private static void AddJsonSchemaArgs(List<string> args, ClaudeClientOptions options)
    {
        if (options.OutputFormatSchema is { } schema)
        {
            args.Add("--json-schema");
            args.Add(schema.GetRawText());
        }
        else if (options.OutputFormat is { } format
            && format.TryGetProperty("schema", out var formatSchema))
        {
            args.Add("--json-schema");
            args.Add(formatSchema.GetRawText());
        }
    }

    private static string SerializeSandboxSettings(SandboxSettings sandbox)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WritePropertyName("sandbox");
            WriteSandboxObject(w, sandbox);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static Dictionary<string, string> BuildEnvironmentVariables(ClaudeClientOptions options)
    {
        var envVars = new Dictionary<string, string>();

        if (options.EnvironmentVariables is not null)
        {
            foreach (var (key, value) in options.EnvironmentVariables)
            {
                envVars[key] = value;
            }
        }

        if (options.EnableFileCheckpointing)
        {
            envVars["CLAUDE_CODE_ENABLE_SDK_FILE_CHECKPOINTING"] = "true";
        }

        envVars["CLAUDE_AGENT_SDK_VERSION"] = typeof(CliArgsBuilder).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";

        return envVars;
    }

    private static string MergeSettingsWithSandbox(string settings, SandboxSettings sandbox)
    {
        using var settingsDoc = JsonDocument.Parse(settings);

        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();

            // Copy all existing properties from settings
            foreach (var prop in settingsDoc.RootElement.EnumerateObject())
            {
                if (prop.Name == "sandbox")
                    continue; // Sandbox option overwrites existing sandbox key
                prop.WriteTo(w);
            }

            // Write sandbox from the typed SandboxSettings
            w.WritePropertyName("sandbox");
            WriteSandboxObject(w, sandbox);

            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteSandboxObject(Utf8JsonWriter w, SandboxSettings sandbox)
    {
        w.WriteStartObject();

        if (sandbox.Enabled is { } enabled)
            w.WriteBoolean("enabled", enabled);
        if (sandbox.AutoAllowBashIfSandboxed is { } autoAllow)
            w.WriteBoolean("autoAllowBashIfSandboxed", autoAllow);
        if (sandbox.AllowUnsandboxedCommands is { } allowUnsandboxed)
            w.WriteBoolean("allowUnsandboxedCommands", allowUnsandboxed);
        if (sandbox.EnableWeakerNestedSandbox is { } weakerNested)
            w.WriteBoolean("enableWeakerNestedSandbox", weakerNested);

        if (sandbox.ExcludedCommands is { Count: > 0 })
        {
            w.WriteStartArray("excludedCommands");
            foreach (string cmd in sandbox.ExcludedCommands)
                w.WriteStringValue(cmd);
            w.WriteEndArray();
        }

        if (sandbox.Network is { } network)
        {
            w.WriteStartObject("network");
            if (network.AllowLocalBinding is { } allowLocal)
                w.WriteBoolean("allowLocalBinding", allowLocal);
            if (network.AllowAllUnixSockets is { } allowAll)
                w.WriteBoolean("allowAllUnixSockets", allowAll);
            if (network.HttpProxyPort is { } httpProxy)
                w.WriteNumber("httpProxyPort", httpProxy);
            if (network.SocksProxyPort is { } socksProxy)
                w.WriteNumber("socksProxyPort", socksProxy);
            if (network.AllowUnixSockets is { Count: > 0 })
            {
                w.WriteStartArray("allowUnixSockets");
                foreach (string sock in network.AllowUnixSockets)
                    w.WriteStringValue(sock);
                w.WriteEndArray();
            }
            w.WriteEndObject();
        }

        if (sandbox.IgnoreViolations is { } violations)
        {
            w.WriteStartObject("ignoreViolations");
            if (violations.File is { Count: > 0 })
            {
                w.WriteStartArray("file");
                foreach (string f in violations.File)
                    w.WriteStringValue(f);
                w.WriteEndArray();
            }
            if (violations.Network is { Count: > 0 })
            {
                w.WriteStartArray("network");
                foreach (string n in violations.Network)
                    w.WriteStringValue(n);
                w.WriteEndArray();
            }
            w.WriteEndObject();
        }

        w.WriteEndObject();
    }
}
