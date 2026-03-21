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

        if (options.MaxThinkingTokens is not null)
        {
            args.Add("--max-thinking-tokens");
            args.Add(options.MaxThinkingTokens.Value.ToString());
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

        if (options.Resume is not null)
        {
            args.Add("--resume");
            args.Add(options.Resume);
        }

        if (options.IncludePartialMessages)
        {
            args.Add("--include-partial-messages");
        }

        // Always set --setting-sources to empty to avoid loading user/project settings
        args.Add("--setting-sources");
        args.Add("");

        return args;
    }
}
