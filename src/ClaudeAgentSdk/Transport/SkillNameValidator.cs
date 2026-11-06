namespace ClaudeAgentSdk.Transport;

/// <summary>
/// Checks the names in <see cref="ClaudeClientOptions.Skills"/>. Each name goes into a
/// <c>Skill(name)</c> rule in <c>--allowedTools</c>. The CLI splits that value on commas
/// and spaces outside parentheses and has no escape at that step, so a name with a delimiter
/// can add other permission rules. Names that can never match a skill also throw, so a dead
/// rule fails at connect and does not grant nothing in silence. The checks match the Python SDK.
/// </summary>
internal static class SkillNameValidator
{
    private const string ParamName = nameof(ClaudeClientOptions.Skills);

    /// <summary>
    /// Throws <see cref="ArgumentException"/> when a name in a <see cref="SkillsConfigList"/> is not valid.
    /// </summary>
    public static void Validate(SkillsConfig? skills)
    {
        if (skills is not SkillsConfigList list)
        {
            return;
        }

        if (list.Names is null)
        {
            throw new ArgumentException("SkillsConfigList.Names must not be null.", ParamName);
        }

        foreach (string name in list.Names)
        {
            ValidateName(name);
        }
    }

    private static void ValidateName(string? name)
    {
        if (name is null || name.Trim().Length == 0)
        {
            throw Invalid("Skill names must be non-empty strings.");
        }

        if (HasUnpairedSurrogate(name))
        {
            throw Invalid($"Invalid skill name '{name}': it has an unpaired surrogate, which can never match a skill the CLI found.");
        }

        if (name.Length != name.Trim().Length)
        {
            throw Invalid($"Invalid skill name '{name}': leading or trailing whitespace can never match, because the Skill tool trims the name.");
        }

        if (name.Any(IsDelimiterOrControl))
        {
            throw Invalid($"Invalid skill name '{name}': parentheses, commas, control characters and byte order marks are not allowed. Use the skill directory name, or 'plugin:skill' for a plugin skill.");
        }

        if (name == "*")
        {
            throw Invalid("Invalid skill name '*': use SkillsConfigAll to turn on every skill.");
        }

        if (name.EndsWith(":*", StringComparison.Ordinal) || name.EndsWith(" *", StringComparison.Ordinal))
        {
            throw Invalid($"Invalid skill name '{name}': wildcard suffixes are not allowed. Give each skill by its exact name.");
        }

        if (name.StartsWith('/'))
        {
            throw Invalid($"Invalid skill name '{name}': a name cannot start with '/'. Give the skill name, not the slash command.");
        }

        if (name.Contains(@"\\", StringComparison.Ordinal))
        {
            throw Invalid($"Invalid skill name '{name}': two backslashes in a row are not allowed. The rule parser makes them one, so the rule names a different skill.");
        }

        if (name.EndsWith('\\'))
        {
            throw Invalid($"Invalid skill name '{name}': a name cannot end with a backslash.");
        }
    }

    private static bool IsDelimiterOrControl(char c) =>
        c is '(' or ')' or ','
            or (>= '\u0000' and <= '\u001f')
            or (>= '\u007f' and <= '\u009f')
            or '﻿';

    private static bool HasUnpairedSurrogate(string name)
    {
        for (int i = 0; i < name.Length; i++)
        {
            if (char.IsHighSurrogate(name[i]) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(name[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static ArgumentException Invalid(string message) => new(message, ParamName);
}
