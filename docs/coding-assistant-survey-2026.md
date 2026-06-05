# Coding-assistant SOTA survey & andy-engine gap analysis (2026-06)

Source: deep-research workflow (107 agents, ~1.85M tokens, 595 web ops, 3-vote adversarial
verification per claim). Summary verdict: **andy-engine lags 2025/2026 SOTA on standards files,
harness architecture, localization, and test-based verification / best-of-N.**

## Best practice per dimension (high-confidence, verified)

**1–2. Instruction files & skills.**
- **`AGENTS.md` is the emerging cross-tool standard** — backed by the Linux Foundation "Agentic AI
  Foundation", adopted by Cursor, GitHub Copilot, OpenAI Codex, Devin, VS Code (~60k repos,
  self-reported). `CLAUDE.md` is the hierarchical sibling (global + project + nested).
- **Skills** package reusable instructions/tools as `SKILL.md` on the "Agent Skills" standard, with
  **lazy disclosure** (load the skill body only when relevant) and hierarchical loading.

**3. Harness architecture.** Adaptive context compaction, retrieval, and cross-session memory;
distinct edit toolings (edit vs apply-patch vs morph/fast-apply); **in-loop test/shell execution**;
sub-agent orchestration; prompt caching of a stable prefix.

**4. Prompting / localization.** **Hierarchical code localization** (LocAgent ~92.7 Acc@5 on Lite);
**best-of-N with test-based selection** (Agentless); in-loop test verification; minimal-diff
discipline; root-cause-over-symptom.

**5. SWE-bench Verified leaderboard.** Agentless pipelines (Agentless ~50.8 Verified) and agentic
systems with **multi-LLM ensembling** (TRAE ~75.2, Devlo ~70.2). 2025 SOTA ~75; 2026 climbing >88.

## Where andy-engine is behind

| Dimension | SOTA | andy-engine today | Gap |
|---|---|---|---|
| Instruction files | AGENTS.md standard, hierarchical | nascent `--rules-dir` (just added) | medium |
| Skills | SKILL.md, lazy disclosure | none | medium |
| Test execution | in-loop, agent runs tests & iterates | **files-only, no test execution** | **large** |
| Localization | hierarchical localize stage | none (search-as-you-go) | large |
| Best-of-N | N candidates, test-selected | single attempt | large |
| Context mgmt | adaptive compaction + retrieval | full turn history, basic compaction | medium |
| Prompt caching | stable-prefix caching | **already does this** ✓ | none |
| Models | multi-LLM ensembling | one cheap model/instance | medium (cost-bounded) |

## Prioritized upgrades (highest impact first)

1. **In-loop test execution — HIGHEST impact, LOWEST cost.** andy *already* has `DockerGrader` +
   `TestSpecBuilder`; expose them as an agent tool so the agent can run the failing test, see the
   result, and iterate. This is the single biggest lever and it reuses existing infrastructure.
2. **Hierarchical localization stage** — find the right file/function before editing (directly
   targets andy's observed "wrong file / scattered edit" failures).
3. **Best-of-N with test-based selection** — generate N candidates with the *cheap* model and pick
   the one that passes tests (cheap-model self-ensembling, respects the no-frontier-cost rule).
4. **AGENTS.md + skills support** — extend `--rules-dir` toward the AGENTS.md standard; add a
   lazy-loaded skills mechanism.
5. **Context compaction + retrieval** — for large repos / long sessions.
6. *(optional, lower priority)* multi-model ensembling / self-improvement — cost-rule-constrained.

## Adversarially refuted (did NOT survive verification — do not treat as fact)

- "andy uses 9-pass fuzzy-match editing" — refuted 0/3 (research hallucinated a technique).
- "andy has a Planner subagent with schema-level write-blocking" — refuted 0/3.
- "top SWE-bench Verified entry is 78.80%" — refuted 1/3 (inflated/unsupported).

## Caveats

- **andy's 63–65/99 is a curated django+astropy subset — NOT comparable to full SWE-bench Verified**
  (500 instances). Don't quote it against leaderboard numbers.
- 2025 SOTA ~75 (TRAE); 2026 >88. The "60k AGENTS.md adopters" figure is self-reported. LocAgent 92.7
  is Acc@5 on Lite. Self-improvement deltas (17–53) are subset-specific.
- Frontier ensembling conflicts with the cost rule → adapt to **cheap-model self-ensembling**.

## Key sources

- AGENTS.md / Agentic AI Foundation (Linux Foundation press); agents.md
- Claude Code Skills docs (code.claude.com/docs/en/skills)
- Agentless (github.com/OpenAutoCoder/Agentless); LocAgent (arxiv 2503.09089)
- SWE-bench survey/leaderboard (arxiv 2506.17208, 2603.00520; awesomeagents.ai leaderboard)
- Harness deep-dives (Codex/Claude Code architecture; sourcegraph.com/blog/agentic-coding)
- OpenAI "why we no longer evaluate SWE-bench Verified" (contrarian/limitations)

## Open questions

- andy's resolve rate on *full* Verified vs the 99-subset?
- Ceiling of cheap models *with* a full localize + test + best-of-N pipeline?
- Can `DockerGrader`/`TestSpecBuilder` be exposed in-loop without a major `SimpleAgent` change?
- Are AGENTS.md loading semantics + the Agent Skills spec stable enough to implement in .NET?
