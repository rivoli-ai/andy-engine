# C# Agentic Framework for Stable, Long-Running Tool-Using Conversations

> **Goal:** Build a production-grade C# framework that enables LLM-driven agents to plan, call tools reliably, recover from failures, and sustain multi-step conversations—backed by strict contracts, telemetry, and tests.

---

## 1. Architecture Overview

**Core Layers**  
1. **Planner**: Chooses next action (tool call, ask user, re-plan, stop). Deterministic, low temperature, grammar-constrained JSON for tool calls.  
2. **Executor**: Invokes tools through an **Adapter** that enforces schemas, timeouts, retries, pagination, and normalization.  
3. **Critic**: Evaluates observations vs. goal; checks constraints and decides if the task/subgoal is satisfied.  
4. **State Manager**: Tracks goal, subgoals, working memory, budgets (turns/time), last action/observation.  
5. **Observation Normalizer**: Converts raw tool outputs into `{summary, key_facts, affordances[]}`.  
6. **Telemetry & Tracing**: Structured per-turn events + trace viewer.  
7. **Sandbox (optional for code agents)**: Containerized execution with strict resource limits.

**Process Loop**
```
while (!budget.Exhausted && !state.Done):
  plan = Planner.Decide(state, lastObservation)
  action = Policy.Resolve(plan, lastObservation)
  observation = Executor.Execute(action)
  critique = Critic.Assess(goal, observation, constraints)
  state = State.Update(plan, observation, critique)
```

**Key Non-Goals**  
- Not a general chat framework.  
- All tools must be registered with typed specs; dynamic tool discovery is opt-in and gated.

---

## 2. Data Contracts & Types

### 2.1 Tool Specification
```csharp
public sealed record ToolSpec(
    string Name,
    Version Version,
    JsonSchema InputSchema,
    JsonSchema OutputSchema,
    RetryPolicy RetryPolicy,
    TimeSpan Timeout,
    int PageLimit = 100,
    int MaxPayloadBytes = 512_000
);
```

### 2.2 Tool Call & Result
```csharp
public sealed record ToolCall(string ToolName, JsonNode Args);

public enum ToolErrorCode {
    None, InvalidInput, Timeout, RetryableServer, RateLimited,
    OutputSchemaMismatch, NoResults, ToolBug, Unauthorized, Forbidden, NotFound
}

public sealed record ToolResult(
    bool Ok,
    JsonNode? Data,
    ToolErrorCode ErrorCode = ToolErrorCode.None,
    string? ErrorDetails = null,
    bool SchemaValidated = false,
    int Attempt = 1,
    TimeSpan Latency = default
);
```

### 2.3 Observation (Normalized)
```csharp
public sealed record Observation(
    string Summary,
    IReadOnlyDictionary<string, string> KeyFacts,
    IReadOnlyList<string> Affordances,
    ToolResult Raw
);
```

### 2.4 Agent State & Policies
```csharp
public sealed record AgentGoal(string UserGoal, IReadOnlyList<string> Constraints);

public sealed record Budget(int MaxTurns, TimeSpan MaxWallClock);

public sealed record AgentState(
    AgentGoal Goal,
    IReadOnlyList<string> Subgoals,
    ToolCall? LastAction,
    Observation? LastObservation,
    Budget Budget,
    int TurnIndex,
    IReadOnlyDictionary<string, string> WorkingMemoryDigest
);

public sealed record ErrorHandlingPolicy(
    int MaxRetries,
    TimeSpan BaseBackoff,
    bool UseFallbacks,
    bool AskUserWhenMissingFields
);
```

---

## 3. Planner, Executor, Critic

### 3.1 Planner
- **Input:** `AgentState`, tool catalog, last observation.  
- **Output:** One of: `ToolCall`, `AskUser(question, missingFields[])`, `Stop(reason)`, `Replan(newSubgoals)`.  
- **Determinism:** temperature≈0; JSON-mode or grammar-constrained decoding.  
- **Prompt must include:** tool catalog, error rubric, observation template, action template, and remaining budget.

**Interface**
```csharp
public interface IPlanner {
    PlannerDecision Decide(AgentState state);
}
```

### 3.2 Executor
- Validates input against `InputSchema` (rejects & logs if invalid).  
- Invokes the tool with timeout and retry/backoff for retryable errors.  
- Validates output against `OutputSchema`; on mismatch → policy: repair (bounded), or escalate.  
- Paginates and caps payloads; attaches provenance and timings.

**Interface**
```csharp
public interface IExecutor {
    ToolResult Execute(ToolCall call, CancellationToken ct);
}
```

### 3.3 Critic
- Checks whether observation satisfies the active subgoal; may suggest re-plan or clarification.  
- Populates `known_gaps[]` for next step.

**Interface**
```csharp
public interface ICritic {
    Critique Assess(AgentGoal goal, Observation observation);
}
```

---

## 4. Tool Adapter Layer

**Responsibilities**
- Single entrypoint for all tool calls.  
- Schema validation (input & output).  
- Error taxonomy mapping (HTTP/SDK errors → `ToolErrorCode`).  
- Normalization (field names, units, empty/partial data).  
- Pagination with `nextToken/page`.  
- Payload caps with truncation notes.

**C# Example**
```csharp
public sealed class ToolAdapter : IExecutor {
    private readonly IToolRegistry _registry;
    private readonly IJsonValidator _validator;
    private readonly ISystemClock _clock;
    private readonly ILogger<ToolAdapter> _log;

    public ToolAdapter(IToolRegistry registry, IJsonValidator validator, ISystemClock clock, ILogger<ToolAdapter> log) {
        _registry = registry; _validator = validator; _clock = clock; _log = log;
    }

    public ToolResult Execute(ToolCall call, CancellationToken ct) {
        var spec = _registry.Get(call.ToolName);
        if (spec is null)
            return new ToolResult(false, null, ToolErrorCode.NotFound, "Unknown tool");

        var (inOk, inErr) = _validator.Validate(call.Args, spec.InputSchema);
        if (!inOk)
            return new ToolResult(false, null, ToolErrorCode.InvalidInput, inErr, SchemaValidated: false);

        var sw = Stopwatch.StartNew();
        try {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(spec.Timeout);

            // Invoke tool implementation (HTTP/RPC/etc.)
            var raw = spec.Invoke(call.Args, cts.Token);

            var (outOk, outErr, normalized) = _validator.ValidateAndNormalize(raw, spec.OutputSchema);
            sw.Stop();

            return outOk
                ? new ToolResult(true, normalized, ToolErrorCode.None, null, SchemaValidated: true, Latency: sw.Elapsed)
                : new ToolResult(false, raw, ToolErrorCode.OutputSchemaMismatch, outErr, SchemaValidated: false, Latency: sw.Elapsed);
        }
        catch (OperationCanceledException) {
            sw.Stop();
            return new ToolResult(false, null, ToolErrorCode.Timeout, "Tool timeout", Latency: sw.Elapsed);
        }
        catch (RetryableException ex) {
            sw.Stop();
            return new ToolResult(false, null, ToolErrorCode.RateLimited, ex.Message, Latency: sw.Elapsed);
        }
        catch (Exception ex) {
            sw.Stop();
            _log.LogError(ex, "Tool {Tool} crashed", call.ToolName);
            return new ToolResult(false, null, ToolErrorCode.ToolBug, ex.ToString(), Latency: sw.Elapsed);
        }
    }
}
```

---

## 5. Observation Normalization

**Goal:** Keep the LLM’s context small and predictable.
```csharp
public interface IObservationNormalizer {
    Observation Normalize(string toolName, JsonNode raw, ToolResult result);
}
```
**Template**
```json
{
  "summary": "One sentence of what happened.",
  "key_facts": { "factA": "val", "factB": "val" },
  "affordances": ["next_page", "retry_with_param_x", "fallback_tool_y"]
}
```

---

## 6. Policies (Retries, Fallbacks, Repair, Stop)

```csharp
public sealed class PolicyEngine {
    public AgentAction Resolve(PlannerDecision decision, Observation? lastObs, ErrorHandlingPolicy policy) {
        // 1) Retry <= MaxRetries for RetryableServer/RateLimited/Timeout
        // 2) Use fallback if defined
        // 3) Ask user for missing fields
        // 4) Summarize and stop if budget exceeded or unrecoverable
    }
}
```

**Retry Backoff**: Decorrelated jitter (`FullJitter`) to avoid thundering herds.  
**Repair Bounds**: Only attempt schema repair when edit distance < N or missing fields are uniquely inferrable.

---

## 7. Context & Memory

- **Working Memory**: rolling window of last K normalized observations + plan deltas.  
- **Project Memory (code agents)**: file index, embeddings, LSP signals, test outcomes, git diff.  
- **Compression**: Summarize after each large step; keep a “decision log” as short bullet points.

```csharp
public interface IStateStore {
    AgentState Load(Guid traceId);
    void Save(Guid traceId, AgentState state);
}
```

---

## 8. Telemetry & Tracing

**Event Schema**
```json
{
  "trace_id": "guid",
  "turn": 7,
  "role": "planner|executor|critic",
  "intent": "call_tool|ask_user|stop",
  "tool_call": {"name":"search","args_valid":true},
  "tool_result": {"ok":true,"err":"None","schema_ok":true},
  "policy": {"max_turns":16,"time_budget_s":120},
  "outcome": {"status":"continue","next_action":"call_tool"},
  "timings": {"latency_ms": 432},
  "tokens": {"input": 1534, "output": 214}
}
```
- Emit events via `ILogger` + OpenTelemetry exporters (OTLP).  
- Provide a local **Trace Viewer** (ASP.NET minimal API + Razor page).

**Key Metrics**
- Turn survival curve (alive after 2/4/8/16 tool calls).  
- Step success rate (`tool_result.schema_ok`).  
- Repair success % and mean retries.  
- p95 tool latency; error taxonomy distribution.  
- End states: `DONE`, `CLARIFY_NEEDED`, `BUDGET_EXCEEDED`, `UNRECOVERABLE_TOOL_CONTRACT`.

---

## 9. Testing Strategy

### 9.1 Unit Tests
- Tool adapter: happy path, invalid input, output mismatch, timeout, 429/5xx, empty results, pagination, large payloads.  
- Policy engine: retry/fallback/ask/stop decision table.  
- Normalizer: consistency and size limits.  
- Planner integration with a **Fake LLM** (deterministic).

### 9.2 Integration (Agent Loop)
- Single-tool multi-turn, multi-tool dependencies (A→B), branch on failure, re-ask flow, stop conditions.  
- Sandbox: timeouts and kill-switch verified.

### 9.3 Golden Conversations
- Record N successful end-to-end runs; replay in CI.  
- Fail if output or action trace deviates beyond allowed diffs.

### 9.4 Fuzz & Adversarial
- Prompt fuzz: drop required args, contradict constraints, inject invalid JSON.  
- Tool fuzz: wrong enums, missing fields, extra unknown fields, empty page 1, truncated payloads.  
- Protocol fuzz: unknown tool name, multiple tools in one call, nested tool calls.

### 9.5 Load & Soak
- 100–1000 parallel conversations using fakes; assert no deadlocks or memory leaks.  
- Observe resource ceilings and GC behavior under pressure.

---

## 10. CI/CD & Release Gates

- **Build:** `dotnet build -c Release` with analyzers + nullable enabled.  
- **Tests:** unit, integration, goldens, fuzz (subset per PR; full nightly).  
- **Quality Gates:**  
  - 100% unit/integration pass  
  - Fuzz survival ≥ 95%  
  - No P0 in error budget from canary  
  - p95 tool latency within SLO  
- **Canary:** shadow mode (read-only decisions), then 1–5% traffic, then ramp.

---

## 11. Security & Compliance

- Least-privilege API keys; per-tool credentials via `IOptions<T>` + user secrets/KeyVault.  
- Input/output PII redaction before logging.  
- Deterministic sanitization for code execution outputs.  
- SBOM generation; dependency scanning (Dependabot/Snyk).  
- Multi-tenant isolation (per-tenant encryption keys).

---

## 12. Performance Considerations

- Async everywhere (`Task`, `IAsyncEnumerable`).  
- Batching & caching (embeddings, search results).  
- Truncate large tool outputs and offer continuation affordances.  
- Keep prompts compact: catalog summary + last observation digest + plan delta.  
- Memory pressure: pool buffers (`ArrayPool<byte>`), avoid large string concatenations (use `StringBuilder`).

---

## 13. Reference Implementations (Skeletons)

### 13.1 Tool Registry
```csharp
public interface IToolRegistry {
    ToolSpec? Get(string name);
    IEnumerable<ToolSpec> List();
}
```

### 13.2 JSON Validation
Use **JsonSchema.Net** (Newtonsoft or System.Text.Json) for runtime schemas.
```csharp
public interface IJsonValidator {
    (bool ok, string? error) Validate(JsonNode instance, JsonSchema schema);
    (bool ok, string? error, JsonNode normalized) ValidateAndNormalize(JsonNode instance, JsonSchema schema);
}
```

### 13.3 Fake LLM (for tests)
```csharp
public sealed class FakeLlmPlanner : IPlanner {
    private readonly IDictionary<string, PlannerDecision> _goldens;
    public PlannerDecision Decide(AgentState state) => _goldens.TryGetValue(Hash(state), out var d) ? d : DefaultDecision();
}
```

---

## 14. Example Tool Schemas (JSON)

**Search Tool (Input)**
```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["query"],
  "properties": {
    "query": { "type": "string", "minLength": 1 },
    "page": { "type": "integer", "minimum": 1, "default": 1 },
    "page_size": { "type": "integer", "minimum": 1, "maximum": 50, "default": 10 }
  },
  "additionalProperties": false
}
```

**Search Tool (Output)**
```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["results"],
  "properties": {
    "results": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["title", "url"],
        "properties": {
          "title": { "type": "string" },
          "url": { "type": "string", "format": "uri" },
          "snippet": { "type": "string" }
        },
        "additionalProperties": false
      }
    },
    "next_page": { "type": "integer" }
  },
  "additionalProperties": false
}
```

---

## 15. Minimal Planner Prompt Skeleton

**System Prompt (excerpt)**
```
You are the Planner. Choose exactly one next action:
- call_tool: { "name": <tool_name>, "args": { ... } }
- ask_user: { "question": "...", "missing_fields": ["..."] }
- replan: { "subgoals": ["..."] }
- stop: { "reason": "..." }

If a tool fails:
- Retry ≤2 with backoff for retryables;
- Else attempt fallback;
- Else ask_user for missing information;
- Else stop with a short summary.
```

**Action JSON (grammar-constrained)**
```json
{ "action": "call_tool", "name": "search", "args": { "query": "..." } }
```

---

## 16. Milestones

1. **M0 (Week 1-2):** Tool registry, schemas, adapter, validator, baseline telemetry.  
2. **M1 (Week 3-4):** Planner/Critic interfaces, observation normalizer, policy engine.  
3. **M2 (Week 5-6):** Unit + integration tests, fake LLM, trace viewer.  
4. **M3 (Week 7-8):** Golden suites, fuzz harness, load tests, release gates.  
5. **M4 (Week 9+):** Sandbox for code execution, canary rollout, continuous benchmarking.

---

## 17. Example ASP.NET Minimal Host (Skeleton)

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging();
builder.Services.AddSingleton<IJsonValidator, JsonSchemaValidator>();
builder.Services.AddSingleton<IToolRegistry, ToolRegistry>();
builder.Services.AddSingleton<IExecutor, ToolAdapter>();
builder.Services.AddSingleton<IPlanner, LlmPlanner>();      // integrate chosen LLM here
builder.Services.AddSingleton<ICritic, DefaultCritic>();
builder.Services.AddSingleton<IObservationNormalizer, DefaultNormalizer>();
builder.Services.AddSingleton<PolicyEngine>();

var app = builder.Build();

app.MapPost("/chat", async (ChatRequest req, IPlanner planner, IExecutor exec, ICritic critic, PolicyEngine policy) => {
    // hydrate state
    var state = req.ToState();
    var decision = planner.Decide(state);
    var action = policy.Resolve(decision, state.LastObservation, req.Policy);
    var result = action is CallTool ct ? exec.Execute(ct.Call, req.Ct) : ToolResult.OkResult;
    var obs = new DefaultNormalizer().Normalize(ct.Call.ToolName, result.Data!, result);
    var critique = critic.Assess(state.Goal, obs);
    // update & return
    return Results.Ok(new ChatResponse { Observation = obs, Critique = critique });
});

app.Run();
```

---

## 18. Appendix: Error Codes & Mapping

| Error | HTTP/SDK Mapping | Policy |
|---|---|---|
| InvalidInput | 400/422 validation | Ask user / repair |
| Timeout | TaskCanceled/504 | Retry then fallback |
| RetryableServer | 500-599 | Retry with backoff |
| RateLimited | 429 | Retry with backoff + jitter |
| Unauthorized/Forbidden | 401/403 | Stop with action item |
| NotFound | 404 | Ask/replan |
| OutputSchemaMismatch | Any | Attempt bounded repair else stop |
| ToolBug | Exception | Stop & alert |

---

**End of Spec**
