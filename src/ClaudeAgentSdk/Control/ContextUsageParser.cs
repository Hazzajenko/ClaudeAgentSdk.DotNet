using System.Text.Json;

namespace ClaudeAgentSdk.Control;

internal static class ContextUsageParser
{
    public static ContextUsageResponse Parse(JsonElement root)
    {
        var categories = new List<ContextUsageCategory>();
        foreach (var category in root.GetProperty("categories").EnumerateArray())
        {
            categories.Add(new ContextUsageCategory(
                category.GetProperty("name").GetString()!,
                category.GetProperty("tokens").GetInt32(),
                category.GetProperty("color").GetString()!,
                category.TryGetProperty("isDeferred", out var deferred) && deferred.ValueKind is JsonValueKind.True or JsonValueKind.False
                    ? deferred.GetBoolean()
                    : null));
        }

        var gridRows = new List<IReadOnlyList<JsonElement>>();
        foreach (var row in root.GetProperty("gridRows").EnumerateArray())
        {
            gridRows.Add(CloneItems(row));
        }

        return new ContextUsageResponse
        {
            Categories = categories,
            TotalTokens = root.GetProperty("totalTokens").GetInt32(),
            MaxTokens = root.GetProperty("maxTokens").GetInt32(),
            RawMaxTokens = root.GetProperty("rawMaxTokens").GetInt32(),
            Percentage = root.GetProperty("percentage").GetDouble(),
            Model = root.GetProperty("model").GetString()!,
            IsAutoCompactEnabled = root.GetProperty("isAutoCompactEnabled").GetBoolean(),
            MemoryFiles = CloneItems(root.GetProperty("memoryFiles")),
            McpTools = CloneItems(root.GetProperty("mcpTools")),
            Agents = CloneItems(root.GetProperty("agents")),
            GridRows = gridRows,
            AutoCompactThreshold = root.TryGetProperty("autoCompactThreshold", out var threshold) && threshold.ValueKind == JsonValueKind.Number
                ? threshold.GetInt32()
                : null,
            DeferredBuiltinTools = root.TryGetProperty("deferredBuiltinTools", out var deferredTools) && deferredTools.ValueKind == JsonValueKind.Array
                ? CloneItems(deferredTools)
                : null,
            Raw = root.Clone()
        };
    }

    private static List<JsonElement> CloneItems(JsonElement array)
    {
        var items = new List<JsonElement>(array.GetArrayLength());
        foreach (var item in array.EnumerateArray())
        {
            items.Add(item.Clone());
        }
        return items;
    }
}
