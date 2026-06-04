# SWE-bench instruction & skill tuning loop — design

Goal: iteratively discover the **best agent instructions + skill set** that maximize the SWE-bench
Verified resolve rate, validated at increasing scale (10 → 100 → 500 → 1000 → 2000), without
overfitting to the small tiers and without runaway cost.

This builds on the existing harness (`src/Andy.Engine.SweBench`, `Andy.Benchmarks`). It assumes the
pluggable-agent seam (`--agent andy|external`, branch `feat/swebench-pluggable-agent`): the thing
being tuned is an **agent configuration**, and the harness scores it identically regardless of which
agent runs.

---

## 1. What we're optimizing (the "genome")

A *candidate* is a configuration the harness can run unchanged:

- **andy agent:** the system prompt (`SweSystemPrompt`) + token limits + tool set.
  - *Prerequisite:* `SweSystemPrompt` is currently hardcoded. Add `--system-prompt-file <path>`
    (and thread it through `SweAgentFactory`) so a candidate's instructions are a file. **This is
    the one small code change the loop needs to tune andy.**
- **external agent (opencode/aider):** `AGENTS.md` instructions + a **skills directory** + model.
  - "No skills vs with skills" is literally two candidates here: empty skills dir vs curated one.
  - Drive via `--agent external --agent-cmd "..."`; point the CLI at a per-candidate config dir.

A candidate is therefore: `{ instructions: text, skills: [skill...], model: id, limits: {...} }`.
Keep it serializable (one JSON/dir per candidate) so runs are reproducible and resumable.

## 2. Data tiers (fixed, gold-validated, split)

| Tier | Size | Purpose | Status today |
|------|------|---------|--------------|
| T10  | 10   | smoke / every iteration | `data/bakeoff10.subset` exists |
| T100 | ~99  | primary dev signal | `swebench_verified_first100.validated.subset` exists (django+astropy) |
| T500 | 500  | full Verified | **blocked** — needs ~10 more repos (parser + gold-validate each) |
| T1000/T2000 | — | full SWE-bench | further repos |

Two hard rules:

1. **Gold-gate every tier first** (`--stage grade --predictions-path gold --gold-survey`). Never
   trust agent deltas on a subset whose gold patches don't resolve ~100%. Adding astropy surfaced
   4 genuine grader bugs; new repos will too.
2. **Split each tier into dev / holdout** (e.g. 70/30). Tune on dev, *promote on holdout*. A config
   that wins on the set it was tuned on but loses on holdout is overfit — the central failure mode
   when the optimizer sees only 10 examples.

**The 500+ tiers are a separate workstream** (add repos), not part of the optimizer itself. The
optimizer is fully exercisable today on T10 + T100.

## 3. The loop

```
seed candidate(s)
repeat until budget/▽-plateau:
  1. EVALUATE  each live candidate on T10-dev  → resolve rate + per-instance pass/fail
               (existing harness: --stage all, one --run-id per candidate, --resume)
  2. ANALYZE   for each failing instance, collect the agent transcript + the gold patch;
               an LLM "critic" clusters failure modes (wrong file located, over-broad edit,
               gave up early, mis-read the test, ...) into actionable deficiencies.
  3. PROPOSE   an LLM "optimizer" reads {current instructions, skills, failure clusters} and
               emits N mutated candidates (reflexion/textgrad-style edits to the instructions,
               add/remove/rewrite a skill). Optionally crossover the best two.
  4. SELECT    keep top-K by T10-dev. Promote a candidate to T100-dev ONLY when it beats the
               incumbent on T10-*holdout* (guards against T10 overfit).
  5. GATE      re-score the current T100 leader on T100-holdout periodically; if dev↑ but
               holdout↓, stop promoting that lineage.
  leaderboard: persist every candidate + its scores at every tier it was evaluated on.
```

Scoring metric: resolve rate, tie-broken by (fewer empty patches, fewer turns, lower tokens).
Use **per-instance** results (the `logs/run_evaluation/*/report.json` files) so the critic sees
exactly which instances regressed/improved, not just the aggregate.

## 4. Optimizer strategies (compose as needed)

- **Reflexion (default):** critic → optimizer edits instructions to address the dominant failure
  cluster. Cheap, fast, usually the biggest early wins.
- **Evolutionary:** maintain a population; mutate (reword a rule, toggle a skill) + crossover
  (merge two winners' instruction sections). Better for exploring a wide skill space.
- **Judge panel for proposals:** generate several edits from different angles (precision-first,
  recall-first, "stop giving up" first), score on T10, keep the winner. Avoids single-shot bias.
- **Ablation:** periodically drop each skill/rule and re-score to find dead weight — keeps the
  instruction set lean (long prompts cost tokens and can dilute).

## 5. Overfitting & validity guards

- Dev/holdout split per tier (above); promote on holdout only.
- **Never let the optimizer see holdout transcripts** — holdout is evaluation-only.
- Watch the **dev↔holdout gap**; a widening gap = overfitting, freeze that lineage.
- Re-confirm the final winner on a **fresh, never-touched slice** before declaring a number.
- Log every cap/truncation (top-K, sampling) so "we covered it" never silently means "we sampled."

## 6. Cost & runtime model (be honest about this)

Wall-clock is dominated by **Docker grading**, not the LLM. Per iteration ≈
`candidates × tier_size × (agent_time + grade_time)`.

- T10 with a cheap model (mimo / deepseek-v4-flash): minutes, cents. Run every iteration.
- T100: ~1 hr/candidate, a few $ — run only for promoted candidates, batched + `--resume`.
- T500+: many GPU-hours of Docker + real $ per full pass — run the *final* 1–2 candidates only.

Controls: a token/time budget cap on the run (mirror the harness's fail-fast); cache eval images
(already reused across runs); parallelize grading across candidates; cheap model during search,
optionally confirm the winner once on a stronger model.

**Model policy:** default to cheap large-context models for the search (mimo-v2.5 $0.14/$0.28,
deepseek-v4-flash $0.098/$0.197, glm-4.7-flash). Never use a frontier/$$$ model without explicit
approval (it once drained OpenRouter credits). Small-context models (phi-4 @ 16K) are unusable —
the agent retains full history.

## 7. Orchestration

Implement the loop as a `Workflow` (deterministic control flow, fan-out over candidates, the LLM
critic/optimizer as subagents). Each EVALUATE is a harness invocation (`dotnet run ... --run-id
cand-<id>-t<tier>`); ANALYZE/PROPOSE are subagent calls over the per-instance artifacts. Persist
candidates + leaderboard under `swebench-tuning/`. Resume from the leaderboard after any crash.

## 8. Build order

1. `--system-prompt-file` for the andy agent (the only blocking code change to tune andy). *(small)*
2. Tuning driver: candidate model, leaderboard, dev/holdout split helper over a subset file. *(small)*
3. Reflexion loop on **T10** with mimo/deepseek-flash — prove the mechanism end-to-end. *(medium)*
4. Promotion to **T100** + holdout gating. *(medium)*
5. External-agent (opencode) candidates: AGENTS.md + skills dir → the no-skills-vs-skills study. *(medium)*
6. Add repos for **T500+** — separate, ongoing (parser + gold-validate per repo). *(large)*

Steps 1–4 are achievable now on existing data; step 6 is the long pole for scale.
