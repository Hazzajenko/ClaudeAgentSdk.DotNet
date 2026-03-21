#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: $0 <major|minor|patch|X.Y.Z> [--pre-release <label>]"
  echo ""
  echo "Examples:"
  echo "  $0 patch                      # v0.1.0 → v0.1.1"
  echo "  $0 minor                      # v0.1.0 → v0.2.0"
  echo "  $0 major                      # v0.1.0 → v1.0.0"
  echo "  $0 minor --pre-release alpha  # v0.1.0 → v0.2.0-alpha"
  echo "  $0 0.3.0                      # explicit version"
  exit 1
}

[[ $# -lt 1 ]] && usage

BUMP="${1}"
shift
PRE_RELEASE=""

if [[ "${1:-}" == "--pre-release" ]]; then
  shift
  PRE_RELEASE="${1:?pre-release label required (e.g. alpha, beta)}"
fi

# Get last version from git tags
LAST_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "v0.0.0")
LAST_VERSION="${LAST_TAG#v}"
IFS='.' read -r MAJOR MINOR PATCH <<< "${LAST_VERSION%%-*}"

# Calculate new version
case "$BUMP" in
  major) VERSION="$((MAJOR + 1)).0.0" ;;
  minor) VERSION="${MAJOR}.$((MINOR + 1)).0" ;;
  patch) VERSION="${MAJOR}.${MINOR}.$((PATCH + 1))" ;;
  *)
    if [[ ! "$BUMP" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
      echo "Error: '$BUMP' is not major/minor/patch or valid semver"
      exit 1
    fi
    VERSION="$BUMP"
    ;;
esac

FULL_VERSION="$VERSION"
[[ -n "$PRE_RELEASE" ]] && FULL_VERSION="$VERSION-$PRE_RELEASE"
TAG="v$FULL_VERSION"

echo "Release: $LAST_TAG → $TAG"

# Validate
if ! git diff --quiet || ! git diff --cached --quiet; then
  echo "Error: uncommitted changes — commit or stash first"
  exit 1
fi

if git tag --list | grep -qx "$TAG"; then
  echo "Error: tag $TAG already exists"
  exit 1
fi

# Check [Unreleased] has content
UNRELEASED=$(awk '/^## \[Unreleased\]/{flag=1; next} /^## \[/{flag=0} flag' CHANGELOG.md | grep -v '^$' || true)
if [[ -z "$UNRELEASED" ]]; then
  echo "Error: [Unreleased] section in CHANGELOG.md is empty"
  exit 1
fi

# Finalize changelog: [Unreleased] → [X.Y.Z] - date
TODAY=$(date +%Y-%m-%d)
sed -i "s/^## \[Unreleased\]/## [Unreleased]\n\n## [$VERSION] - $TODAY/" CHANGELOG.md

git add CHANGELOG.md
git commit -m "chore(release): $FULL_VERSION"
git tag "$TAG"

echo ""
echo "Pushing to origin..."
git push origin main "$TAG"

echo "Done — $TAG released. Watch: gh run watch"
