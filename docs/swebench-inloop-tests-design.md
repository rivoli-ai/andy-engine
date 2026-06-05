# In-loop test execution for the andy SWE-bench agent — design & estimate

Goal: let the andy agent **run tests against its own edits and iterate**, the survey's #1
highest-impact upgrade. andy is currently files-only (no test execution) — the structural gap that
both the SOTA survey and the opencode experiment identified.

## Reuse point

The execution primitive already exists and is exactly what we need:

```
IDockerClient.RunAsync(DockerRunSpec { Image, Script, Timeout }) -> { Stdout, Stderr, ExitCode, TimedOut }
```

`DockerGrader` uses it to run the *eval* script (apply model_patch + test_patch, run FAIL_TO_PASS).
The in-loop tool reuses the same primitive with a **different script**: apply the agent's *current*
edits and run an *agent-chosen* test command. The official image already has the repo at base_commit
with the environment installed, so arbitrary tests run as-is.

## The leakage question — and why it's safe by construction

The central worry with in-loop tests is the agent cheating by running the hidden FAIL_TO_PASS tests.
**It can't:** those tests are *added by `test_patch`*, and the in-loop script never injects
`test_patch`. So the bug-specific gold tests physically do not exist in the container. The agent can
only run:
- **existing repo tests** (regression signal — legitimate, a real dev does this), and
- **a reproduction test it writes itself** from the problem statement (the SWE-agent / Agentless
  pattern — legitimate, no leakage).

This means no oracle access and no special guardrail beyond "never inject test_patch."

## Components

1. **`SweTestRunner`** (new) — given `(instance, currentWorkingTreeDiff, testCommand)`, build a script
   that: `cd` repo → `git apply` the diff (fail clearly if it doesn't apply) → run `testCommand`
   between START/END markers → call `IDockerClient.RunAsync`. Returns truncated output + pass/fail.
   Refactor `TestSpecBuilder` to share the image-tag + apply-patch preamble (no `test_patch`, no
   FAIL_TO_PASS). *(~0.5–1 day)*
2. **`run_tests` tool** exposed to the agent — captures the current working-tree diff (reuse the
   `GetModelPatchAsync` git-diff logic, minus the test-exclusions), validates the requested test
   target, calls `SweTestRunner`, returns the result. Enforces a **per-instance invocation cap** and
   a per-call timeout. *(~1 day)*
3. **Wire into `SweAgentFactory`** — register `run_tests` as a custom tool for this agent only
   (the factory currently sets `ProcessExecution=false`; this tool is *not* a general shell — it only
   runs a test command inside the ephemeral, network-less container, never on the host). *(~0.5 day)*
4. **Prompt v2** — instruct the workflow: write a reproduction test from the problem statement, run it
   to confirm it fails, make the fix, run it to confirm it passes, run nearby existing tests for
   regressions. Builds on tuned-v1's root-cause/focus rules. *(quick)*
5. **Cost controls + the repro-test wrinkle** — the agent's repro test must NOT pollute the final
   `model_patch`. Require repro tests at a `_swebench_repro_*` path and add that to
   `SweWorkspaceManager.ArtifactExcludes` so it's stripped from the captured patch. Cap test
   invocations/instance; truncate output. *(~0.5 day)*
6. **Tests + smoke** — unit tests with a fake `IDockerClient` (apply-fails, pass, fail, cap reached,
   leakage check that test_patch is never in the script); one live smoke instance. *(~1 day)*

## Effort & cost

- **Dev: ~4–5 days.** Mostly the runner + tool + the patch-capture/exclusion plumbing.
- **Run cost goes UP:** every `run_tests` call starts a container (image cached, but seconds–minutes
  each) × several calls/instance × instances. Much heavier than files-only. The per-instance cap and
  `--max-parallel` (now merged) keep it bounded; budget for slower runs.

## Expected impact

Per the survey this is the **largest single lever** (top systems all run tests in-loop). It directly
attacks andy's observed failure modes (wrong fix / can't tell if the edit worked) that prompting
alone can't fix — consistent with rule-tuning topping out at +4/26. Realistic hope: a meaningful jump
on the hard set, well beyond the prompt-only ceiling. Pairs naturally with **best-of-N** later
(generate N fixes, pick the one whose repro test passes) — cheap-model self-ensembling.

## Key decision for sign-off

**Which test modes to enable in v1?**
- (A) Reproduction test only — purest signal, depends on the model writing a good repro.
- (B) Existing repo tests only — simpler, regression-focused, weaker on the actual bug.
- (C) Both (recommended) — repro test for the bug + existing tests for regressions.

## Open risks

- Cheap models may write weak repro tests (the value depends on it) — measure on the 26 first.
- Container-per-call latency; mitigate with the invocation cap + parallelism.
- `git apply` of an in-progress diff onto the image's repo copy must match paths exactly (the grader
  already handles this for the final patch; reuse that logic).
