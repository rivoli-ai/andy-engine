# Bounded agent continuation

Status: complete as of 2026-07-23.

## Purpose

`SimpleAgent` keeps `maxTurns` as a hard per-window safeguard. An optional
`AgentContinuationPolicy` lets a request cross that boundary through compact checkpoints while
retaining independent global turn, continued-window, elapsed-time, and no-progress ceilings.

The policy is disabled by default. With no policy, `SimpleAgent` retains the existing
`max_turns_exceeded` behavior.

## Execution model

One user request has two representations:

- The audit transcript contains the original user objective, every assistant tool call, every tool
  result, and the final assistant response. It is append-only for the duration of the run and is
  committed consistently on completion, bounded stop, cancellation, or error.
- The provider request view is compacted at a continuation boundary. It contains a representative
  checkpoint and recent complete tool-call/result rounds. Retained calls are context only; the
  engine executes tools only from the provider's new response.

The default checkpoint includes the objective, completed work, observed outcomes, working
directory/task state, and remaining-work instructions. Hosts can supply an asynchronous
`CheckpointFactory` when they need a domain-specific representation.

## Limits and stop reasons

| Limit | Policy member | Stop reason |
| --- | --- | --- |
| All LLM turns | `MaxTotalTurns` | `continuation_total_turns_exceeded` |
| Windows/checkpoints after the initial window | `MaxContinuationWindows` | `continuation_windows_exceeded` |
| Optional wall-clock duration | `MaxElapsedTime` | `continuation_time_exceeded` |
| Repeated or oscillating progress | `EquivalentCheckpointLimit` | `continuation_no_progress` |

External cancellation retains the existing contract: partial transcript state is committed, then
`OperationCanceledException` propagates.

## Host integration

Subscribe to `SimpleAgent.ContinuationProgress` to receive correlated structured events:

- `WindowStarted`
- `WindowCompleted`
- `CheckpointCreated`
- `NoProgressDetected`
- `Stopped`
- `Completed`

Every event includes a run identifier, window number, total turns, and timestamp. Checkpoint and
terminal events also carry checkpoint text or a machine-readable stop reason as applicable.

## Acceptance checklist

- [x] A task can complete after crossing a per-window turn budget.
- [x] Total turns, continued windows/checkpoints, and optional elapsed time are strictly bounded.
- [x] Tool side effects are not replayed across a boundary.
- [x] Retained tool-call/result correlation remains valid.
- [x] Repeated and oscillating progress checkpoints trip the no-progress guard.
- [x] Cancellation during checkpointing or a continued window commits consistent history.
- [x] Full transcript export/restore preserves continued-run fidelity.
- [x] Omitting the continuation policy preserves existing max-turn behavior.
- [x] Structured lifecycle events let hosts render continuation without parsing response text.

## Completion summary

On 2026-07-23, bounded continuation was added to `SimpleAgent` with opt-in policy types, compact
checkpoint generation, recent correlated tool-round retention, global ceilings, elapsed-time
cancellation, oscillation detection, structured host events, transcript-safe cancellation, public
documentation, and focused regression coverage.
