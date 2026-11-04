# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

C# SDK wrapping the Claude Code CLI agent subprocess protocol. Port of the Python `claude-code-sdk`. Clone the Python SDK to `reference/`, which git ignores, to compare wire formats or to port a missing feature. A C#-only change does not need it.

## Build & Test

```bash
dotnet build
dotnet test
dotnet test --treenode-filter "/*/*/*/*TestMethodName*"   # filter by test name
dotnet test --treenode-filter "/*/*/*/ClassName/*"         # filter by class
dotnet test -- --output-display Standard                  # show console output
```

TUnit uses `--treenode-filter`, not `--filter`. Path format: `/{assembly}/{namespace}/{class}/{test}`.

## Release

A ruleset on `main` blocks direct pushes, so a release goes through a PR in two steps:

1. Run `scripts/release.sh <major|minor|patch>` on an up-to-date `main`. It checks for a clean working tree, a new tag and a non-empty `[Unreleased]` section in CHANGELOG.md. It then stamps the version and date on a `release-<version>` branch, pushes the branch and opens a PR.
2. Merge the PR. Then run `scripts/release.sh tag <version>`. It tags the merge commit and pushes the tag.

The tag push starts `release.yml`, which publishes to NuGet through Trusted Publishing and makes the GitHub release. The script pushes the tag because a tag pushed with the workflow `GITHUB_TOKEN` does not start other workflows.

Update CHANGELOG.md `[Unreleased]` section with each change.

## AOT Constraints

This SDK is fully AOT-compatible. Every code path must pass IL2026/IL3050 analyzers.

- **All JSON must use source-generated `ClaudeJsonContext`**, with no reflection. Adding a new serializable type requires adding `[JsonSerializable(typeof(T))]` to the context.
- `TreatWarningsAsErrors`, `EnableAotAnalyzer`, `EnableTrimAnalyzer` are all enabled in `Directory.Build.props`.
- Dynamic/unknown JSON stored as `JsonElement` must be `.Clone()`d from `JsonDocument` before disposal.
- JSON schema building uses `MemoryStream` → `Utf8JsonWriter` → `JsonDocument.Parse()` → `.RootElement.Clone()` pattern. `McpJsonSchema.cs` has an example.
- Seal classes by default for devirtualization unless inheritance is needed.

## Wire Format Gotchas

The Claude CLI's JSON output has several quirks that required workarounds:

- **Out-of-order type discriminator**: CLI emits `tool_use_id` before `type` in content blocks. `AllowOutOfOrderMetadataProperties = true` on `ClaudeJsonContext` fixes STJ polymorphic deserialization.
- **Task messages at root level**: `task_started`/`task_progress`/`task_notification` put fields at root, not nested in `data`. Deserialize from root element, not wire's `Data` property.
- **UserMessage content polymorphism**: Content can be `string` OR `ContentBlock[]`. Inspect `JsonElement.ValueKind` before deserializing, as `WireMapper.MapUserContent()` does.
- **System message subtype detection**: Manual string matching on `subtype` field to distinguish task messages from generic system messages.
- **Snake case wire format**: `JsonKnownNamingPolicy.SnakeCaseLower` in `ClaudeJsonContext`, `DefaultIgnoreCondition.WhenWritingNull` for optional fields.

## Transport & Control Protocol

- `JsonStreamReader` buffers partial JSON lines. Only catches `JsonException` for incomplete JSON detection. All other exceptions propagate immediately.
- `ControlProtocolHandler` uses `SemaphoreSlim` write lock to prevent interleaved JSON on stdin.
- Initialize handshake has a 120s timeout, because MCP servers can be slow to start.
- Background reader task routes control messages vs content messages via bounded `Channel<Message>` with capacity 100.
- `CliTransport` has 5s graceful shutdown timeout before force kill.

## MCP SDK Server

In-process tools with zero IPC overhead, JSONRPC 2.0 compliant:

- SDK server configs are filtered from CLI `--mcp-config`. They run in-process, not externally.
- Responses wrapped in `{"mcp_response": ...}` for control protocol routing.
- All responses built with `MemoryStream` + `Utf8JsonWriter` + `JsonDocument.Parse()` + `.Clone()`.

## Testing

- **TUnit**: AOT-compatible, source-generated, parallel by default. Test project needs `<OutputType>Exe</OutputType>`.
- **All assertions are async**: `await Assert.That(x).IsEqualTo(y)`. A missing `await` compiles, and the test passes without checking.
- **NSubstitute** for mocking interfaces.
- **`MockTransport`** in tests simulates CLI output via unbounded channels with `OnWrite` callback hook for response simulation.

## Development Approach

- **TDD**: write failing test first, then implement.
- **Reference Python SDK** when debugging wire format issues or implementing features that exist in Python but not yet in C#. Don't reference it for C#-idiomatic changes.
- **Conventional Commits**: `type(scope): description (#N)`. Types are `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore` and `revert`. One change per commit. Add `(#N)` when an issue exists.

## Code Style

Enforced by `.editorconfig` with `EnforceCodeStyleInBuild=true`:

- File-scoped namespaces. Severity error.
- Private fields are `_camelCase`. Severity error.
- `var` only when the type is apparent. Explicit types for built-in types.
- Pattern matching over `is`/`as` with casts. Severity error.
- Expression-bodied members for single-line properties/methods
- Braces preferred on all control flow

## Project Structure

- `src/ClaudeAgentSdk/`: the library, published to NuGet as `ClaudeAgentSdk.DotNet`
- `tests/ClaudeAgentSdk.Tests/`: the TUnit test project
- `examples/`: 12 example projects that show SDK features
- `scripts/release.sh`: version bump and release

## Pull requests

Work goes through GitHub issues and PRs. `docs/agents/pull-requests.md` gives the branch name, the PR body and the proof a PR needs.

## Agent skills

### Issue tracker

Issues live in GitHub Issues on Hazzajenko/ClaudeAgentSdk.DotNet. Use the `gh` CLI. See `docs/agents/issue-tracker.md`.

### Triage labels

The five default triage roles use their default label names. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: one `GLOSSARY.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.
