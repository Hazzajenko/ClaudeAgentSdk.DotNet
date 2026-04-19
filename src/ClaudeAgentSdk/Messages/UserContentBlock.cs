namespace ClaudeAgentSdk.Messages;

/// <summary>
/// Content block for outgoing user messages. Use with
/// <see cref="ClaudeClient.SendAsync(IReadOnlyList{UserContentBlock}, string, CancellationToken)"/>
/// to send multimodal input (text + images, tool results) to Claude.
/// </summary>
public abstract record UserContentBlock;

public sealed record UserTextBlock(string Text) : UserContentBlock;

/// <summary>Base64-encoded inline image. Common media types: "image/png", "image/jpeg", "image/gif", "image/webp".</summary>
public sealed record UserImageBlock(string MediaType, string Base64Data) : UserContentBlock;

public sealed record UserImageUrlBlock(string Url) : UserContentBlock;

/// <summary>Response to a Claude tool_use, sent back as user content.</summary>
public sealed record UserToolResultBlock(string ToolUseId, string? Content = null, bool? IsError = null) : UserContentBlock;
