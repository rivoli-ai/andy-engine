#!/usr/bin/env bash
#
# setup-git-hooks.sh — install the repository's local pre-commit / commit-msg hooks.
#
# Installs:
#   * commit-msg  — rejects prohibited assistant attribution (scripts/check-attribution.sh).
#   * pre-commit  — verifies whitespace/style formatting of STAGED .cs files (fast, scoped).
#
# Run once after cloning: ./scripts/setup-git-hooks.sh
# Re-run any time to refresh the hooks.

set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
hooks_dir="$(git rev-parse --git-path hooks)"
mkdir -p "$hooks_dir"

install_hook() {
  local name="$1" body="$2"
  local target="$hooks_dir/$name"
  printf '%s\n' "$body" > "$target"
  chmod +x "$target"
  echo "installed $name hook -> $target"
}

install_hook "commit-msg" '#!/usr/bin/env bash
set -euo pipefail
repo_root="$(git rev-parse --show-toplevel)"
exec "$repo_root/scripts/check-attribution.sh" --message-file "$1"
'

install_hook "pre-commit" '#!/usr/bin/env bash
set -euo pipefail
repo_root="$(git rev-parse --show-toplevel)"

# Only the staged C# files, so the check stays fast.
mapfile -t files < <(git diff --cached --name-only --diff-filter=ACM -- "*.cs")
[ "${#files[@]}" -eq 0 ] && exit 0

if ! command -v dotnet >/dev/null 2>&1; then
  echo "pre-commit: dotnet not found; skipping format check." >&2
  exit 0
fi

sln="$repo_root/Andy.Engine.sln"
fail=0
for sub in whitespace style; do
  if ! dotnet format "$sub" "$sln" --verify-no-changes --include "${files[@]}" >/dev/null 2>&1; then
    fail=1
  fi
done

if [ "$fail" -ne 0 ]; then
  echo "pre-commit: formatting issues in staged files. Fix with:" >&2
  echo "  dotnet format Andy.Engine.sln --include ${files[*]}" >&2
  exit 1
fi
'

echo "done. Hooks live in $hooks_dir (per-clone, not committed)."
