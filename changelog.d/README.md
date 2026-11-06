# Changelog fragments

Each PR that a user of the package can see adds one file to this folder. A PR never edits `CHANGELOG.md`. Each PR writes its own file, so two open PRs do not conflict on the `[Unreleased]` section.

An internal change gets no fragment.

## File name

Name the file `<issue>-<short-subject>.md`, for example `12-raw-user-message.md`. When no issue exists, name it after the branch, for example `fix-stderr-pipe.md`.

## Content

Write one or more headings from this list. Put the bullet lines for each heading below it:

- `### Added`
- `### Changed`
- `### Deprecated`
- `### Removed`
- `### Fixed`
- `### Security`

Use each heading one time only. Write each bullet as one line. A heading that is not in the list stops the release.

Example `12-raw-user-message.md`:

```markdown
### Added

- `ClaudeClient.SendRawAsync(string messageJson, ct)` writes a pre-built user message to the CLI.

### Fixed

- `SendAsync` no longer drops an empty text block.
```

## Release

`scripts/release.sh` merges all fragments into the new version section of `CHANGELOG.md`. It writes one heading of each kind, in the order of the list above. Lines already in the `[Unreleased]` section come first under each heading. It then removes the fragments with `git rm` in the release commit. This `README.md` stays.
