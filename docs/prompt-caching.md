# Prompt caching for SimpleAgent

## Problem

`SimpleAgent.ProcessMessageAsync` rebuilds and resends the full prefix
(`SystemPrompt` + the entire growing conversation history) on **every** turn of a
multi-turn run. For an agentic loop that makes many tool-calling round-trips this
prefix is by far the dominant input-token cost and a large share of latency.

The standard fix is **provider-side prompt caching**: mark the stable prefix
(system prompt, tool declarations, and the already-settled history) as cacheable so
the provider only bills/processes the changed suffix on subsequent turns.

- **Anthropic / Claude on OpenRouter**: requires explicit `cache_control`
  breakpoints, e.g. a content block with `"cache_control": {"type": "ephemeral"}`
  on the last stable message (and/or the system prompt). Without a breakpoint there
  is **no** caching for Anthropic upstreams.
- **OpenAI / DeepSeek (and a few others) on OpenRouter**: caching is **automatic** —
  no request field is needed; the provider caches a stable prefix on its own. The
  only requirement is that the request prefix is byte-stable across turns.

## Investigation result — BLOCKED on the upstream library

Prompt caching **cannot be wired into `SimpleAgent` today** because the
provider-abstraction packages the engine depends on expose no cache-control surface:

- `Andy.Llm` **2026.5.29-rc.31**
- `Andy.Model` **2025.10.29-rc.5**

(both versions are the ones referenced by `src/Andy.Engine/Andy.Engine.csproj`).

What the request types actually expose:

```
Andy.Model.Llm.LlmRequest      : Messages, Tools, Config, SystemPrompt, Model,
                                  Temperature, MaxTokens, TopP        // no cache field
Andy.Model.Llm.LlmClientConfig : Model, Temperature, MaxTokens, TopP // no cache field
Andy.Model.Model.Message       : Role, Content, ToolCalls, ToolResults,
                                  Metadata (Dictionary<string,object>), Timestamp,
                                  Id, ToolCallId, Parts                // no cache field
```

Findings, verified by reflecting over the packaged assemblies and disassembling
`Andy.Llm.dll` (`ikdasm`):

1. **No cache-control field exists on any request/config/message type.** The only
   `Cache*` member in `Andy.Llm` is `LlmOptions.CacheDurationMinutes`, which caches
   *provider client instances* per alias (`_providerCache`) — unrelated to prompt
   caching.

2. **`Message.Metadata` is not a usable backdoor.** It is never read during outbound
   request serialization by any provider (the only `get_Metadata` call sites in
   `Andy.Llm` are `LlmOperationProgress` and response-side `StructuredResponseMetadata`).
   Setting a metadata key would be silently dropped, so injecting `cache_control`
   through it is impossible.

3. **No provider emits cache breakpoints.** The string literals `cache_control` /
   `ephemeral` / `cacheControl` do **not** appear anywhere in `Andy.Llm.dll`.

4. **The Anthropic provider itself cannot cache.** `AnthropicMessagesRequest.System`
   is a plain `string` (not a content-block array) and `AnthropicContentBlock` has
   fields `Type, Text, Id, Name, Input, ToolUseId, Content` — **no `CacheControl`**.
   So even the native Anthropic path has no breakpoint slot.

5. **The OpenRouter provider (what the SWE-bench harness uses) sends a vanilla
   OpenAI-style `chat/completions` body** — top-level keys observed: `model`,
   `messages`, `tools`, `temperature`, `top_p`, `max_tokens`, `stream`. There is no
   `provider` routing object, no `transforms`, and no per-message `cache_control`.

Because none of these expose a breakpoint, faking it in `SimpleAgent` (e.g. stuffing
a JSON key into `Content` or `Metadata`) would not reach the wire and was therefore
**not** implemented, per task guardrails.

## Partial win already in place (no code change needed)

For the auto-caching upstreams (OpenAI / DeepSeek family on OpenRouter), the only
precondition is a **byte-stable request prefix** across turns. `SimpleAgent` already
satisfies this:

- `SystemPrompt = _systemPrompt` is a constant for the life of the agent instance.
- `BuildContextFromConversation()` replays committed turns in stable chronological
  order (`UserOrSystemMessage` → interleaved `ToolMessages` → final `AssistantMessage`),
  then appends the current in-flight messages. Settled turns are never reordered or
  rewritten, so the prefix grows append-only.
- `BuildToolDeclarations()` enumerates the registry deterministically, so the tool
  block is stable too.

Net effect: on OpenRouter auto-cache models the stable prefix is **already eligible**
for automatic caching with no change to the engine. There is no additional low-risk
header or field we can set today that would improve this without the upstream API —
the SWE-bench harness's current model (Moonshot Kimi) is not an auto-cache upstream,
so it sees no benefit until explicit breakpoints are supported.

## Minimal upstream change required to fully land this

The smallest viable change in `Andy.Llm` / `Andy.Model` to enable explicit caching
(the only thing that helps Anthropic and OpenRouter-routed Anthropic models):

1. **Add an opt-in cache marker to the model.** Either:
   - a `CacheControl` flag on `Andy.Model.Model.Message`
     (e.g. `bool CacheBreakpoint` or `string? CacheControlType`), **or**
   - a request-level "cache up to message N / cache the system prompt" hint on
     `Andy.Model.Llm.LlmRequest` / `LlmClientConfig`.

2. **Honor it in the providers' request serialization:**
   - **Anthropic / OpenRouter→Anthropic**: render the system prompt and the marked
     message(s) as content-block arrays and attach
     `"cache_control": {"type": "ephemeral"}` to the breakpoint block(s). This means
     `AnthropicMessagesRequest.System` must become a content-block array and
     `AnthropicContentBlock` must gain a `CacheControl` property.
   - **OpenAI / DeepSeek**: no field needed (automatic) — the marker is simply a no-op.

3. (Optional) Surface returned cache usage (`cache_creation_input_tokens` /
   `cache_read_input_tokens` / `prompt_tokens_details.cached_tokens`) on
   `LlmResponse.Usage` so the engine can verify hit rates.

## Engine-side change to land once the upstream API exists

When `Andy.Llm` exposes a cache marker, the `SimpleAgent` change is small and was
the intended deliverable:

- Add a constructor flag `bool enablePromptCaching = true`.
- When enabled, set the cache breakpoint on the system prompt and on the last
  **stable** (already-committed) history message before sending each request — i.e.
  mark `contextMessages.Last()` (not the in-flight suffix) so the cached prefix grows
  with the conversation while the changing tail stays uncached.
- Add a unit test asserting the request is built with the cache marker(s) when the
  flag is on, and without them when off.

Until then, this item is **blocked on the upstream library** and intentionally ships
as documentation plus the (already-satisfied) stable-prefix precondition.
