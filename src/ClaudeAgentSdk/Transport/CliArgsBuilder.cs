namespace ClaudeAgentSdk.Transport;

/// <summary>
/// Builds CLI command-line arguments from ClaudeClientOptions.
/// </summary>
internal static class CliArgsBuilder
{
    public static List<string> Build(ClaudeClientOptions options)
    {
        var args = new List<string>
        {
            "--output-format", "stream-json",
            "--verbose",
            "--input-format", "stream-json"
        };

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

        // Always set --setting-sources to empty to avoid loading user/project settings
        args.Add("--setting-sources");
        args.Add("");

        return args;
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

        return envVars;
    }
}
