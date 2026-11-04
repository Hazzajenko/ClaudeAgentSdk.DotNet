#!/usr/bin/env bash
set -euo pipefail

# Releases go through a PR because the ruleset on main blocks direct pushes.
#
#   1. release.sh <bump>         stamps CHANGELOG.md on a release branch and opens a PR
#   2. release.sh tag <version>  after the PR merges, tags the merge commit and pushes the tag
#
# The tag push starts .github/workflows/release.yml, which publishes to NuGet.
# The script pushes the tag, not a workflow, because a tag pushed with the
# workflow GITHUB_TOKEN does not start other workflows.

usage() {
  echo "Usage:"
  echo "  $0 <major|minor|patch|X.Y.Z> [--pre-release <label>]   open the release PR"
  echo "  $0 tag <version>                                      tag the merged release PR"
  echo ""
  echo "Examples:"
  echo "  $0 minor                      # v0.1.0 → PR for v0.2.0"
  echo "  $0 minor --pre-release alpha  # v0.1.0 → PR for v0.2.0-alpha"
  echo "  $0 0.3.0                      # explicit version"
  echo "  $0 tag 0.2.0                  # after merge: push tag v0.2.0"
  exit 1
}

# State for rollback_release_pr. A failed run must leave no local branch,
# remote branch or temp file behind, so the next run can start clean.
ROLLBACK_BRANCH=""
ROLLBACK_PUSHED=0
BODY_FILE=""

rollback_release_pr() {
  local status=$?
  [[ -n "$BODY_FILE" ]] && rm -f "$BODY_FILE"
  if [[ $status -ne 0 && -n "$ROLLBACK_BRANCH" ]]; then
    echo "Rolling back $ROLLBACK_BRANCH" >&2
    # Restore from HEAD, not the index, so a staged stamp is also undone.
    git checkout --quiet HEAD -- CHANGELOG.md 2>/dev/null || true
    git switch --quiet main || true
    git branch --quiet -D "$ROLLBACK_BRANCH" 2>/dev/null || true
    if [[ $ROLLBACK_PUSHED -eq 1 ]]; then
      git push --quiet origin --delete "$ROLLBACK_BRANCH" || echo "Delete origin/$ROLLBACK_BRANCH by hand" >&2
    fi
  fi
  exit $status
}

fail() {
  echo "Error: $*" >&2
  exit 1
}

require_clean_tree() {
  if ! git diff --quiet || ! git diff --cached --quiet; then
    fail "uncommitted changes. Commit or stash first."
  fi
}

require_tag_absent() {
  local tag="$1"
  if git tag --list | grep -qx "$tag"; then
    fail "tag $tag already exists locally"
  fi
  if git ls-remote --exit-code --tags origin "refs/tags/$tag" >/dev/null 2>&1; then
    fail "tag $tag already exists on origin"
  fi
}

open_release_pr() {
  local bump="$1"
  shift
  local pre_release=""

  if [[ "${1:-}" == "--pre-release" ]]; then
    shift
    pre_release="${1:?pre-release label required (e.g. alpha, beta)}"
  fi

  require_clean_tree

  git fetch --quiet --tags origin main
  if [[ "$(git branch --show-current)" != "main" ]]; then
    fail "run from main"
  fi
  if [[ "$(git rev-parse HEAD)" != "$(git rev-parse origin/main)" ]]; then
    fail "local main differs from origin/main. Pull first."
  fi

  local last_tag last_version major minor patch version
  last_tag=$(git describe --tags --abbrev=0 2>/dev/null || echo "v0.0.0")
  last_version="${last_tag#v}"
  IFS='.' read -r major minor patch <<< "${last_version%%-*}"

  case "$bump" in
    major) version="$((major + 1)).0.0" ;;
    minor) version="${major}.$((minor + 1)).0" ;;
    patch) version="${major}.${minor}.$((patch + 1))" ;;
    *)
      if [[ ! "$bump" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
        fail "'$bump' is not major/minor/patch or valid semver"
      fi
      version="$bump"
      ;;
  esac

  local full_version="$version"
  [[ -n "$pre_release" ]] && full_version="$version-$pre_release"
  local tag="v$full_version"
  local branch="release-$full_version"

  echo "Release: $last_tag → $tag"

  require_tag_absent "$tag"
  if git show-ref --verify --quiet "refs/heads/$branch"; then
    fail "branch $branch already exists locally"
  fi
  if git ls-remote --exit-code --heads origin "$branch" >/dev/null 2>&1; then
    fail "branch $branch already exists on origin"
  fi

  local unreleased
  unreleased=$(awk '/^## \[Unreleased\]/{flag=1; next} /^## \[/{flag=0} flag' CHANGELOG.md | grep -v '^$' || true)
  if [[ -z "$unreleased" ]]; then
    fail "[Unreleased] section in CHANGELOG.md is empty"
  fi

  trap rollback_release_pr EXIT
  git switch --quiet -c "$branch"
  ROLLBACK_BRANCH="$branch"

  local today
  today=$(date +%Y-%m-%d)
  sed -i "s/^## \[Unreleased\]/## [Unreleased]\n\n## [$version] - $today/" CHANGELOG.md

  git add CHANGELOG.md
  git commit --quiet -m "chore(release): $full_version"
  git push --quiet -u origin "$branch"
  # Set only after the push, so a rejected push never deletes a branch
  # that this run did not create.
  ROLLBACK_PUSHED=1

  BODY_FILE=$(mktemp)
  {
    echo "Stamps \`CHANGELOG.md\` for $tag."
    echo ""
    echo "After merge, run \`scripts/release.sh tag $full_version\`. The tag push starts the NuGet publish."
    echo ""
    echo "## Changelog"
    echo ""
    echo "$unreleased"
  } > "$BODY_FILE"

  gh pr create --base main --head "$branch" --title "chore(release): $full_version" --body-file "$BODY_FILE"

  # The PR exists, so a later failure must not roll back the branch.
  ROLLBACK_BRANCH=""
  git switch --quiet main

  echo ""
  echo "Merge the PR, then run: $0 tag $full_version"
}

tag_release() {
  local full_version="${1:-}"
  [[ -z "$full_version" ]] && usage

  local tag="v$full_version"
  local branch="release-$full_version"
  local version="${full_version%%-*}"

  require_tag_absent "$tag"

  local state merge_commit
  state=$(gh pr view "$branch" --json state --jq .state) || fail "no PR found for branch $branch"
  if [[ "$state" != "MERGED" ]]; then
    fail "PR for $branch is $state, not MERGED"
  fi
  merge_commit=$(gh pr view "$branch" --json mergeCommit --jq .mergeCommit.oid)

  git fetch --quiet --tags origin main
  if ! git merge-base --is-ancestor "$merge_commit" origin/main; then
    fail "merge commit $merge_commit is not on origin/main"
  fi
  if ! git show "$merge_commit:CHANGELOG.md" | grep -q "^## \[$version\]"; then
    fail "CHANGELOG.md at $merge_commit has no [$version] section"
  fi

  git tag "$tag" "$merge_commit"
  if ! git push --quiet origin "$tag"; then
    git tag -d "$tag" >/dev/null
    fail "push of $tag failed. Removed the local tag, so you can run this again."
  fi

  echo "Done. $tag on $merge_commit. Watch: gh run watch"
}

[[ $# -lt 1 ]] && usage

if [[ "$1" == "tag" ]]; then
  shift
  tag_release "$@"
else
  open_release_pr "$@"
fi
