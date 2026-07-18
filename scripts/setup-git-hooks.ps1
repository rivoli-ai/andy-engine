# setup-git-hooks.ps1 — install the repository's local git hooks on Windows.
#
# The hook logic lives in the shell scripts (check-attribution.sh, and the inline format check);
# on Windows they run via the Git-for-Windows bundled bash, so there is a single source of truth.
#
# Run once after cloning:  ./scripts/setup-git-hooks.ps1

$ErrorActionPreference = "Stop"

$hooksDir = (git rev-parse --git-path hooks)
New-Item -ItemType Directory -Force -Path $hooksDir | Out-Null

# commit-msg: reject prohibited assistant attribution.
@'
#!/usr/bin/env bash
set -euo pipefail
repo_root="$(git rev-parse --show-toplevel)"
exec "$repo_root/scripts/check-attribution.sh" --message-file "$1"
'@ | Set-Content -NoNewline -Path (Join-Path $hooksDir "commit-msg")

# pre-commit: verify whitespace/style of staged .cs files.
@'
#!/usr/bin/env bash
set -euo pipefail
repo_root="$(git rev-parse --show-toplevel)"
mapfile -t files < <(git diff --cached --name-only --diff-filter=ACM -- "*.cs")
[ "${#files[@]}" -eq 0 ] && exit 0
command -v dotnet >/dev/null 2>&1 || { echo "pre-commit: dotnet not found; skipping." >&2; exit 0; }
sln="$repo_root/Andy.Engine.sln"
fail=0
for sub in whitespace style; do
  dotnet format "$sub" "$sln" --verify-no-changes --include "${files[@]}" >/dev/null 2>&1 || fail=1
done
if [ "$fail" -ne 0 ]; then
  echo "pre-commit: formatting issues in staged files. Fix with: dotnet format Andy.Engine.sln --include ${files[*]}" >&2
  exit 1
fi
'@ | Set-Content -NoNewline -Path (Join-Path $hooksDir "pre-commit")

Write-Host "Installed commit-msg and pre-commit hooks in $hooksDir"
