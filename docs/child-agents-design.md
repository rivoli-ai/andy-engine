# Bounded child-agent execution (issue #34)

Design for safe subagent delegation in `SimpleAgent`, consumed by rivoli-ai/andy-cli#210.

## Goals

- One engine-owned contract for delegating work to child agents, so clients (TUI, headless,
  ACP) don't reimplement safety-critical orchestration with incompatible semantics.
- A child can never widen anything beyond its parent: workspace, tools, provider policy,
  turn/time budgets.
- Deterministic, fully-reported outcomes: results ordered by task index, explicit
  partial-failure and cancellation states, no silent drops.

## Non-goals

- Recursive delegation policy (a child spawning grandchildren) — children get plain
  `SimpleAgent`s; nothing hands them the coordinator. Recursion can be layered later.
- Cross-child communication or shared memory. Children are isolated by design.
- Process-level sandboxing — enforcement is at the engine boundary (registry/executor/
  provider wrappers), the same trust level as the rest of Andy.Tools.

## Public surface

All new API lives in `src/Andy.Engine/ChildAgents.cs` plus one method on `SimpleAgent`:

```csharp
public Task<ChildTaskRunReport> RunChildTasksAsync(
    IReadOnlyList<ChildTask> tasks,
    ChildRunOptions? options = null,
    Action<ChildAgentEvent>? onEvent = null,
    CancellationToken cancellationToken = default);
```

The method lives on the parent agent deliberately: the parent's own dependencies *are* the
ceilings. Its tool registry bounds child tools, its working directory bounds child
workspaces, its LLM provider is the default child provider, its `maxTurns` is the default
per-child turn ceiling, and its context/output/compression settings are inherited.

### ChildTask — the child task contract

| Field | Meaning | Ceiling rule |
|---|---|---|
| `Name` | Stable identifier for events/results (defaults to `child-{index}`) | must be unique in the batch |
| `Objective` (required) | The user message the child processes | non-empty |
| `RoleInstructions` | The child's system prompt (falls back to a generic delegate prompt) | — |
| `Workspace` | Relative subpath of the parent working directory | must resolve inside the parent working directory; absolute paths and `..` escapes rejected |
| `AllowedTools` | Tool-id subset visible to the child; `null` = inherit all parent tools | every id must exist in the parent registry |
| `ProviderName` | Key into `ChildRunOptions.ChildProviders` | must be a parent-supplied key; children cannot name arbitrary providers |
| `MaxTurns` | Per-child turn budget | ≤ `ChildRunOptions.MaxTurnsPerChild` |
| `MaxDuration` | Per-child wall-clock budget | ≤ `ChildRunOptions.MaxDurationPerChild` |

### ChildRunOptions — parent ceilings

`MaxConcurrency` (default 1 — sequential unless the parent opts into parallelism),
`MaxTurnsPerChild` (default: parent `maxTurns`), `MaxDurationPerChild`,
`MaxTotalTurns` (aggregate LLM-call budget across the whole batch),
`MaxTotalDuration` (aggregate deadline), `ChildProviders` (the parent-defined provider
policy: name → `ILlmProvider`; this dictionary is the *only* provider surface children can
reference).

### Validation is all-or-nothing

The entire batch is validated before any child starts. Any widening attempt — unknown tool,
workspace escape, unknown provider name, per-child budget above its ceiling, duplicate
names — throws `ArgumentException` and **nothing runs**. Runtime enforcement (below) is the
second line of defense, not the first.

## Enforcement

- **Tools** — each child gets a `ChildToolRegistry` (read-only filtered view of the parent
  registry; mutators throw `NotSupportedException`) and a `ChildToolExecutor` wrapper whose
  `ExecuteAsync` returns a failed `ToolExecutionResult` for any tool outside the allow-set.
  The registry filter keeps disallowed tools out of the LLM request; the executor guard
  covers a model that hallucinates a tool name anyway (surfaced to the child as a normal
  tool error — this is the "permission denial" path, and the child can continue).
- **Workspace** — child working directory = `Path.GetFullPath(parentDir/Workspace)`,
  validated inside the parent directory (separator-suffixed prefix check, so `/repo-evil`
  does not pass as inside `/repo`). Created if missing.
- **Aggregate turn budget** — a shared `ChildTurnBudget` (single `Interlocked`-decremented
  counter) wrapped around every child's provider (`BudgetedLlmProvider`). Every
  `CompleteAsync`/`StreamCompleteAsync` acquires one turn *before* dispatch; exhaustion
  throws, the child's own error path (issue #37 semantics) turns that into a failed
  `SimpleAgentResult`, and the coordinator maps it to `ChildTaskStatus.BudgetExceeded`.
  Interlocked acquisition makes the aggregate limit race-safe under `MaxConcurrency > 1`.
  The wrapper is applied even without an aggregate budget: it counts the calls each child
  actually dispatched, so `TurnCount`/`TotalTurns` are exact (denied calls excluded,
  cancelled children included) rather than inferred from `SimpleAgentResult`.
- **Time** — one linked CTS per batch with `CancelAfter(MaxTotalDuration)`, plus one linked
  CTS per child with `CancelAfter(MaxDuration)`. A child cancelled by its own deadline
  reports `BudgetExceeded`; a child cancelled by the batch deadline or the parent token
  reports `Cancelled`. `BudgetExceeded` is claimed only when the child HAS a deadline and
  its token actually fired — a spurious `OperationCanceledException` (e.g. an HttpClient
  timeout inside a provider, which cancels no token) is attributed `Failed`, never a
  phantom budget overrun.
- **Cancellation tree** — parent token → batch CTS → per-child CTS. `RunChildTasksAsync`
  awaits every started child before returning, so no child work continues after it returns;
  children never started by the time of cancellation are reported `Cancelled` without
  running. The report is always complete — cancellation is a result state, not an exception
  (matching `ProcessMessageAsync`'s cancel contract).
- **Concurrency** — `SemaphoreSlim(MaxConcurrency)`. Children are *started* in task order;
  with `MaxConcurrency = 1` execution order is exactly task order.
- **History isolation** — every child is a fresh `SimpleAgent` with its own
  `DefaultConversationManager`; the parent's conversation manager is never shared, and
  child histories are discarded with the child (its `Response` is the deliverable).

## Results and events

`ChildTaskRunReport`:

- `Results` — one `ChildTaskResult` per task, **same order as the input list**, always the
  full count. Per-child: `TaskIndex`, `Name`, `Status`
  (`Succeeded | Failed | BudgetExceeded | Cancelled`), `Response`, `StopReason`,
  `TurnCount`, `Duration`.
- `Outcome` — batch rollup: `Succeeded` (all children succeeded), `PartialFailure` (at
  least one child failed/exceeded budget, none cancelled), `Cancelled` (at least one child
  was cancelled).
- `TotalTurns` — aggregate LLM calls actually consumed.

`ChildAgentEvent` (via the `onEvent` callback, mirroring the `Action<AgentResponseDelta>`
streaming pattern): `ParentRunId` (one id per `RunChildTasksAsync` call), `ChildName`,
`TaskIndex`, `Kind` (`Started | ToolCalled | Completed`), `ToolName` (for `ToolCalled`),
`Status` (for `Completed`), `Timestamp`. Callbacks are serialized under a lock so
consumers (TUI progress panes, ACP adapters, headless logs) see a consistent stream
without their own synchronization; correlation is `ParentRunId` + `ChildName`.

The one-result-per-task promise is absolute: after batch validation passes, nothing may
fault the batch. A per-child setup failure (e.g. the workspace path colliding with an
existing file) becomes a `Failed` result for that child only, and a throwing event
consumer is logged and ignored — events are advisory.

## Testing

Moq-based, no real providers: parallel children return deterministically ordered results;
widening attempts (tools/workspace/provider/budget) throw before anything runs; disallowed
tool call at runtime surfaces as a failed tool result; aggregate turn budget holds exactly
under `MaxConcurrency > 1` (Interlocked, verified with concurrent never-finishing
children); parent cancellation cancels all children and the report still covers every
task; per-child failure does not disturb sibling results (partial failure); workspace
directories are created and children run in them; histories are isolated (no cross-child
context leakage in captured requests).
