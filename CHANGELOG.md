# Changelog

All notable changes to this project will be documented in this file.

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `ConversationResetMessage` with `NewConversationId`, `Uuid` and `SessionId`. The CLI sends a `conversation_reset` frame after `/clear`.
- `McpToolAnnotations.Title` and `McpToolAnnotations.Idempotent`. An SDK MCP server writes them in `tools/list` as `title` and `idempotentHint`. The CLI `mcp_status` response has no such values, so `GetMcpStatusAsync` leaves them null.

### Fixed

- A message with an unknown top-level `type` is now skipped and the session continues. Before, it threw `MessageParseException` and ended the session. `ClaudeJsonOptions.DeserializeMessage` returns `null` for an unknown type.
- A failed SDK MCP tool result now has `"isError": true` on the wire. Before, the SDK wrote `is_error`, which MCP does not read, so the model saw each failed result as a success.
- SDK MCP tool annotations now go to the CLI in `tools/list` as `readOnlyHint`, `destructiveHint` and `openWorldHint`, the MCP names. Before, the SDK wrote `readOnly`, `destructive` and `openWorld`, which MCP clients drop. `ClaudeClient.GetMcpStatusAsync` still reads the `readOnly`, `destructive` and `openWorld` names, because the CLI `mcp_status` response uses them.
- An unknown tool or an exception from an SDK MCP tool handler now comes back as an `isError` tool result with the message, which the model can read. Before, it came back as a JSON-RPC error. A handler that throws `OperationCanceledException` after its token is cancelled still gets a JSON-RPC error.
- An SDK MCP server now echoes a JSON-RPC `id` back unchanged. Before, a string `id` threw before the request was handled.
- `Sessions.GetMessages` now returns every call and every result of a turn that ran tools in parallel. Before, it followed one `parentUuid` branch and dropped the other calls and their results.

### Security

- On Windows, a `.bat` or `.cmd` path from `CliPath` or `CLAUDE_CLI_PATH` now throws `CliConnectionException` before any process starts. Windows runs batch files through cmd.exe, which can run commands injected through CLI arguments. Use the native `claude.exe` instead.
- The Windows fallback search now probes only `~/.local/bin/claude.exe`. It no longer probes extensionless names or `/usr/local/bin/claude`, which resolves to a folder on the current drive that any local user can create. The Windows not-found message no longer recommends the npm package.
- `Resume` now goes to the CLI as one `--resume=<value>` token. Before, a value such as `--permission-mode=bypassPermissions` became its own CLI flag. On Windows, a `Resume` value with `& | < > ^ % ! "`, CR or LF throws `ArgumentException`.
- An `ExtraArgs` value that starts with `-` now goes to the CLI as one `--<flag>=<value>` token, so the CLI cannot read the value as its own flag.

### Fixed

- The stdout reader now parses each line on its own and skips lines that do not start with `{`. Before, one debug line such as `[SandboxDebug] ...` made the reader join every later line onto it, so no more messages arrived until the 1 MB limit threw. A `{` line that does not parse now throws `MessageParseException` at once.
- The CLI no longer blocks when `StderrCallback` is not set. The SDK redirected stderr in streaming mode and never read it, so the CLI stopped when the pipe filled. Stderr now goes to the parent process when there is no callback.
- An exception from `StderrCallback` no longer stops later stderr lines. The SDK catches it and passes the next line to the callback.
- Cancelling the token of the first `SendAsync` or `QueryAsync` call no longer stops `StderrCallback`. The stderr reader used that token, so it stopped and the CLI blocked on a full stderr pipe. The reader now stops when the transport is disposed.

## [0.2.0] - 2026-11-04

### Added

- Multimodal user input: `UserContentBlock` hierarchy (`UserTextBlock`, `UserImageBlock`, `UserImageUrlBlock`, `UserToolResultBlock`) with new `ClaudeClient.SendAsync(IReadOnlyList<UserContentBlock>, sessionId, ct)` and `Claude.QueryAsync(IReadOnlyList<UserContentBlock>, ...)` overloads — sends content as the CLI's `content` array (matching what Claude Desktop sends for image paste), enabling image input + tool result responses. AOT-safe — built with `Utf8JsonWriter` directly, no reflection.
- `ClaudeClient.SendRawAsync(string messageJson, ct)` — escape hatch matching Python SDK's `AsyncIterable[dict]` mode. Writes a pre-built user-message JSON line to the CLI for content shapes not modeled by `UserContentBlock` (e.g. documents, custom block types). Validates well-formed JSON before write.
- `CLAUDE_AGENT_SDK_VERSION` environment variable set on the CLI subprocess for telemetry/debugging (reads assembly version from MinVer)
- `CliNotFoundException.SearchedPath`, `CliConnectionException.ExitCode`/`Stderr`, `MessageParseException.Line` — structured context fields on exception types for programmatic error handling. `MessageParseException` now wraps JSON deserialization errors (previously raw `JsonException`) with the failed line truncated to 100 chars.
- Unicode sanitization on session `Rename`/`Tag` — strips dangerous Unicode (format chars, directional marks, private-use area, zero-width joiners) before writing, matching Python SDK behavior
- `ClaudeClient.ReceiveResponseAsync()` — async iterator that yields messages and auto-stops after the first `ResultMessage`. Use for multi-turn flows where you write user messages directly via the control handler.
- `ClaudeClient.GetServerInfo()` — returns the initialization handshake result (`JsonElement`) from the CLI
- `ClaudeClientOptions.MaxBufferSize` — configurable CLI stdout buffer limit (default 1MB). Threaded through to `JsonStreamReader`.
- `ClaudeClientOptions.OutputFormat` — accepts the full output format dict (e.g. `{"type":"json","schema":{...}}`). Extracts `schema` for `--json-schema` when `OutputFormatSchema` is not set. `OutputFormatSchema` takes precedence when both are set.

## [0.1.0] - 2026-04-05

### Added

- `PermissionUpdate`, `PermissionRuleValue` types and `PermissionUpdateType` enum for typed permission rule management — programmatically add/remove/replace permission rules, change permission mode, or modify allowed directories from `CanUseTool` callbacks
- `PermissionResultAllow.UpdatedPermissions` — return permission updates alongside allow decisions (matches Python SDK's `updated_permissions`)
- `ITransport` is now public with `ConnectAsync` — inject custom transports into `ClaudeClient` and `Claude.QueryAsync()` for testing or non-CLI backends (all overloads accept optional `transport:` parameter)
- CLI version check on connect — verifies `claude --version` >= 2.0.0 before starting, throws `CliVersionException` if too old. Skip with `CLAUDE_AGENT_SDK_SKIP_VERSION_CHECK=1` env var. Skipped when custom transport is injected.
- `ClaudeClientOptions.Settings` — pass arbitrary settings JSON string or file path via `--settings`. When both `Settings` and `Sandbox` are set, merges sandbox config into the settings JSON.
- `ClaudeClientOptions.AddDirectories` — specify additional project directories via `--add-dir` for multi-root workspaces
- `TestingWithTransport` example project — demonstrates testing SDK-dependent code without the CLI using `FakeTransport`, an `ITransport` mock with auto init handshake and canned JSON responses

### Changed

- **BREAKING**: `ToolPermissionContext.Suggestions` changes from `IReadOnlyList<JsonElement>?` to `IReadOnlyList<PermissionUpdate>?` — suggestions from the CLI are now deserialized into typed `PermissionUpdate` records instead of raw JSON
- **BREAKING**: `ITransport` gains `ConnectAsync` method — existing custom implementations (unlikely given it was `internal`) must add this method

## [0.0.8] - 2026-03-24

### Added

- `AssistantMessage.Text` — concatenated text from all `TextBlock` content, null when none. Replaces the 5-line nested pattern match every user had to write.
- `UserMessage.TextContent` / `UserMessage.ContentBlocks` — typed accessors for the polymorphic `Content` property. No more `when content is IReadOnlyList<ContentBlock>` downcasts.
- `McpSdkServerConfig.GetQualifiedToolNames(serverKey)` — generates `mcp__{key}__{tool}` names for `AllowedTools`. Eliminates hand-constructed magic strings.
- `HookMatcher.Create(matcher, callback)` factory methods with sync overload — collapses 7-line object initializer to 1 line, kills `Task.FromResult` noise in sync hook handlers
- Auto-derive JSON schema from `JsonTypeInfo<T>` in `Claude.QueryAsync<T>()` — no more manually duplicating the type shape with `McpJsonSchema.Object(...)`. Uses .NET 9's `JsonSchemaExporter`, AOT-safe.
- `SdkMcpTool.Create<T>(name, description, jsonTypeInfo, handler)` — typed MCP tool handlers with auto-derived input schema. Eliminates manual `McpJsonSchema.Object(...)` construction, raw `JsonElement` property access, and `Task.FromResult` wrapping. Sync and async overloads.
- `MessageCallback` option on `ClaudeClientOptions` — fires for every message before it's yielded, for real-time debug visibility into the message stream
- `Claude.FormatMessage(Message)` — returns a concise debug string for any message type (e.g. `[assistant] 1 blocks: tool_use:Read`, `[result] turns=2 error=False cost=$0.0671`). Use with `MessageCallback = msg => Console.Error.WriteLine(Claude.FormatMessage(msg))`

### Changed

- **BREAKING**: `HookCallback` delegate signature changes from `Task<JsonElement>(JsonElement, string?, CancellationToken)` to `Task<HookOutput>(HookInput, CancellationToken)`. Hook callbacks now receive typed `HookInput` subclasses (`PreToolUseInput`, `PostToolUseInput`, `UserPromptSubmitInput`, etc.) and return `HookOutput` records instead of raw JSON. The `hookEventName` discriminator is auto-set by the concrete `HookSpecificOutput` type — no more manual string matching.
- **BREAKING**: `AssistantMessage.Usage` and `ResultMessage.Usage` change from `JsonElement?` to typed `Usage` class with `InputTokens`, `OutputTokens`, `CacheCreationInputTokens`, `CacheReadInputTokens`. Unknown fields from future CLI versions are captured in `ExtensionData` for forward compatibility.

### Fixed

- Fix agent queries hanging — task system messages (`task_started`, `task_progress`, `task_notification`) failed to deserialize because the CLI puts fields at the top level, not nested in a `data` property
- Fix `JsonStreamReader` silently swallowing deserialization errors as "partial JSON" — any single malformed message corrupted the buffer and killed the entire stream. Non-JSON exceptions now propagate immediately.

## [0.0.7] - 2026-03-24

### Added

- 12 example projects in `examples/` solution demonstrating SDK features: QuickStart, StreamingClient, SystemPrompt, MaxBudget, ToolsConfiguration, StderrCallback, PartialMessages, Agents, Hooks, ToolPermissions, McpCalculator, StructuredOutput

### Fixed

- Fix `--json-schema` structured output not working — `QueryAsync` now uses streaming mode with initialize handshake internally (matching Python SDK) instead of `-p` print mode, which couldn't complete the CLI's multi-turn StructuredOutput tool cycle when stdout is a pipe
- Fix `user` messages with `tool_result` content failing to deserialize when the CLI emits `tool_use_id` before `type` in the JSON (STJ polymorphic deserialization requires the type discriminator to be the first property)

## [0.0.6] - 2026-03-23

### Added

- In-process SDK MCP server: define tools that run inside your application with zero IPC overhead via `McpSdkServerConfig.Create()` and `SdkMcpTool.Create()` — the biggest feature gap vs the Python SDK
- `McpJsonSchema` static builder for constructing AOT-safe JSON Schema input definitions (`Object`, `String`, `Number`, `Integer`, `Boolean`)
- `McpToolResult` hierarchy for tool responses (`McpToolResult.Text()`, `McpToolResult.Error()`)
- `SdkMcpRequestHandler` routes MCP JSONRPC messages (`initialize`, `tools/list`, `tools/call`) to in-process tool handlers via the control protocol `mcp_message` subtype
- STJ source-generated JSON deserialization via `ClaudeJsonContext` and internal wire DTOs — replaces manual `JsonDocument` property extraction with compile-time validated, AOT-safe `[JsonSerializable]` types

### Fixed

- Fix NuGet package metadata pointing to non-existent GitHub repository URL
- Fix `JsonDocument` memory leaks in `SessionMessageParser.ParseTranscriptEntries` and `SessionStorage.ExtractFirstPromptFromHead` — pooled memory was never disposed

### Changed

- `McpConfigSerializer` now skips SDK server configs (they run in-process, not via CLI `--mcp-config`)
- `CliArgsBuilder` omits `--mcp-config` when only SDK servers are configured

### Removed

- `MessageConverter` and `ContentBlockConverter` — replaced by source-generated wire DTO deserialization

## [0.0.5] - 2026-03-22

### Added

- Session utilities: `Sessions.List()`, `Sessions.GetInfo()`, `Sessions.GetMessages()`, `Sessions.Rename()`, `Sessions.Tag()` — reads JSONL session files directly from `~/.claude/projects/` with conversation chain reconstruction
- Bidirectional control protocol: `ControlProtocolHandler` with request/response tracking, background message reader using `Channel<Message>`, and initialize handshake on connect
- Custom agents: `AgentDefinition` type with `AgentModel` enum, sent to CLI via initialize request (not CLI args)
- Runtime control methods on `ClaudeClient`: `InterruptAsync()`, `SetPermissionModeAsync()`, `SetModelAsync()`, `RewindFilesAsync()`, `ReconnectMcpServerAsync()`, `ToggleMcpServerAsync()`, `StopTaskAsync()`, `GetMcpStatusAsync()`
- `McpStatusParser` for typed `McpStatusResponse` from `GetMcpStatusAsync()`
- Hook system: `HookEvent` enum, `HookCallback` delegate, `HookMatcher` record — hooks registered with callback IDs in initialize request, dispatched via control protocol
- Permission callback: `CanUseToolCallback` delegate with `PermissionResultAllow`/`PermissionResultDeny` — automatically adds `--permission-prompt-tool stdio` CLI arg
- `ClaudeClient.ReceiveMessagesAsync()` for continuous message monitoring

### Changed

- Refactor `ClaudeClient` to use background `Task` + `Channel<Message>` for message routing — control messages intercepted before reaching user code
- `ClaudeClient` now sends initialize handshake after connect, enabling agents/hooks/permissions

### Fixed

- Fix GitHub release notes stripping backticked content by using `--notes-file` instead of shell-interpolated `--notes`

## [0.0.4] - 2026-03-22

### Added

- `EffortLevel` option to control thinking depth (`Low`, `Medium`, `High`, `Max`) via `--effort`
- `ThinkingConfig` discriminated union (`Adaptive`, `Enabled(BudgetTokens)`, `Disabled`) — takes precedence over `MaxThinkingTokens`
- `ForkSession` flag to fork resumed sessions into a new session ID
- `Betas` list for enabling beta features (e.g., `context-1m-2025-08-07`)
- `EnableFileCheckpointing` to track file changes for rewind support
- `SettingSources` option to control which setting files are loaded (`user`, `project`, `local`) — replaces hardcoded empty value
- `SdkPluginConfig` for local plugin directory registration via `--plugin-dir`
- `SandboxSettings` with nested `SandboxNetworkConfig` and `SandboxIgnoreViolations` for bash command sandboxing
- `ExtraArgs` escape hatch for passing arbitrary CLI flags not yet covered by the SDK
- `OutputFormatSchema` for structured output via `--json-schema`, with `ResultMessage.StructuredOutput` extraction
- `Claude.QueryAsync<T>()` typed query helper — deserializes structured output using AOT-safe `JsonTypeInfo<T>`
- Task message types: `TaskStartedMessage`, `TaskProgressMessage`, `TaskNotificationMessage` with `TaskUsage` struct — parsed from system message subtypes
- MCP server config types: `McpStdioServerConfig`, `McpSseServerConfig`, `McpHttpServerConfig` with AOT-safe serializer and `--mcp-config` CLI mapping
- MCP status types (`McpServerStatus`, `McpServerInfo`, `McpToolInfo`) for future runtime control

### Changed

- Centralize environment variable building into `CliArgsBuilder.BuildEnvironmentVariables()` (was duplicated in `Claude.cs` and `ClaudeClient.cs`)
- Unseal `SystemMessage` so task message subtypes can inherit from it

## [0.0.3] - 2026-03-22

### Changed

- Rename NuGet package to `ClaudeAgentSdk.DotNet` (`Claude.*` prefix is reserved on NuGet)

## [0.0.2] - 2026-03-22

### Changed

- Rename NuGet package to `ClaudeAgentSdk.DotNet`

## [0.0.1] - 2026-03-21

### Added

- One-shot query API (`Claude.QueryAsync`, `Claude.QueryTextAsync`) for fire-and-forget prompts
- Stateful multi-turn client (`ClaudeClient.SendAsync`) with `IAsyncDisposable` lifecycle
- Full message type system matching the Claude CLI JSON protocol: `AssistantMessage`, `UserMessage`, `SystemMessage`, `ResultMessage`, `StreamEvent`, `RateLimitEvent`
- Content block types: `TextBlock`, `ThinkingBlock`, `ToolUseBlock`, `ToolResultBlock`
- AOT-safe JSON deserialization using `JsonDocument`/`Utf8JsonWriter` — zero reflection, passes IL2026/IL3050 analyzers
- `ClaudeClientOptions` immutable record for all CLI configuration (model, tools, permissions, thinking tokens, etc.)
- `CliTransport` subprocess wrapper with stdin/stdout JSON streaming, stderr piping, and graceful shutdown
- `JsonStreamReader` with partial JSON buffering for handling truncated lines
- `CliFinder` to locate the Claude CLI binary (explicit path → env var → PATH → well-known locations)
- 40 unit tests covering deserialization, stream reading, args building, and query flow
- NuGet package publishing with automatic versioning via MinVer
- GitHub Actions CI pipeline (build + test on push/PR)
- Tag-triggered release workflow (NuGet publish + GitHub release)
- Release script (`scripts/release.sh`) for version bumping and changelog automation
