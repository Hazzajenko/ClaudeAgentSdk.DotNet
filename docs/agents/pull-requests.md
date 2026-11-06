# Pull requests

How an agent opens a PR in this repo. Use the `gh` CLI. One PR per issue.

## Branch and commits

- Branch from `main`. Name the branch `implement-<n>-<short-subject>`, for example `implement-12-raw-user-message`.
- Write commits as `CLAUDE.md` says: Conventional Commits, one change per commit, `(#<n>)` at the end of each subject.
- Add a line to the `[Unreleased]` section of `CHANGELOG.md` for each change that a user of the package can see. An internal change gets no line.

## Body template

Write the body to a temp file with the file tool and pass it with `--body-file`. Never pass Markdown inline with `--body`, because the shell changes backticks, `$` and quotes. Use these headings in this order:

```markdown
Closes #<n>.

## What changed

- One bullet per behaviour change.

## Choices the issue did not make

- One line each: the question, the choice, and why. Or "None."

## Where this differs from the issue

- Each place where the change does not do what the issue says, and why. Or "None."

## Changelog

The line added under `[Unreleased]` in `CHANGELOG.md`, or "None, internal."

## Acceptance (from #<n>)

- [x] Each acceptance line from the issue, copied as written. Name the test that proves it.

## Proof

- `dotnet build`: pass with no warnings. The build treats warnings as errors, so AOT and trim warnings fail it.
- `dotnet test`: <n> pass. Name the new and changed tests.
```

## Proof

Copy `<n>` from the `total:` line of the `dotnet test` output. Do not count tests by hand. Name each new and changed test. Give no other count, for example of new or changed tests. A reviewer can check the names in the diff, but cannot check a count.

## Choices and differences

A choice is a question that the issue leaves open, for example an option name or a default value. Give the question, the choice and the reason in one line. A difference is a place where the change goes against the issue. Keep the differences apart from the choices, because they need more attention in review.

Do not make a choice that changes the public API in a breaking way or the wire format that the CLI expects. Open a `needs-triage` issue for it, build the rest, and name the issue in the PR body.

## Create the PR

```sh
gh pr create --title "<type>(<scope>): <description> (#<n>)" --body-file /tmp/pr-<n>.md
```

The title follows the commit rules in `CLAUDE.md`.

## After create

- Link the PR to the issue only through `Closes #<n>` in the body. Do not close the issue by hand.
- Put the full PR URL in the final report. A number such as "PR #12" alone is not enough.
- Do not merge. A human merges.
