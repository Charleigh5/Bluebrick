# SPEC: P0 Gate Closure + 6 C# Bug Fixes

**Version**: 1.0-draft  
**Date**: 2026-05-28  
**Status**: DRAFT — awaiting human review  
**Scope**: Close all P0 gates for the BlueBrick AI Assistant and fix 6 identified C# bugs in a single coherent delivery.

---

## 1. Objective

Close the 7 P0 gates defined in the Rebuilt Master Plan and fix the 6 C# bugs that currently prevent the assistant from functioning correctly. The P0 gates and bug fixes are interdependent — several bugs directly undermine P0.2 (Cancellation) and P0.6 (Bridge Security), so they must be fixed together.

**Exit criteria**: All 7 P0 gates pass their acceptance tests, all 6 bugs are verified fixed, no double-serialize anti-patterns remain, and the entire codebase is baselined into git (P0.1).

---

## 2. Scope

### In Scope
- **P0.1 Source-of-Truth**: Git baseline of all WIP assistant code
- **P0.2 Cancellation Regression**: Cancellation-safe streaming + regression tests
- **P0.3 Model Authority ADR**: Already created; verify completeness
- **P0.4 Model Profile Contract**: Verify existing `AssistantModelProfile` capability flags
- **P0.5 WebView2 Security**: Verify existing guards; add navigation/DOM test hooks
- **P0.6 Bridge Security**: Add body-size limit to `AgentHttpServer`; add route-level validation test
- **P0.7 Tool Policy**: Verify existing deny rules; add route-level live validation test
- **Bug 1** (CRITICAL): `PostStreamingAsync` read timeout — `AgentPanelClient.cs:104`
- **Bug 2** (CRITICAL): `bbGetTranscript` double-encoding — `AssistantPanel.cs:940`, `FrmAssistantWindow.cs:458`
- **Bug 3** (SUBSTANTIAL): SSE line splitting across buffer boundaries — `AgentPanelClient.cs:103-124`
- **Bug 4** (SUBSTANTIAL): Non-SSE response `message.Text` extraction — `AssistantPanel.cs:694`, `FrmAssistantWindow.cs:249`
- **Bug 5** (MINOR): Double-serialize anti-pattern — 10+ call sites across both panel files
- **Bug 6** (MINOR): `LogErrorAsync` concurrency safety — `AssistantPanel.cs:927`, `FrmAssistantWindow.cs:445`

### Out of Scope
- Design tool TS tasks 7-9 (deferred until after C# fixes)
- AionUI broker sync for model profiles (P0.4 is verification-only)
- Approval workflow UI for tool policy (P0.7 is verification + route test only)
- Server-side SSE conversion (Bug 4 is client-side extraction fix only)

---

## 3. Tech Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| Runtime | .NET Framework | 4.8 |
| Language | C# | LangVersion 8 |
| WebView | Microsoft.Web.WebView2 | 1.0.2365.46 |
| JSON | Newtonsoft.Json | (from csproj) |
| Build | MSBuild (VS Build Tools 2022) | Latest |
| Tests | MSTest (dotnet test) | .NET 6+ test project |
| VCS | Git | main branch |
| SolidWorks | Add-in SDK | (existing) |

---

## 4. Commands

| Command | Purpose |
|---------|---------|
| `dotnet build BlueBrick.csproj -c Lab -v minimal` | Build the add-in |
| `dotnet test BlueBrick.UI.Tests -c Debug --filter "FullyQualifiedName~LabWorkspaceTests"` | Run existing tests |
| `dotnet test BlueBrick.UI.Tests -c Debug` | Run all tests |
| `powershell -File deploy_bluebrick_safe.ps1` | Deploy Debug DLL to C:\BlueBrick\ |
| `git add -A && git commit -m "..."` | Baseline WIP |

**Prerequisite**: VS Build Tools 2022 must be installed from `C:\Users\cweir\Downloads\vs_buildtools.exe` before any build/test commands can run.

---

## 5. Project Structure

```
Bluebrick\
├── Agent\
│   ├── AgentPanelClient.cs          ← Bugs 1, 3
│   ├── AgentHttpServer.cs           ← P0.6 body-size limit
│   ├── AssistantErrorClassifier.cs  ← (stable, reference)
│   ├── AssistantModels.cs           ← (stable, reference)
│   ├── AssistantToolPolicy.cs       ← (stable, reference)
│   ├── AssistantToolAuthorization.cs← (stable, reference)
│   ├── AssistantToolAuditLog.cs     ← (stable, reference)
│   ├── AssistantToolExecutionReceipt.cs ← (stable, reference)
│   ├── AssistantWebViewSecurity.cs  ← (stable, reference)
│   └── OpenAiAssistantService.cs    ← (stable, reference)
├── AssistantPanel.cs                ← Bugs 2, 4, 5, 6
├── FrmAssistantWindow.cs            ← Bugs 2, 4, 5, 6
├── BlueBrick.csproj
├── BlueBrick.UI.Tests\
│   ├── LabWorkspaceTests.cs         ← Add cancellation + security tests here
│   └── TestHttpServer.cs            ← (existing mock server)
├── BlueBrick.Relay.Tests\
│   └── RelayCoreTests.cs
└── docs\
    ├── SPEC_P0_GATES_AND_BUG_FIXES.md     ← This document
    ├── BLUEBRICK_AIONUI_MODEL_AUTHORITY_ADR.md
    ├── BLUEBRICK_ASSISTANT_ROUTE_MANIFEST.md
    ├── BLUEBRICK_SOURCE_OF_TRUTH_PROOF.md
    └── AI_ASSISTANT_IMPLEMENTATION_AND_VERIFICATION_PLAN.md
```

---

## 6. Bug Fix Specifications

### Bug 1 — CRITICAL: No read timeout in PostStreamingAsync

**File**: `AgentPanelClient.cs:90-128`  
**Problem**: `HttpClient.Timeout` with `ResponseHeadersRead` only covers the header phase. The `while` loop at line 104 calls `reader.ReadAsync` with no per-chunk idle timeout. If the server stalls mid-stream, the client hangs forever.  
**Fix**:
1. Inside `PostStreamingAsync`, create a `CancellationTokenSource` with 30-second `CancelAfter`.
2. Link it to the caller's `cancellationToken` via `CancellationTokenSource.CreateLinkedTokenSource`.
3. Reset `idleCts.CancelAfter(30_000)` before each `ReadAsync` call.
4. Catch `OperationCanceledException` from the idle token separately — if it fires, throw a descriptive timeout exception (not a user cancellation).
5. Dispose both the idle CTS and the linked CTS in a `finally` block.

**Verification**: Add a unit test that starts a mock server that sends headers then stalls for >30s. Assert the client throws a timeout exception within ~32s.

### Bug 2 — CRITICAL: bbGetTranscript double-encoding

**File**: `AssistantPanel.cs:940`, `FrmAssistantWindow.cs:458`  
**Problem**: `ExecuteScriptAsync` already JSON-encodes the JS return value. Wrapping the JS call in `JSON.stringify(...)` produces doubly-encoded JSON. When this is assigned to `transcript` and later embedded in `session.ToString()`, the transcript value is a string containing escaped JSON rather than a proper JSON array.  
**Fix**:
1. Remove `JSON.stringify(...)` wrapper from the `ExecuteScriptAsync` call.
2. Change from: `"JSON.stringify(window.bbGetTranscript ? window.bbGetTranscript() : []);"`
3. Change to: `"window.bbGetTranscript ? window.bbGetTranscript() : [];"`
4. `ExecuteScriptAsync` returns a JSON-encoded string. Parse it with `JArray.Parse(result)` to get the actual array.
5. Assign the parsed `JArray` to the `transcript` property of the session object (instead of assigning the raw string).

**Verification**: Unit test that calls `bbGetTranscript` via mock WebView2, asserts the returned value is a valid `JArray` with the expected structure.

### Bug 3 — SUBSTANTIAL: SSE line splitting across buffer boundaries

**File**: `AgentPanelClient.cs:103-124`  
**Problem**: Raw buffer chunks are split by `\n` without maintaining a line buffer. If a `data:` line is split across two reads, partial JSON gets parsed and fails. The `chunk.Split('\n')` on line 109 treats each buffer fragment independently.  
**Fix**:
1. Add a `StringBuilder lineBuffer` before the `while` loop (around line 102).
2. After reading each chunk, append it to `lineBuffer`.
3. Extract complete lines: find the last `\n` in the buffer. Process all complete lines (up to and including the last `\n`). Keep the remainder in `lineBuffer` for the next read.
4. After the loop ends, process any remaining content in `lineBuffer` as a final line.

**Verification**: Unit test that feeds data in small chunks (e.g., 5 bytes at a time) containing `data:` lines split across buffer boundaries. Assert all lines are parsed correctly.

### Bug 4 — SUBSTANTIAL: Non-SSE response message.Text extraction

**File**: `AssistantPanel.cs:694`, `FrmAssistantWindow.cs:249`  
**Problem**: The server returns a single JSON blob `{ sessionId, assistantAvailable, error, message: { Role, Text, ... } }`, not SSE. The client's `onChunk` callback tries `jObj["text"]` → `jObj["content"]` → `jObj["delta"]["content"]` but the actual text is at `message.Text`, so `textChunk` is always null and raw JSON gets appended to chat.  
**Fix**:
1. Add `jObj["message"]?["Text"]?.ToString()` as the **first** extraction attempt in the `onChunk` callback, before the existing `jObj["text"]` fallback chain.
2. Apply to both files:
   - `AssistantPanel.cs:694`: change to `var textChunk = jObj["message"]?["Text"]?.ToString() ?? jObj["text"]?.ToString() ?? jObj["content"]?.ToString() ?? jObj["delta"]?["content"]?.ToString();`
   - `FrmAssistantWindow.cs:249`: same change.

**Verification**: Unit test with mock server returning the non-SSE JSON blob format. Assert `onChunk` extracts the text from `message.Text`.

### Bug 5 — MINOR: Double-serialize anti-pattern (10+ call sites)

**Files**: `AssistantPanel.cs`, `FrmAssistantWindow.cs`  
**Problem**: `JsonConvert.SerializeObject(payload.ToString(Formatting.None))` double-serializes. `payload.ToString(Formatting.None)` already produces valid JSON. Wrapping it in `JsonConvert.SerializeObject` encodes the entire JSON string as a JSON string literal, adding extra quotes and escaping.  
**Fix**: Replace `JsonConvert.SerializeObject(payload.ToString(Formatting.None))` with `payload.ToString(Formatting.None)` at all call sites.

**Call sites in `AssistantPanel.cs`**:
| Line | Method | Current | Fixed |
|------|--------|---------|-------|
| 195 | `RefreshStatusAsync` (bbSetStatus) | `JsonConvert.SerializeObject(uiState.ToString(Formatting.None))` | `uiState.ToString(Formatting.None)` |
| 248 | `LoadToolsAsync` (bbSetTools) | `JsonConvert.SerializeObject(_toolCatalog.ToString(Formatting.None))` | `_toolCatalog.ToString(Formatting.None)` |
| 263 | `LoadToolAuditAsync` (bbSetToolReceipts) | `JsonConvert.SerializeObject(_toolReceipts.ToString(Formatting.None))` | `_toolReceipts.ToString(Formatting.None)` |
| 284 | `LoadProductCatalogsAsync` (bbSetProductCatalogs) | `JsonConvert.SerializeObject(payload.ToString(Formatting.None))` | `payload.ToString(Formatting.None)` |
| 524 | `AppendToolResultAsync` | `JsonConvert.SerializeObject(payload.ToString(Formatting.None))` | `payload.ToString(Formatting.None)` |
| 547 | `AppendScreenshotArtifactAsync` | `JsonConvert.SerializeObject(payload.ToString(Formatting.None))` | `payload.ToString(Formatting.None)` |
| 905 | `AppendMessageAsync` | `JsonConvert.SerializeObject(payload.ToString(Formatting.None))` | `payload.ToString(Formatting.None)` |

**Call sites in `FrmAssistantWindow.cs`**:
| Line | Method | Current | Fixed |
|------|--------|---------|-------|
| 423 | `AppendMessageAsync` | `JsonConvert.SerializeObject(payload.ToString(Formatting.None))` | `payload.ToString(Formatting.None)` |

**Verification**: Grep for `JsonConvert.SerializeObject.*\.ToString\(Formatting\.None\)` — must return zero matches after fix.

### Bug 6 — MINOR: LogErrorAsync not concurrency-safe

**Files**: `AssistantPanel.cs:927`, `FrmAssistantWindow.cs:445`  
**Problem**: `File.AppendAllText` can throw `IOException` under concurrent access (e.g., two streaming errors logged simultaneously).  
**Fix**:
1. Add a static `readonly object _logLock = new object();` field to each class.
2. Wrap the `File.AppendAllText` call in `lock (_logLock) { ... }`.
3. Since this is inside `Task.Run`, use `lock` synchronously (the lock is held briefly for file I/O).

**Verification**: Unit test that concurrently calls `LogErrorAsync` from multiple threads. Assert no `IOException` thrown.

---

## 7. P0 Gate Closure Specifications

### P0.1 — Source-of-Truth (currently FAIL)

**Current state**: All assistant work is local-only untracked WIP.  
**Closure criteria**:
1. `git add -A` all files in the Bluebrick directory.
2. `git commit -m "baseline: P0 assistant WIP + governance docs + test infrastructure"`.
3. Verify `git status` shows clean working tree.
4. Update `BLUEBRICK_SOURCE_OF_TRUTH_PROOF.md` with the baseline commit hash.

### P0.2 — Cancellation Regression (currently PARTIAL)

**Current state**: `AssistantErrorClassifier` classifies cancel/timeout errors. Request-local CTS exists. No regression tests.  
**Closure criteria**:
1. Bug 1 fixed (per-chunk idle timeout in `PostStreamingAsync`).
2. Bug 3 fixed (SSE line buffer — prevents partial-parse exceptions that bypass cancellation).
3. Add cancellation regression tests to `LabWorkspaceTests.cs`:
   - Test: Cancel mid-stream → `OperationCanceledException` propagates, no hang.
   - Test: Idle timeout mid-stream → timeout exception (not user cancellation).
   - Test: Cancel before response headers → immediate `OperationCanceledException`.

### P0.3 — Model Authority ADR (currently CREATED)

**Current state**: `docs/BLUEBRICK_AIONUI_MODEL_AUTHORITY_ADR.md` exists.  
**Closure criteria**: Verify the ADR covers: (a) model selection authority, (b) model capability flags, (c) vision gating rule, (d) broker sync intent. If any gap, update. Otherwise, mark PASS.

### P0.4 — Model Profile Contract (currently PARTIAL)

**Current state**: `AssistantModelProfile` has capability flags, vision gating.  
**Closure criteria**: Verify `AssistantModelProfile` in code has: `Id`, `Provider`, `SupportsVision`, and the vision gating logic in `AssistantPanel.cs:574-600` is functional. If complete, mark PASS.

### P0.5 — WebView2 Security (currently PARTIAL)

**Current state**: `AssistantWebViewSecurity.cs` has navigation guard + token scan.  
**Closure criteria**:
1. Verify `NavigationStarting` handler calls `AssistantWebViewSecurity.IsNavigationAllowed` (confirmed at `AssistantPanel.cs:72`).
2. Verify `NewWindowRequested` is handled (confirmed at `AssistantPanel.cs:78`).
3. Verify DevTools disabled, context menus disabled, host objects denied, web messages disabled (confirmed at `AssistantPanel.cs:64-67`).
4. Add test: attempt navigation to disallowed URL → blocked.
5. Mark PASS.

### P0.6 — Bridge Security (currently PARTIAL)

**Current state**: Auth header exists on all routes. Route manifest created. No body-size limit. No security tests.  
**Closure criteria**:
1. Add body-size limit (1 MB) to `AgentHttpServer.cs`. Add a constant `MaxRequestBodyBytes = 1_048_576`. At the start of request handling, check `Content-Length` header; if exceeded, return 413.
2. Verify auth header enforcement on all routes.
3. Add test: POST with body > 1 MB → 413.
4. Add test: POST without `X-Agent-Auth` header → 401.
5. Mark PASS.

### P0.7 — Tool Policy (currently PARTIAL)

**Current state**: Policy denies `/sw/*`, native `/pdm/*`, `/lab/vault/reset`, unknown mutation routes. No live route-level validation tests.  
**Closure criteria**:
1. Verify deny rules in `AssistantToolPolicy.cs` match the route manifest.
2. Add test: POST to denied route → 403 with policy denial.
3. Add test: POST to allowed route → passes policy check.
4. Mark PASS.

---

## 8. Code Style

- **No code comments** in source files (standing rule).
- Follow existing C# conventions in the codebase: `internal`/`private` modifiers, `ConfigureAwait(false)` on async calls in library code, `using` statements for disposable resources.
- Use `StringBuilder` for buffer operations (Bug 3).
- Use `lock` for concurrency safety (Bug 6) — no `SemaphoreSlim` needed for brief file I/O.
- All new test methods follow the existing `LabWorkspaceTests.cs` pattern: `[TestMethod]` attribute, `async Task` return type, descriptive names.

---

## 9. Testing Strategy

### Unit Tests (add to `BlueBrick.UI.Tests/LabWorkspaceTests.cs`)

| Test Name | Bug/Gate | Asserts |
|-----------|----------|---------|
| `PostStreaming_IdleTimeout_ThrowsTimeoutException` | Bug 1 | Timeout exception thrown within 32s |
| `PostStreaming_UserCancel_ThrowsOperationCanceled` | Bug 1, P0.2 | OCE propagates immediately |
| `PostStreaming_UserCancel_BeforeHeaders_ThrowsOperationCanceled` | Bug 1, P0.2 | OCE before any data |
| `SseLineSplit_AcrossBufferBoundaries_ParsedCorrectly` | Bug 3 | All data lines extracted |
| `NonSseResponse_ExtractsMessageText` | Bug 4 | `message.Text` extracted as first attempt |
| `DoubleSerialize_NotPresentAfterFix` | Bug 5 | Grep returns zero matches |
| `LogError_ConcurrentAccess_NoIOException` | Bug 6 | No exception from 10 concurrent calls |
| `WebView_NavigationBlocked_ForDisallowedUrl` | P0.5 | Navigation cancelled |
| `Bridge_BodySizeLimit_Returns413` | P0.6 | 413 for >1MB body |
| `Bridge_AuthRequired_Returns401` | P0.6 | 401 without auth header |
| `ToolPolicy_DeniedRoute_Returns403` | P0.7 | 403 with denial |
| `ToolPolicy_AllowedRoute_PassesCheck` | P0.7 | Request succeeds |

### Verification Commands

```powershell
# Build
dotnet build BlueBrick.csproj -c Lab -v minimal

# Run all tests
dotnet test BlueBrick.UI.Tests -c Debug

# Verify no double-serialize remains
rg "JsonConvert\.SerializeObject.*\.ToString\(Formatting\.None\)" Agent/ AssistantPanel.cs FrmAssistantWindow.cs
```

---

## 10. Boundaries

| Boundary | Rule |
|----------|------|
| No server-side SSE conversion | Bug 4 is client-side extraction only; server continues returning single JSON blob |
| No approval UI | P0.7 is policy + route test only; approval workflow is a future phase |
| No AionUI broker integration | P0.4 is verification of existing model profile contract |
| No design tool changes | TS tasks 7-9 remain deferred |
| No SolidWorks API changes | Bridge runs inside SolidWorks; no SDK modifications |
| Build dependency | VS Build Tools 2022 must be installed before any compile/test |
| Deploy only after clean build | `deploy_bluebrick_safe.ps1` runs only after `dotnet build` succeeds |

---

## 11. Success Criteria

1. All 7 P0 gates marked PASS in `AI_ASSISTANT_IMPLEMENTATION_AND_VERIFICATION_PLAN.md`
2. All 6 bugs verified fixed (code + test)
3. Zero `JsonConvert.SerializeObject(X.ToString(Formatting.None))` patterns remain
4. All new + existing tests pass: `dotnet test BlueBrick.UI.Tests -c Debug`
5. Working tree is clean: `git status` shows no untracked or modified files
6. `BLUEBRICK_SOURCE_OF_TRUTH_PROOF.md` updated with baseline commit hash

---

## 12. Open Questions

1. **VS Build Tools 2022 installation**: The installer is at `C:\Users\cweir\Downloads\vs_buildtools.exe` but NOT yet installed. Should I proceed with silent install, or does the user want to install it manually?
2. **P0.1 git baseline commit**: Should the baseline commit include only the assistant-related files, or all untracked WIP in the repo (including any non-assistant files)?
3. **P0.6 body-size limit value**: 1 MB (1,048,576 bytes) is proposed. Is this appropriate, or should it be higher/lower?
4. **Bug 4 — future SSE**: The server currently returns a single JSON blob, not SSE. Should a follow-up task be created to add proper SSE streaming to `AgentHttpServer.cs`, or is the client-side extraction fix sufficient indefinitely?
5. **Live testing**: After build + deploy, should I launch SolidWorks to verify the fixes in a live environment, or is unit test verification sufficient for now?
