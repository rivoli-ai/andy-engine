# Andy.Engine.SweBench

A .NET harness for evaluating the Andy agent on [SWE-bench Verified](https://www.swebench.com/).
It reuses the **official prebuilt evaluation Docker images** (`swebench/sweb.eval.x86_64.<id>`),
so grading matches the upstream harness without rebuilding environments.

Library: `src/Andy.Engine.SweBench`. CLI + tests: `Andy.Benchmarks`
(run via `dotnet run --project Andy.Benchmarks -- ...`).

## Pipeline

```
dataset (.jsonl)
  → [agent stage]  SweInstanceRunner → SweAgentFactory → SimpleAgent (edits files only)
                   → GetModelPatchAsync  → predictions.jsonl
  → [grade stage]  DockerGrader: pull image, apply model patch + test patch, run test_cmd,
                   parse log → status map → resolved? → report.json
```

- **Agent stage** (`Agent/`): clones the repo at `base_commit`, runs `SimpleAgent` on the problem
  statement with file/search/edit tools (no shell), captures the working-tree diff as the model patch.
- **Grade stage** (`Grading/`): builds an eval script (`TestSpecBuilder`) mirroring swebench's
  `make_eval_script`, runs it in the instance image (`DockerGrader`), and parses the test log
  (`LogParsers/`) into a pass/fail map that `GradingEngine` compares against `FAIL_TO_PASS` /
  `PASS_TO_PASS`.

## The gold gate (correctness contract)

**Always gold-validate a new subset before trusting agent numbers.** Gold mode applies the dataset's
*gold patch* (the official fix) and must resolve ~100% — if it doesn't, our grader/Docker setup
doesn't match upstream.

```bash
# Validate: every gold patch must resolve.
dotnet run --project Andy.Benchmarks -- --dataset data/<subset>.jsonl \
  --stage grade --predictions-path gold

# Triage a large/new subset without aborting on the first failure:
dotnet run --project Andy.Benchmarks -- --dataset data/<subset>.jsonl \
  --stage grade --predictions-path gold --gold-survey
```

Default behavior aborts on the first non-resolving gold instance (fail-fast). `--gold-survey`
grades all and reports the full breakdown — use it to separate genuine grader bugs from upstream
dataset/image drift.

## Adding a repo

The first 100 Verified cases span `django/django` (supported) and `astropy/astropy`. To add another:

1. **`Grading/Constants/RepoTestSpecs.cs`** — add a `RepoTestSpec` per version (`TestCmd`, `Install`,
   `EvalCommands`). Port exactly from swebench `constants/python.py`
   (`MAP_REPO_VERSION_TO_SPECS[<repo>]`).
2. **`Grading/LogParsers/`** — add/register the parser swebench maps the repo to
   (`MAP_REPO_TO_PARSER`). pytest repos use `PytestLogParser` (a port of `parse_log_pytest_v2`).
3. **Gold-validate** the new instances. Fix until ~100%.

### Gotchas (learned the hard way)

- **`test_cmd` must match the prebuilt images, not just the current constants.** swebench's
  `constants/python.py` defines `TEST_PYTEST` twice; the effective bare `"pytest -rA"` does **not**
  match the published astropy images. The fuller `pytest --no-header -rA --tb=no -p no:cacheprovider`
  is required — without `--no-header`, astropy's pytest-header plugin escalates the nose-style
  `setup(self)` deprecation to an *error*, failing whole test classes.
- **pytest output has two shapes.** `parse_log_pytest_v2` (→ `PytestLogParser`) handles both
  status-first (`PASSED <id>`, the `-rA` summary) and status-last (`<id> PASSED`, older pytest's
  verbose progress, e.g. astropy 1.x on pytest 3.3.1).
- **Colourised output.** Some tools emit ANSI colour codes even without a TTY (astropy 5.1).
  `DockerGrader.StripAnsi` normalises the log before parsing.
- **Test directives on renames.** Use the `b/` target path from `diff --git a/X b/Y`
  (`DiffUtil.GetDiffTargetFiles`), not the deleted `a/` source — a renamed test file
  (`py3_test_x.py → test_x.py`) must resolve to the file that exists when tests run.
- **Model patch hygiene.** Capture excludes `__pycache__`, `*.pyc`, `*.orig`, `*.rej`, and backup
  copies (`*.bak`, `*.backup*`) that some models write before editing.
- **Dataset/image drift exists.** A few instances record test ids that the published image no
  longer emits (e.g. astropy-7606: `test_compose_roundtrip[]` vs the image's `[unit0..]`). These
  fail gold even upstream; exclude + document them (see
  `data/swebench_verified_first100.validated.subset`).

## Running the agent

```bash
export OPENROUTER_API_KEY=...   # native OpenRouter provider via Andy.Llm 2026.5.29-rc.31
dotnet run --project Andy.Benchmarks -- \
  --dataset data/<subset>.jsonl --stage all \
  --model xiaomi/mimo-v2.5 \
  --subset-file data/<validated>.subset \
  --resume                       # checkpoints to predictions.jsonl; safe to re-run
```

### Swapping the agent (`--agent`)

The harness is agent-agnostic: it captures the patch by diffing the workspace, so the agent only
has to edit files. Two implementations are selectable at the `SweInstanceRunner` seam:

- `--agent andy` (default): the in-process `SimpleAgent` (reaches OpenRouter directly, needs
  `OPENROUTER_API_KEY`).
- `--agent external --agent-cmd "<template>"`: any out-of-process CLI agent (opencode, aider, ...).
  The template is whitespace-tokenized; whole-token placeholders are substituted (never
  shell-interpolated, so no injection): `{model}`, `{workspace}`, `{prompt}` (statement as one
  argv element), `{prompt_file}` (path to a temp file holding the statement). With no
  `{prompt}`/`{prompt_file}` token, the statement is piped to stdin. The process runs with its
  working directory set to the instance workspace; `--agent-timeout-seconds` bounds it. The
  external agent carries its own auth, so the OpenRouter key is not required.

```bash
# Compare opencode against andy on the same gold-validated subset, same grader, same report:
dotnet run --project Andy.Benchmarks -- \
  --dataset data/<subset>.jsonl --stage all --run-id opencode-baseline \
  --agent external --agent-cmd "opencode run --model {model} {prompt}" \
  --model xiaomi/mimo-v2.5 --subset-file data/<validated>.subset --resume
```

Both arms grade identically, which is what makes a no-skills-vs-skills (or andy-vs-opencode)
comparison meaningful. (Verify the exact opencode subcommand/flags against your installed version.)

### Agent Skills (`--skills-dir`) — the no-skills-vs-skills study

`--skills-dir` is the **with-skills arm** for the in-process `andy` agent. It gives the agent
[Agent Skills](https://www.anthropic.com/news/skills) via lazy disclosure, so you can measure
whether skills move resolution rate against an identical no-skills baseline (same model, subset,
grader, and report).

**Skills directory layout.** The directory is a *root* of skills; each skill is its own
subdirectory with a `SKILL.md` manifest (YAML frontmatter + markdown body), plus any resources it
references:

```
skills/
  pdf-forms/
    SKILL.md            # frontmatter: name, description (required); body = full instructions
    scripts/fill.py     # optional package resources the skill's body points at
    references/api.md
  react-review/
    SKILL.md
```

**What the flag does.** For each instance the andy agent gains two tools and a prompt block:

- `skill` — loads a skill's full `SKILL.md` body on demand.
- `skill_file` — read-only access to a skill's OWN package resources (`scripts/`, `references/`,
  …). These live outside the workspace-scoped file permissions, so this tool is the sanctioned
  bridge; access is confined to the named skill's directory (no `..`, symlink, or absolute-path
  escape; no write).
- A **lazy-disclosure** summary is appended to the system prompt: one line per skill, **name and
  description only** — never a `SKILL.md` path (paths would only provoke denied `read_file` calls).
  The agent calls `skill` by name when a skill looks relevant.

**Validation & diagnostics (fail-fast).** The catalog is validated and scanned **once** at run
start, before any instance is cloned:

- A missing `--skills-dir`, or a directory with **zero usable skills**, aborts the run with a clear
  error — the with-skills arm can never silently collapse into the baseline.
- Discovery diagnostics (skipped/unreadable/duplicate/malformed manifests, lint warnings) are
  surfaced: fatal ones in the error, non-fatal ones on stderr (prefixed `[skills]`).

**Reproducible study — run both arms:**

```bash
# Baseline (no skills):
dotnet run --project Andy.Benchmarks -- \
  --dataset data/<subset>.jsonl --stage all --run-id andy-noskills \
  --model xiaomi/mimo-v2.5 --subset-file data/<validated>.subset --resume

# With skills (same everything, plus --skills-dir):
dotnet run --project Andy.Benchmarks -- \
  --dataset data/<subset>.jsonl --stage all --run-id andy-skills \
  --model xiaomi/mimo-v2.5 --subset-file data/<validated>.subset \
  --skills-dir ./skills --resume
```

Compare the two `report.json` resolution rates. `--skills-dir` applies only to `--agent andy`; an
external CLI agent brings its own skills mechanism.

Resilience / safety knobs:

- `--agent-timeout-seconds <n>` (default 1800): per-instance wall-clock cap. A runaway agent is
  cancelled and recorded as a timed-out (empty) prediction; the run continues. Relies on
  `SimpleAgent` propagating cancellation.
- **Token limits default GENEROUS for large-context models** (the norm for capable coding agents).
  A re-baseline showed tight limits REGRESS such models — compaction + output truncation hide
  information and burn turns, producing empty patches. Defaults: `--max-turns 50`,
  `--max-output-tokens 32768`, `--max-context-tokens 1000000`, `--max-tool-result-chars 100000`.
  **Tighten these for token-constrained / small-context models** to save cost. (mimo-v2.5 went
  from a regression under tight 200k/8k/40 limits back to baseline once the limits were relaxed.)
- Malformed/empty LLM responses (JSON parse failures) and HTTP 429/5xx are retried with backoff
  (`RateLimitingLlmProvider`).
- Run in batches with `--resume` for large sets; a single long process is more fragile.

## Notes

- Instance id → image: `sweb.eval.x86_64.<id>` with `__` → `_1776_`.
- `OPENROUTER_API_KEY` must be set for the agent stage (grading needs only Docker).
- Outputs land in `swebench-runs/<run-id>/` (`predictions.jsonl`, `report.json`, per-instance logs).
  These are gitignored — copy them out if you need to preserve a baseline.

## Status

### 2026-07-18 — Agent Skills productionized for the andy agent

- `--skills-dir` wires `skill` + read-only `skill_file` tools into the in-process andy agent, with
  lazy-disclosure prompt summaries (names/descriptions only, no host paths).
- The skills catalog is validated and scanned once at run start; an invalid or empty `--skills-dir`
  fails fast so the with-skills arm cannot silently collapse into the baseline. Discovery
  diagnostics are surfaced (error for fatal, stderr for non-fatal).
- Registration and skill-body loading are covered by credential-free tests (no API key, Docker, or
  network) that run in CI; `skill_file` traversal/symlink/absolute-path denials are tested too.
- Documented CLI defaults reconciled with `SweBenchCliOptions` (`--max-output-tokens` default is
  32768).
- Remaining: gather no-skills-vs-skills numbers on a validated subset once a skills corpus exists.
