# Changelog

All notable changes to this project will be documented in this file.

Format based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
