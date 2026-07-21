#!/usr/bin/env bash
#
# check-attribution.sh — fail if commit/PR metadata contains prohibited assistant attribution.
#
# The repository policy (agents.md) forbids mentioning Claude Code, Anthropic, or any other code
# assistant in commit messages, issue descriptions, PRs, or merges. This script enforces that for
# commit messages (locally via the commit-msg hook) and for a commit range or arbitrary text
# (in CI).
#
# Usage:
#   check-attribution.sh --message-file <path>     # a single commit message (commit-msg hook)
#   check-attribution.sh --range <base>..<head>    # every commit message in a range (CI)
#   check-attribution.sh --text "<string>"         # an arbitrary string (PR title/body)
#   printf '%s' "<text>" | check-attribution.sh --stdin
#
# Exit 0 = clean, 1 = prohibited attribution found, 2 = usage error.

set -euo pipefail

# Attribution-shaped patterns (case-insensitive). Deliberately narrow so legitimate mentions like
# "add Anthropic provider support" do NOT trip: we match sign-offs and "generated/written by …"
# phrasings, not every occurrence of a vendor name.
PATTERNS=(
  'co-authored-by:.*(claude|anthropic|copilot|cursor|codeium|\[bot\]|noreply@anthropic)'
  '(generated|written|created|authored|assisted)[[:space:]]+(with|by)[[:space:]]+.*(claude|anthropic|copilot|chatgpt|gpt-[0-9]|cursor|codeium|ai assistant|code assistant)'
  '🤖[[:space:]]*generated'
  'claude code'
  'noreply@anthropic\.com'
)

scan() {
  # $1 = human label for messages, reads text on stdin.
  local label="$1"
  local text
  text="$(cat)"
  local found=0
  local pat
  for pat in "${PATTERNS[@]}"; do
    local hits
    if hits="$(printf '%s\n' "$text" | grep -inE "$pat" || true)"; then
      if [ -n "$hits" ]; then
        if [ "$found" -eq 0 ]; then
          echo "ERROR: prohibited assistant attribution in $label:" >&2
          found=1
        fi
        printf '%s\n' "$hits" >&2
      fi
    fi
  done
  return "$found"
}

main() {
  [ $# -ge 1 ] || { echo "usage: check-attribution.sh --message-file|--range|--text|--stdin ..." >&2; exit 2; }
  local mode="$1"; shift
  case "$mode" in
    --message-file)
      [ $# -ge 1 ] || { echo "--message-file requires a path" >&2; exit 2; }
      scan "commit message ($1)" < "$1"
      ;;
    --range)
      [ $# -ge 1 ] || { echo "--range requires <base>..<head>" >&2; exit 2; }
      # One commit at a time so the label points at the offender.
      local rc=0 sha
      while IFS= read -r sha; do
        [ -n "$sha" ] || continue
        if ! git log -1 --format='%B' "$sha" | scan "commit $sha ($(git log -1 --format='%s' "$sha"))"; then
          rc=1
        fi
      done < <(git rev-list "$1")
      return "$rc"
      ;;
    --text)
      [ $# -ge 1 ] || { echo "--text requires a string" >&2; exit 2; }
      printf '%s' "$1" | scan "text"
      ;;
    --stdin)
      scan "input"
      ;;
    *)
      echo "unknown mode: $mode" >&2; exit 2 ;;
  esac
}

if main "$@"; then
  exit 0
else
  echo "" >&2
  echo "Attribution to code assistants is not allowed in commit/PR metadata (see agents.md)." >&2
  exit 1
fi
