# Issue tracker: GitHub

Issues and specs for this repo live as GitHub issues on `Hazzajenko/ClaudeAgentSdk.DotNet`. Use the `gh` CLI for all operations.

## Conventions

- **Create an issue**: `gh issue create --title "..." --body "..."`. Use a heredoc for a multi-line body.
- **Read an issue**: `gh issue view <number> --json number,title,body,labels,comments`.
- **List issues**: `gh issue list --state open --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'`. Add `--label` and `--state` filters as needed.
- **Make an issue a sub-issue of a parent**: `gh issue create --parent <parent> ...`, or `gh issue edit <parent> --add-sub-issue <child>` after the child exists. These flags need `gh` 2.94 or later. With an older `gh`, use `gh api --method POST repos/<owner>/<repo>/issues/<parent>/sub_issues -F sub_issue_id=<child-db-id>`. The id is the database id, as in **Blocking** below. If sub-issues are not available, put `Part of #<parent>` at the top of the child body.
- **Blocking**: use GitHub's native issue dependencies. They are the canonical form and show in the UI. Add an edge with `gh issue create --blocked-by <n>,<n> ...` or `gh issue edit <child> --add-blocked-by <blocker>`. These flags need `gh` 2.94 or later. With an older `gh`, use `gh api --method POST repos/<owner>/<repo>/issues/<child>/dependencies/blocked_by -F issue_id=<blocker-db-id>`. `<blocker-db-id>` is the numeric database id of the blocker from `gh api repos/<owner>/<repo>/issues/<n> --jq .id`. It is not the `#number` and not the `node_id`. If dependencies are not available, put a `Blocked by: #<n>, #<n>` line at the top of the child body. A ticket is unblocked when all its blockers are closed.
- **Count open blockers**: `gh api repos/<owner>/<repo>/issues/<n> --jq .issue_dependencies_summary.blocked_by`. This counts open blockers only. 0 means the ticket can start. `gh issue view <n> --json blockedBy` also lists closed blockers, so filter it with `--jq '[.blockedBy.nodes[] | select(.state == "OPEN")] | length'`.
- **Comment on an issue**: `gh issue comment <number> --body "..."`
- **Add or remove labels**: `gh issue edit <number> --add-label "..."` or `--remove-label "..."`
- **Close**: post the reason with `gh issue comment <number> --body "..."`, then run `gh issue close <number>`. On an issue that is already closed, `gh issue close --comment` drops the comment and still exits 0.

`gh` gets the repo from `git remote -v` when it runs inside a clone.

File issues only into this repo. Never file into `mattpocock/skills`, the repo these skills come from. Before the first `gh issue create` of a session, check the target with `gh repo view --json nameWithOwner`. It must print `Hazzajenko/ClaudeAgentSdk.DotNet`. If it prints another repo, ask the user before you continue.

## Pull requests as a triage surface

**PRs as a request surface: no.** Set this to `yes` if this repo treats external PRs as feature requests. `/triage` reads this flag.

When the flag is `yes`, PRs go through the same labels and states as issues. Use the `gh pr` commands:

- **Read a PR**: `gh pr view <number>`, then `gh pr view <number> --comments`, then `gh pr diff <number>` for the diff. Off a TTY, `--comments` prints only the comments and not the body.
- **List external PRs for triage**: `gh api --paginate 'repos/{owner}/{repo}/pulls?state=open' --jq '.[] | select(.author_association | IN("OWNER","MEMBER","COLLABORATOR") | not) | {number, title, author: .user.login, author_association, labels: [.labels[].name]}'`.
- **Comment, label or close**: `gh pr comment`, `gh pr edit --add-label` or `--remove-label`, `gh pr close`.

Issues and PRs share one number space on GitHub. A bare `#42` can be either. Try `gh pr view 42` first, then `gh issue view 42`.

## When a skill says "publish to the issue tracker"

Create a GitHub issue.

## When a skill says "fetch the relevant ticket"

Read it as in **Read an issue** above.

## Wayfinding operations

`/wayfinder` uses these. The **map** is one issue. Its **child** issues are the tickets.

- **Map**: one issue with the label `wayfinder:map`. Its body holds the Notes, Decisions-so-far and Fog sections. Create it with `gh issue create --label wayfinder:map`.
- **Child ticket**: an issue linked to the map as a GitHub sub-issue. See **Make an issue a sub-issue of a parent**. If sub-issues are not enabled, add the child to a task list in the map body and put `Part of #<map>` at the top of the child body. Labels are `wayfinder:research`, `wayfinder:prototype`, `wayfinder:grilling` and `wayfinder:task`. When a dev claims a ticket, assign it to that dev.
- **Blocking**: see **Blocking** under Conventions.
- **Frontier query**: list the open children of the map with `gh issue list --state open`, limited to the sub-issues or the task list of the map. Remove each child that has an assignee or an open blocker. See **Count open blockers**, or look for an open issue in the `Blocked by` line. The first child left, in map order, wins.
- **Claim**: `gh issue edit <n> --add-assignee @me`. This is the first write of the session.
- **Resolve**: `gh issue comment <n> --body "<answer>"`, then `gh issue close <n>`. Then add a short summary and a link to the Decisions-so-far section of the map.
