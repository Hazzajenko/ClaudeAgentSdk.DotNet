# Changelog

All notable changes to this project will be documented in this file.

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
