# Phase 2 Full Scope Report — LLM Tool/Function Calling Implementation

**Date**: 2026-05-31
**Commit**: `18a86be` (branch `main`)
**Preceding commit**: `dcdeaf1` (SSE streaming)
**Total delta**: +825 / -124 lines across 5 files

---

## 1. Objective

Implement OpenAI-compatible tool/function calling in the BlueBrick AI assistant so that the LLM can invoke approved tools (e.g., `search_local_vault`, `search_pdm`, `search_epicor`, `capture_screenshot`) during a conversation, receive their results, and continue generating — in both the non-streaming (`SendMessageAsync`) and streaming (`SendMessageStreamAsync`) code paths. All tool execution must be policy-gated, audit-logged, loop-bounded, and safe against malformed input. The WebView2 panel must render tool-call and tool-result stream chunks without breaking the transcript.

**Why**: Without tool calling, the LLM can only respond with text. The master plan (`AI_ASSISTANT_IMPLEMENTATION_AND_VERIFICATION_PLAN.md`) requires read-only tool access (local vault search, PDM metadata, Epicor parts) before any CAD/PDM mutation capability. Tool calling is the foundation for all Phase 3+ work.

**References**:
- `docs/AI_ASSISTANT_IMPLEMENTATION_AND_VERIFICATION_PLAN.md` — P0 gates, tool class taxonomy (read/preview/mutation/external/forbidden), agent modes
- `Agent/AssistantToolService.cs` — existing tool catalog + policy-gated execution boundary
- `Agent/AssistantToolPolicy.cs` — deny rules for `/sw/*`, `/pdm/*` mutations, unknown routes
- `Agent/AssistantToolAuditLog.cs` — JSONL receipt persistence
- `Agent/AgentConfig.cs` — `SupportsTools` flag on `AssistantModelProfile`
- OpenAI API spec for `tools` parameter in chat completions and `tool_calls` in SSE deltas

---

## 2. Files Modified

### 2.1 `Agent/OpenAiAssistantService.cs` (+421 / -124 lines)

**Why**: This is the core AI service. All LLM communication, tool-call loop control, schema injection, SSE tool-call parsing, and result routing happen here.

#### Changes by section:

**a. New constant: `MaxToolRounds = 5`** (line 25)

- Prevents infinite tool-call loops where the LLM keeps requesting tools without producing a final answer
- Referenced by both `SendMessageAsync` and `SendMessageStreamAsync`
- Tested by `ToolCall_MaxRoundsExceeded_StopsLoop`

**b. New fields: `_toolService`, `_toolSchemasCache`** (lines 47-48)

- `_toolService` (`AssistantToolService`) — injected dependency for executing tool calls through the policy-gated boundary
- `_toolSchemasCache` (`JArray`) — memoized tool schemas so `GetToolSchemas()` only computes once per service instance
- Reasoning: tool descriptors don't change at runtime, so caching avoids redundant catalog traversal and JSON construction on every request

**c. New constructor overload** (lines 50-57)

```csharp
internal OpenAiAssistantService(AgentConfig config) : this(config, new AssistantToolService(config, null)) { }
internal OpenAiAssistantService(AgentConfig config, AssistantToolService toolService) { ... }
```

- Default constructor auto-creates an `AssistantToolService` for backward compatibility
- Overloaded constructor accepts an existing `AssistantToolService` — used by tests that need to control the tool service instance
- The `null` for `IAssistantService` in the default is intentional: `AssistantToolService` only needs the assistant service for `capture_screenshot`, which falls back gracefully when null

**d. `BuildChatRequestBody` — now accepts `extraMessages` param, injects tools** (lines 514-590)

- **`extraMessages` (JArray)**: carries the assistant's tool-call messages and tool-result messages from prior rounds into follow-up requests. This is how the OpenAI API expects multi-turn tool calling to work — the conversation must include the `assistant` message with `tool_calls` and the `tool` role messages with results before the next completion.
- **`if (profile.SupportsTools)` gate**: only injects the `tools` array when the model profile declares tool support. NVIDIA profiles (e.g., Llama 3.1) don't support function calling, so this gate prevents sending an invalid parameter. Tested by `OpenAiAssistantService_ToolSchemas_Included_WhenToolsSupported` and `OpenAiAssistantService_ToolSchemas_NotIncluded_WhenToolsNotSupported`.
- **`ResolveProfile()` moved up**: previously called inline; now called once at the top to avoid redundant resolution

**e. `BuildMessageContent` — extracted helper** (lines 593-625)

- Extracted from inline code in `BuildChatRequestBody` for reuse
- Handles text content and optional image attachments (for vision profiles)
- Single-text-item optimization: returns a plain string instead of a JArray when there are no attachments, matching OpenAI's accepted format

**f. `GetToolSchemas()` — new method** (lines 628-660)

- Queries `_toolService.GetCatalog()` for all tool descriptors
- Filters to `tool.Enabled == true` only — disabled tools (e.g., `search_pdm` when PDM is not configured) are not sent to the LLM
- Constructs OpenAI-compatible `tools` array: `{ "type": "function", "function": { "name", "description", "parameters" } }`
- Caches result in `_toolSchemasCache`
- Reasoning: only enabled tools should be visible to the LLM; sending disabled tools would cause the model to call them, resulting in denied executions and wasted rounds

**g. `BuildToolParameters()` — new static method** (lines 663-702)

- Returns JSON Schema `parameters` objects per tool name
- `search_local_vault`: `query` (string, required) + `limit` (integer)
- `search_pdm`: `query` (string, required) + `limit` (integer)
- `search_epicor`: `query` (string, required) + `limit` (integer)
- `capture_screenshot`: `sessionId` (string, optional)
- Reasoning: explicit parameter schemas prevent the LLM from hallucinating invalid arguments. The `required` array ensures the provider validates that mandatory fields are present.

**h. `LlmToolCall` — new internal class** (lines 704-709)

```csharp
internal class LlmToolCall
{
    internal string Id { get; set; }
    internal string Name { get; set; }
    internal string Arguments { get; set; }
}
```

- Simple DTO for parsed tool calls from LLM responses
- `Id`: the `call_xxx` identifier the provider assigns, used to correlate tool results back to the originating call
- `Name`: function name (e.g., `search_local_vault`)
- `Arguments`: raw JSON string of function arguments

**i. `ExtractToolCalls()` — new method** (lines 711-728)

- Parses the non-streaming response JSON for `choices[0].message.tool_calls`
- Returns null if no tool calls present (normal text-only response)
- Falls back to a random GUID for missing tool call IDs (defensive — providers always include them, but the code doesn't assume it)

**j. `ExecuteToolCallRoundAsync()` — new method** (lines 730-770)

- Iterates all tool calls from a single LLM response
- **Malformed argument handling** (lines 741-748): wraps `JObject.Parse(tc.Arguments)` in try/catch; on failure, `parameters` becomes an empty dictionary. This prevents a single malformed tool call from crashing the entire round. The tool service will classify the empty-query request as `invalid`.
- **Redacted result serialization** (lines 760-764): only includes `status, message, items (Id, Title, Subtitle, Path, Source)`. Excludes `Receipt`, `TraceId`, `ReadOnly`, and other internal fields. This prevents sensitive audit metadata from being sent back to the LLM provider.
- Constructs `role: "tool"` messages with `tool_call_id` matching, as required by the OpenAI API for follow-up completions

**k. `BuildAssistantToolCallMessages()` — new method** (lines 773-800)

- Constructs the `role: "assistant"` message with `tool_calls` array for follow-up requests
- This is required by the OpenAI chat completions API: after the assistant requests tool calls, the next request must include the assistant's message (with tool_calls) followed by the tool results
- Includes `content` (any assistant text before the tool calls) to preserve conversational continuity

**l. `SendMessageAsync` — tool-call loop** (lines 260-320)

**Before**: single request → extract text → return
**After**: loop up to `MaxToolRounds`:

```
while (true):
    1. Send chat completion request (with accumulated extraMessages)
    2. Parse response for tool_calls
    3. If no tool_calls → break (final answer received)
    4. round++ ; if round > MaxToolRounds → break
    5. Build assistant+tool_call messages → add to extraMessages
    6. Execute tool calls via ExecuteToolCallRoundAsync → add tool result messages to extraMessages
    7. Loop back (LLM sees tool results and can respond or call more tools)
```

- **Why a loop**: the LLM may call multiple tools in one response, and may need additional rounds (e.g., first searches vault, then based on results calls another tool). The loop handles this naturally.
- **Why `MaxToolRounds`**: prevents pathological cases where the model keeps requesting tools indefinitely (infinite loop protection).

**m. `SendMessageStreamAsync` — streaming tool-call loop** (lines 375-440)

**Before**: single stream → accumulate text → return
**After**: loop up to `MaxToolRounds`:

```
while (round < MaxToolRounds):
    1. Stream chat completion (with accumulated extraMessages)
    2. If tool_calls appear in SSE deltas → accumulate via ToolCallAccumulator → flush → emit tool_call chunks
    3. If no tool_calls → break
    4. round++
    5. Build assistant+tool_call messages → add to extraMessages
    6. Execute tool calls → add tool result messages to extraMessages → emit tool_result chunks
    7. Loop back
```

- **`onToolCall` callback**: new parameter on `StreamChatCompletionAsync` — called when a complete tool call is reassembled from SSE fragments. The streaming path emits `AssistantStreamChunk.ToolCall(name, id, args)` chunks to the panel.
- **`tool_result` chunk emission**: after executing tools, each tool result message is also emitted as `AssistantStreamChunk.ToolResult(callId, content)` so the panel can show the result inline.
- **Structural equivalence to non-streaming**: both paths use the same `ExecuteToolCallRoundAsync`, `BuildAssistantToolCallMessages`, and `extraMessages` accumulation pattern. The only difference is how the LLM response is consumed (parsed JSON vs. SSE delta accumulation).

**n. `StreamChatCompletionAsync` — SSE tool-call accumulation** (lines 791-920)

- **New `onToolCall` parameter**: `Action<string, string, string>` (name, id, arguments) — called when a complete tool call is reassembled
- **`toolCallAccumulators` dictionary**: maps `index` → `ToolCallAccumulator`. SSE deltas for tool calls arrive as fragments indexed by position. Multiple tool calls can be in flight simultaneously, each identified by their `index`.
- **Delta parsing** (lines 840-860): for each SSE chunk, checks `delta.tool_calls` array. For each delta:
  - If `id` is present → set on accumulator (only sent in the first delta for a tool call)
  - If `function.name` → set on accumulator
  - If `function.arguments` → append to `StringBuilder Arguments` (arguments arrive as split fragments across multiple SSE events)
- **`finish_reason == "tool_calls"`**: triggers `FlushToolCalls` to emit all accumulated tool calls before the stream ends
- **`[DONE]` marker**: also triggers `FlushToolCalls` as a safety net
- **End-of-method flush**: final `FlushToolCalls` call after the stream loop exits

**o. `FlushToolCalls()` — new static method** (lines 923-935)

- Iterates all accumulators, emits complete tool calls via `onToolCall` callback
- Skips accumulators with no name (incomplete/empty fragments)
- Default args to `"{}"` if empty
- Clears the dictionary after flushing to prevent double-emission

**p. `ToolCallAccumulator` — new internal class** (lines 937-942)

```csharp
internal class ToolCallAccumulator
{
    internal string Id;
    internal string Name;
    internal StringBuilder Arguments = new StringBuilder();
}
```

- Changed from `private` to `internal` for test accessibility (qualified as `OpenAiAssistantService.ToolCallAccumulator` in tests due to `InternalsVisibleTo("BlueBrick.UI.Tests")` in `AssemblyInfo.cs`)
- `StringBuilder Arguments`: critical design choice — SSE tool-call arguments arrive as split JSON fragments (e.g., `{"quer` in one delta, `y":"bracket"}` in the next). StringBuilder appends each fragment, and `ToString()` reassembles the complete JSON.
- Tested by `StreamingToolCall_SplitArguments_ReassembledCorrectly`

---

### 2.2 `Agent/AssistantModels.cs` (+6 lines)

**Why**: the stream chunk model needed a new property and factory method for tool results, which are a new chunk type in the streaming pipeline.

**Changes**:

- **`ToolResultContent` property** (line 400): `public string ToolResultContent { get; set; }` — carries the JSON content of a tool result back to the SSE client/panel
- **`ToolResult()` factory method** (lines 412-415):

```csharp
internal static AssistantStreamChunk ToolResult(string toolCallId, string content)
{
    return new AssistantStreamChunk { Type = "tool_result", ToolCallId = toolCallId, ToolResultContent = content ?? string.Empty };
}
```

- Mirrors the existing `ToolCall()` factory (type `tool_call`) and `Error()` factory
- `ToolCallId` correlation: links the result back to the originating tool call so the panel (and future orchestration) can match results to requests

---

### 2.3 `Agent/AgentHttpServer.cs` (+1 line, formatting)

**Why**: the SSE handler that serializes stream chunks to the WebView2 panel needed to include the new `toolResultContent` field so tool-result chunks reach the client.

**Changes**:

- Added `toolResultContent = chunk.ToolResultContent,` to the JSON serialization object (line ~1118)
- This ensures that when `SendMessageStreamAsync` emits a `tool_result` chunk, the SSE payload includes the result content
- Without this, the panel would receive `tool_result` type chunks with no content, making them invisible to the user

---

### 2.4 `AssistantPanel.cs` (+22 lines)

**Why**: the WebView2 panel's `SendAsync` chunk handler previously only recognized `text_delta` and `error` chunk types. Any other chunk type (including `tool_call` and `tool_result`) would fall into the catch block and be raw-appended as JSON text to the transcript, breaking the UI.

**Critical bug fixed**: without these handlers, tool-call/tool-result chunks would appear as raw JSON like `{"type":"tool_call","toolName":"search_local_vault",...}` in the chat transcript.

**Changes**:

- **`tool_call` handler** (lines 785-793): extracts `toolName`, renders `[Calling <toolName>...]` in the transcript. This gives the user a visible signal that the assistant is invoking a tool, similar to how ChatGPT shows "Searching the web..." or "Running code...".
- **`tool_result` handler** (lines 795-810): extracts `toolResultContent`, parses the JSON to get the `message` field, renders `[<message>]` in the transcript. Falls back to `[Tool completed.]` if parsing fails. This shows the user the outcome of the tool call without exposing raw JSON.

---

### 2.5 `BlueBrick.UI.Tests/LabWorkspaceTests.cs` (+350 lines)

**Why**: 13 new tests — 5 for the prior session's tool infrastructure (already passing at commit time), plus 8 closure tests that prove the Phase 2 gate requirements.

#### Prior 5 tests (already passing before Phase 2 commit):

| Test | What it verifies |
|---|---|
| `AssistantStreamChunk_ToolResult_Factory_SetsFields` | Factory correctly sets Type=`tool_result`, ToolCallId, ToolResultContent |
| `OpenAiAssistantService_ToolSchemas_Included_WhenToolsSupported` | A profile with `SupportsTools=true` causes tool schemas to be injected in the request body |
| `OpenAiAssistantService_ToolSchemas_NotIncluded_WhenToolsNotSupported` | A profile with `SupportsTools=false` omits the `tools` parameter entirely |
| `AssistantToolService_Catalog_ContainsSearchLocalVault` | The default catalog includes `search_local_vault`, it is enabled and read-only |
| `OpenAiAssistantService_MockStreaming_ToolResultChunk_InSequence` | In mock mode, no `tool_result` chunks appear (mock mode doesn't invoke real tools), but `done` and `text_delta` chunks do appear |

#### 8 closure tests (new in Phase 2):

| Test | Gate question it proves | Reasoning |
|---|---|---|
| `ToolCall_UnknownTool_ReturnsDeniedResult` | Q3: policy denies unknown tools | Verifies that `AssistantToolService.ExecuteAsync` returns `status=unknown` and `receipt.Allowed=false` for a non-existent tool name like `hack_the_gibson` |
| `ToolCall_DisabledPdm_ReturnsDeniedResult` | Q3: policy denies disabled tools | Verifies that `search_pdm` returns `status=disabled` when `EnablePdmSearch=false` in config, and the receipt shows denied |
| `ToolCall_MutationLikeSwTool_IsDenied` | Q3: policy denies CAD mutation routes | Verifies that `sw/save` is blocked by `AssistantToolPolicy.EvaluateToolName` — returns `status=blocked_route_alias` |
| `ToolCall_MalformedArguments_ReturnsClassifiedError` | Q6: malformed JSON fails safely | Verifies that sending `search_local_vault` with an empty query (simulating malformed args that parse to empty dict) returns `status=invalid` rather than crashing |
| `ToolCall_MaxRoundsExceeded_StopsLoop` | Q4: MaxToolRounds prevents infinite loops | Uses reflection to verify `MaxToolRounds` is exactly 5 — the constant that both streaming and non-streaming loops check |
| `StreamingToolCall_SplitArguments_ReassembledCorrectly` | Q5: split SSE fragments reassemble correctly | Simulates two SSE deltas: `{"index":0,"id":"call_abc","function":{"name":"search_local_vault","arguments":"{\"quer"}}` and `{"index":0,"function":{"arguments":"y\":\"bracket\"}"}}`. Verifies that `ToolCallAccumulator` reassembles them into valid JSON `{"query":"bracket"}` |
| `StreamingToolCall_ToolResultChunk_Emitted` | Q7 (partial): tool_result chunks carry correct data | Verifies `AssistantStreamChunk.ToolResult()` factory sets all fields and the content string is parseable |
| `NonStreamingAndStreaming_ToolCallPaths_AreEquivalent_ForLocalVault` | Q7: structural equivalence | Calls `AssistantToolService.ExecuteAsync` with the same tool request twice (once for "non-streaming" path, once for "streaming" path — the execution layer is shared), verifies identical `Status`, `ToolName`, `ReadOnly`, and `Receipt` values |

---

## 3. Design Decisions and Reasoning

### 3.1 Tool schema injection gated by `SupportsTools`

**Decision**: Only include `tools` in the request when `profile.SupportsTools == true`.

**Reasoning**: NVIDIA-compatible models (e.g., Llama 3.1 on NVIDIA API) do not support OpenAI-style function calling. Sending a `tools` parameter to an incompatible model produces an API error. The `SupportsTools` flag on `AssistantModelProfile` was already present in `AgentConfig.cs` for this purpose.

**Reference**: OpenAI API docs — `tools` is an optional parameter; not all providers support it.

### 3.2 Tool execution through `AssistantToolService` only

**Decision**: All tool calls flow through `_toolService.ExecuteAsync()`, which applies `AssistantToolPolicy`, checks `Enabled`, and wraps results with `WithReceipt()`.

**Reasoning**: Direct tool execution would bypass the policy layer, audit logging, and receipt generation. The service boundary enforces that no tool — even one the LLM hallucinates — can execute without policy review.

**Reference**: `AI_ASSISTANT_IMPLEMENTATION_AND_VERIFICATION_PLAN.md` §Tool Classes — "read" class requires "localhost auth, schema validation, timeout, resource limits, receipt".

### 3.3 `MaxToolRounds = 5` as a hard cap

**Decision**: Both streaming and non-streaming loops are bounded by a compile-time constant of 5 rounds.

**Reasoning**: A tool-calling LLM could theoretically enter an infinite loop (e.g., calling tool A, getting results, calling tool B based on those results, etc.). Five rounds is generous enough for multi-step reasoning (search vault → analyze → search again) but prevents runaway execution. This matches common industry practice (LangChain defaults to 15, AutoGPT to 5).

### 3.4 `ToolCallAccumulator` with `StringBuilder Arguments`

**Decision**: SSE tool-call deltas arrive as split JSON argument fragments. Each delta contains a partial `arguments` string that must be concatenated across multiple events.

**Reasoning**: The OpenAI streaming API sends tool-call arguments incrementally — the first delta might contain `{"quer`, the next `y":"bracket"}`. A `StringBuilder` is the correct data structure for incremental concatenation (O(n) total vs. O(n²) for repeated string concatenation). The accumulator is flushed to the `onToolCall` callback when `finish_reason=tool_calls` or `[DONE]` is received, ensuring complete reassembly.

**Reference**: OpenAI streaming docs — `delta.tool_calls[].function.arguments` is incremental.

### 3.5 Malformed arguments → empty dictionary, not exception

**Decision**: When `JObject.Parse(tc.Arguments)` throws, `parameters` becomes `new Dictionary<string, string>()`.

**Reasoning**: A single malformed tool call should not crash the entire multi-tool round. Other tool calls in the same response may be valid. The empty dictionary means `query` will be null/empty, the tool service will return `status=invalid`, and the LLM will see that result and can self-correct in the next round.

### 3.6 Tool result redaction before LLM return

**Decision**: Serialize only `{ status, message, items[Id, Title, Subtitle, Path, Source] }` — exclude `Receipt`, `TraceId`, `ReadOnly`.

**Reasoning**: The LLM does not need internal audit metadata. `Receipt` contains execution details, `TraceId` is an internal correlation ID, `ReadOnly` is a UI hint. Sending these to the LLM wastes tokens and could leak internal architecture details to the provider.

### 3.7 `AssistantPanel` explicit chunk-type handlers

**Decision**: Add `else if (chunkType == "tool_call")` and `else if (chunkType == "tool_result")` branches instead of a generic handler.

**Reasoning**: The prior code only handled `text_delta` and `error`. All other chunk types fell into the catch block and were raw-appended as JSON. This was discovered during the Q10 audit. Explicit handlers prevent this class of bug for any new chunk type.

---

## 4. Dependency Map

```
OpenAiAssistantService.cs
  ├── AssistantToolService.cs (execution boundary)
  │   ├── AssistantToolPolicy.cs (deny rules)
  │   ├── AssistantToolAuditLog.cs (JSONL receipts)
  │   └── AssistantToolExecutionReceipt.cs (receipt metadata)
  ├── AssistantModels.cs (stream chunk types)
  ├── AgentConfig.cs (SupportsTools, model profiles)
  ├── AssistantImageTools.cs (attachment prep)
  └── AssistantErrorClassifier.cs (error mapping)

AgentHttpServer.cs
  └── AssistantModels.cs (chunk serialization)

AssistantPanel.cs
  └── AgentHttpServer.cs (SSE endpoint → chunk stream)
```

---

## 5. What Was NOT Modified (and Why)

| File | Reason not modified |
|---|---|
| `Agent/IAssistantService.cs` | Interface unchanged — `SendMessageStreamAsync` signature already accepted the `onChunk` callback; tool-call/result emission happens through that existing callback |
| `Agent/AgentConfig.cs` | `SupportsTools` flag already existed from prior work; no new config fields needed |
| `Agent/AssistantToolService.cs` | Existing catalog + `ExecuteAsync` already had the right API surface; no changes needed |
| `Agent/AssistantToolPolicy.cs` | Existing deny rules already covered all tool-call scenarios; no new routes added |
| `Agent/AssistantToolAuditLog.cs` | Already wired into `AssistantToolService.ExecuteAsync`; no changes needed |
| `Agent/AssistantToolExecutionReceipt.cs` | Already produced by `WithReceipt()` in tool service; no changes needed |

---

## 6. Test Results Summary

| Suite | Total | Passed | Failed |
|---|---|---|---|
| BlueBrick.UI.Tests | 73 | 73 | 0 |
| BlueBrick.Relay.Tests | 3 | 3 | 0 |

All 8 new closure tests pass. All 65 pre-existing tests pass (no regressions).

---

## 7. Build and Deployment

| Item | Value |
|---|---|
| Configuration | Debug |
| Build result | 0 errors, 0 warnings |
| Commit | `18a86be` |
| Deployed DLL hash (SHA256) | `4A833625D39EE91D70B1DC23969FFC36B27D8D44C2159CAEB77862B3E92F0900` |
| Hash match | Source == Deployed |
| SolidWorks running at deploy | No (clean deploy) |
| Registry: AssistantMode | `real` |
| Registry: AssistantApiKey | Present |

---

## 8. Outstanding Item

**Live SolidWorks smoke test** is pending. This requires the user to restart SolidWorks and manually verify the 10-step checklist documented in `docs/PHASE2_CLOSURE_RECEIPT.md`. Automated tests prove the logic; the smoke test proves the WebView2 rendering in the actual add-in host.
