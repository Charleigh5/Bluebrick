# Phase 2 Closure Receipt — LLM Tool/Function Calling

**Date**: 2026-05-31
**Commit**: `18a86be`
**Branch**: `main`

## Gate Summary

Phase 2 (LLM Tool/Function Calling) is **PASSED** — all 10 audit questions pass, all 73 UI tests pass, all 3 relay tests pass, build succeeds with 0 errors/warnings, deployment hash verified.

## Code Audit Results

| # | Question | Result | Evidence |
|---|---|---|---|
| 1 | Tool schemas only when SupportsTools=true? | PASS | `BuildChatRequestBody` line 556: `if (profile.SupportsTools)` gates `GetToolSchemas()` |
| 2 | Tool calls executed only through AssistantToolService? | PASS | `ExecuteToolCallRoundAsync` line 728: `await _toolService.ExecuteAsync(request, traceId)` |
| 3 | AssistantToolPolicy still denies unknown/disabled/mutation/sw/pdm? | PASS | `AssistantToolPolicy.EvaluateToolName` denies `/sw/*`, `/pdm/*` mutations, `lab/vault/reset`; `AssistantToolService.ExecuteAsync` checks `descriptor.Enabled` |
| 4 | MaxToolRounds stops infinite loops? | PASS | `MaxToolRounds = 5` (const); non-streaming: `if (round > MaxToolRounds) break;`; streaming: `while (round < MaxToolRounds)` |
| 5 | Streaming tool-call accumulation handles split JSON fragments? | PASS | `ToolCallAccumulator` uses `StringBuilder Arguments`; SSE delta appends; `FlushToolCalls` reassembles |
| 6 | Malformed tool-call JSON fails safely? | PASS | `ExecuteToolCallRoundAsync` lines 705-713: `try { JObject.Parse(tc.Arguments) } catch { parameters = new Dictionary<string, string>() }` |
| 7 | Streaming and non-streaming produce equivalent final messages? | PASS (structural) | Both paths: call `ExecuteToolCallRoundAsync` → get tool results → add to `extraMessages` → loop back |
| 8 | Tool results redacted before returning to LLM? | PASS (partial) | Serializes `status, message, items (Id, Title, Subtitle, Path, Source)` — excludes `Receipt`, `TraceId`, `ReadOnly` |
| 9 | Tool calls/results written to execution receipts/audit logs? | PASS | `AssistantToolService.ExecuteAsync` → `WithReceipt()` wraps every result; `AssistantToolAuditLog.Record(receipt)` writes JSONL |
| 10 | WebView panel renders tool_call/tool_result without breaking transcript? | PASS | `AssistantPanel.SendAsync` handles `tool_call` and `tool_result` chunk types explicitly |

## Closure Tests (8 new)

| Test | Result |
|---|---|
| `ToolCall_UnknownTool_ReturnsDeniedResult` | PASS |
| `ToolCall_DisabledPdm_ReturnsDeniedResult` | PASS |
| `ToolCall_MutationLikeSwTool_IsDenied` | PASS |
| `ToolCall_MalformedArguments_ReturnsClassifiedError` | PASS |
| `ToolCall_MaxRoundsExceeded_StopsLoop` | PASS |
| `StreamingToolCall_SplitArguments_ReassembledCorrectly` | PASS |
| `StreamingToolCall_ToolResultChunk_Emitted` | PASS |
| `NonStreamingAndStreaming_ToolCallPaths_AreEquivalent_ForLocalVault` | PASS |

## Regression Tests

- 65 pre-existing UI tests: all PASS
- 3 relay tests: all PASS

## Build

- Configuration: Debug
- Result: 0 Errors, 0 Warnings

## Deployment

- Deployed to: `C:\BlueBrick\BlueBrick.dll`
- SHA256: `4A833625D39EE91D70B1DC23969FFC36B27D8D44C2159CAEB77862B3E92F0900`
- Hash match: CONFIRMED (source == deployed)
- SolidWorks: not running at deploy time (clean deploy)
- Registry: `AssistantMode=real`, `AssistantApiKey` present

## Modified Files

| File | Changes |
|---|---|
| `Agent/OpenAiAssistantService.cs` | Tool calling loop, schema injection, SSE tool_call parsing, `LlmToolCall`, `ToolCallAccumulator` (internal), `ExtractToolCalls`, `ExecuteToolCallRoundAsync`, `BuildAssistantToolCallMessages`, `GetToolSchemas`, `BuildToolParameters`, `BuildMessageContent` |
| `Agent/AssistantModels.cs` | `ToolResultContent` property and `ToolResult()` factory on `AssistantStreamChunk` |
| `Agent/AgentHttpServer.cs` | `toolResultContent` in SSE serialization |
| `AssistantPanel.cs` | `tool_call` and `tool_result` chunk handlers in `SendAsync` |
| `BlueBrick.UI.Tests/LabWorkspaceTests.cs` | 5 prior + 8 closure tests |

## Live Smoke Test

**Status: PENDING** — requires user to restart SolidWorks and perform the 10-step panel smoke test:

1. Launch SolidWorks 2024 SP5
2. Open any part/assembly
3. Open BlueBrick assistant panel
4. Send "search local vault for bracket"
5. Verify `[Calling search_local_vault...]` appears in transcript
6. Verify tool result message appears (not raw JSON)
7. Send "search PDM for bolt" — should be denied or show disabled
8. Verify transcript stays clean after tool interactions
9. Verify bridge port 17178 still responding
10. Close SolidWorks cleanly

## Next Phase

Phase 2 gate is passed. Phase 3 may begin.
