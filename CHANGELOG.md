# Changelog

All notable changes to this project will be documented in this file.

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
