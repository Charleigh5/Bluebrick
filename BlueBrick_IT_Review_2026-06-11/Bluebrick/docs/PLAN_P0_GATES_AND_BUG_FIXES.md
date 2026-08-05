# IMPLEMENTATION PLAN: P0 Gates + 6 Bug Fixes

**Version**: 1.0  
**Date**: 2026-05-28  
**Status**: DRAFT — awaiting human approval  
**Derived from**: `docs/SPEC_P0_GATES_AND_BUG_FIXES.md`  

---

## Execution Order

The 6 bugs and 7 P0 gates are ordered by dependency: bug fixes first (they unblock gate closure), then gates from lowest to highest effort.

**Phase A — Prerequisites** (must complete before any code changes)
1. Install VS Build Tools 2022
2. Verify build succeeds with current code
3. P0.1: Git baseline commit

**Phase B — Bug Fixes** (ordered by severity + dependency)
4. Bug 1: PostStreamingAsync read timeout
5. Bug 3: SSE line buffer (shares PostStreamingAsync code with Bug 1)
6. Bug 4: Non-SSE message.Text extraction
7. Bug 5: Double-serialize anti-pattern (touches all ExecuteScriptAsync sites)
8. Bug 2: bbGetTranscript double-encoding (shares ExecuteScriptAsync pattern with Bug 5)
9. Bug 6: LogErrorAsync concurrency safety

**Phase C — P0 Gate Closure** (ordered by effort)
10. P0.3: Verify Model Authority ADR completeness
11. P0.4: Verify Model Profile Contract completeness
12. P0.5: WebView2 Security — add navigation test
13. P0.6: Bridge Security — add body-size limit + tests
14. P0.7: Tool Policy — add route validation tests
15. P0.2: Cancellation Regression — add tests (depends on Bug 1 + Bug 3)

**Phase D — Verification**
16. Run full test suite
17. Verify no double-serialize patterns remain
18. Update governance docs (source-of-truth proof, verification plan)

---

## Step-by-Step Details

### Step 1: Install VS Build Tools 2022

**What**: Silent install of VS Build Tools 2022 from the downloaded installer.  
**Why**: MSBuild is required to compile the .NET Framework 4.8 project. The installer is at `C:\Users\cweir\Downloads\vs_buildtools.exe` but not yet installed.  
**How**:
```powershell
Start-Process -Wait -FilePath "C:\Users\cweir\Downloads\vs_buildtools.exe" -ArgumentList "--quiet","--wait","--norestart","--nocache","--add","Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools","--add","Microsoft.VisualStudio.Workload.WebBuildTools","--add","Microsoft.Net.Component.4.8.SDK","--add","Microsoft.Net.Component.4.8.TargetingPack"
```
**Verify**: `& "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" -version`

### Step 2: Verify current build

**What**: Build the project with existing code to establish a baseline.  
**How**: `dotnet build BlueBrick.csproj -c Lab -v minimal`  
**Verify**: Build succeeds (even with warnings — only errors block progress).

### Step 3: P0.1 — Git baseline

**What**: Commit all assistant-related WIP to the `main` branch.  
**How**:
```powershell
git add -A
git commit -m "baseline: P0 assistant WIP + governance docs + test infrastructure"
```
**Verify**: `git status` shows clean working tree. Record commit hash.

### Step 4: Bug 1 — PostStreamingAsync read timeout

**File**: `Agent/AgentPanelClient.cs:90-128`  
**Change**: Inside `PostStreamingAsync`, add a per-chunk idle timeout:
```
Before the while loop (line ~102):
  var idleCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
  var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, idleCts.Token);
  var linkedToken = linkedCts.Token;

Replace line 104: while (!cancellationToken.IsCancellationRequested)
  with: while (!linkedToken.IsCancellationRequested)

Replace line 106: var count = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
  with: idleCts.CancelAfter(TimeSpan.FromSeconds(30));
        var count = await reader.ReadAsync(buffer, 0, buffer.Length, linkedToken).ConfigureAwait(false);

Add after the while loop (before existing using dispose):
  linkedCts.Dispose();
  idleCts.Dispose();

In the catch block for the streaming operation (the caller in AssistantPanel.cs and FrmAssistantWindow.cs 
already catch OperationCanceledException — no change needed there; the linked token ensures 
idle-timeout also triggers cancellation).
```
**Risk**: Low. Linked CTS propagates either user cancel or idle timeout.

### Step 5: Bug 3 — SSE line buffer

**File**: `Agent/AgentPanelClient.cs:103-124`  
**Change**: Add `StringBuilder lineBuffer` and process only complete lines:
```
Before the while loop (line ~102):
  var lineBuffer = new StringBuilder();

Replace lines 108-123:
  var chunk = new string(buffer, 0, count);
  lineBuffer.Append(chunk);
  var content = lineBuffer.ToString();
  var lastNewline = content.LastIndexOf('\n');
  if (lastNewline < 0) continue;
  var processable = content.Substring(0, lastNewline + 1);
  lineBuffer.Remove(0, lastNewline + 1);
  var lines = processable.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
  foreach (var line in lines) { ... existing line processing logic ... }

After the while loop, process remaining buffer:
  var remaining = lineBuffer.ToString().Trim();
  if (remaining.Length > 0) { process remaining as a line }
```
**Risk**: Low. Purely additive — doesn't change line processing logic.

### Step 6: Bug 4 — Non-SSE message.Text extraction

**Files**: `AssistantPanel.cs:694`, `FrmAssistantWindow.cs:249`  
**Change**: Add `message.Text` as first extraction attempt in `onChunk`:
```
Current: var textChunk = jObj["text"]?.ToString() ?? jObj["content"]?.ToString() ?? jObj["delta"]?["content"]?.ToString();
Fixed:   var textChunk = jObj["message"]?["Text"]?.ToString() ?? jObj["text"]?.ToString() ?? jObj["content"]?.ToString() ?? jObj["delta"]?["content"]?.ToString();
```
**Risk**: Very low. Additive fallback — existing extraction paths still work if `message.Text` is absent.

### Step 7: Bug 5 — Double-serialize anti-pattern

**Files**: `AssistantPanel.cs` (7 sites), `FrmAssistantWindow.cs` (1 site)  
**Change**: Replace `JsonConvert.SerializeObject(X.ToString(Formatting.None))` with `X.ToString(Formatting.None)` at all 8 sites listed in the spec.  
**Risk**: Low. `ExecuteScriptAsync` expects a JavaScript expression that evaluates to the argument. A JSON object literal is a valid JS expression. The double-encoding was causing `bbAppend` to receive a string instead of an object.

### Step 8: Bug 2 — bbGetTranscript double-encoding

**Files**: `AssistantPanel.cs:940`, `FrmAssistantWindow.cs:458`  
**Change**:
```
Current:  var transcript = await _webView.ExecuteScriptAsync("JSON.stringify(window.bbGetTranscript ? window.bbGetTranscript() : []);");
          ... ["transcript"] = transcript ?? "[]"

Fixed:    var transcriptRaw = await _webView.ExecuteScriptAsync("window.bbGetTranscript ? window.bbGetTranscript() : [];");
          JArray transcript;
          try { transcript = JArray.Parse(transcriptRaw); } catch { transcript = new JArray(); }
          ... ["transcript"] = transcript
```
**Risk**: Low. `ExecuteScriptAsync` returns a JSON-encoded string. `JArray.Parse` decodes it back to a proper array. The session JSON then contains a real array instead of an escaped string.

### Step 9: Bug 6 — LogErrorAsync concurrency safety

**Files**: `AssistantPanel.cs:908-930`, `FrmAssistantWindow.cs:426-448`  
**Change**:
```
Add field: private static readonly object _logLock = new object();

Change: await Task.Run(() => File.AppendAllText(path, line + Environment.NewLine));
To:     await Task.Run(() => { lock (_logLock) { File.AppendAllText(path, line + Environment.NewLine); } });
```
**Risk**: Very low. The lock is held for <1ms per write.

### Step 10: P0.3 — Verify Model Authority ADR

**What**: Read `docs/BLUEBRICK_AIONUI_MODEL_AUTHORITY_ADR.md` and verify it covers: (a) model selection authority, (b) model capability flags, (c) vision gating rule, (d) broker sync intent.  
**Action**: If any gap, update. Otherwise, mark PASS in the verification plan.

### Step 11: P0.4 — Verify Model Profile Contract

**What**: Verify `AssistantModelProfile` (or equivalent) in code has `Id`, `Provider`, `SupportsVision` fields and that vision gating logic exists in `AssistantPanel.cs:574-600`.  
**Action**: If complete, mark PASS.

### Step 12: P0.5 — WebView2 Security test

**What**: Add a unit test to `LabWorkspaceTests.cs` that verifies `AssistantWebViewSecurity.IsNavigationAllowed` blocks disallowed URLs.  
**Test**: `WebView_NavigationBlocked_ForDisallowedUrl` — call `IsNavigationAllowed("https://evil.com")` → `false`, `IsNavigationAllowed("about:blank")` → `true`.

### Step 13: P0.6 — Bridge Security (body-size limit + tests)

**File**: `Agent/AgentHttpServer.cs`  
**Change**: Add body-size check in `HandleRequest` before line 301:
```
After line 158 (origin check block), before line 160:
  if (method == "POST")
  {
      var contentLength = context.Request.ContentLength64;
      if (contentLength > MaxRequestBodyBytes)
      {
          context.Response.StatusCode = 413;
          await WriteJson(context, new { error = "Request body exceeds maximum allowed size", maxBytes = MaxRequestBodyBytes, traceId });
          return;
      }
  }

Add constant to class:
  private const long MaxRequestBodyBytes = 1_048_576;
```
**Tests**:
- `Bridge_BodySizeLimit_Returns413` — POST with `Content-Length: 2_097_152` → 413
- `Bridge_AuthRequired_Returns401` — POST without `X-Agent-Auth` → 403 (note: code uses 403, not 401, for missing auth — will document this)

### Step 14: P0.7 — Tool Policy tests

**What**: Add unit tests that verify `AssistantToolPolicy` denies blocked routes and allows permitted ones.  
**Tests**:
- `ToolPolicy_DeniedRoute_Returns403` — POST to `/sw/execute` → policy denies
- `ToolPolicy_AllowedRoute_PassesCheck` — POST to `/assistant/tool` with valid tool → policy allows

### Step 15: P0.2 — Cancellation Regression tests

**What**: Add 3 cancellation regression tests to `LabWorkspaceTests.cs` using the existing `TestHttpServer`.  
**Tests**:
- `PostStreaming_UserCancel_ThrowsOperationCanceled` — cancel token mid-stream → OCE
- `PostStreaming_IdleTimeout_ThrowsTimeoutException` — server stalls >30s → timeout
- `PostStreaming_UserCancel_BeforeHeaders_ThrowsOperationCanceled` — cancel before data → immediate OCE

**Note**: These tests require the Bug 1 and Bug 3 fixes to be in place.

### Step 16: Full test suite

**How**: `dotnet test BlueBrick.UI.Tests -c Debug`  
**Verify**: All existing + new tests pass.

### Step 17: Verify no double-serialize remains

**How**: Search for `JsonConvert.SerializeObject.*.ToString(Formatting.None)` across all C# files. Must return zero matches.

### Step 18: Update governance docs

1. Update `BLUEBRICK_SOURCE_OF_TRUTH_PROOF.md` with the baseline commit hash from Step 3.
2. Update `AI_ASSISTANT_IMPLEMENTATION_AND_VERIFICATION_PLAN.md` — mark all P0 gates as PASS.
3. Final commit: `git add -A && git commit -m "fix: 6 C# bugs + close all P0 gates"`

---

## Dependency Graph

```
Step 1 (VS Build Tools) → Step 2 (Build) → Step 3 (Git baseline)
                                                   ↓
Step 4 (Bug 1) → Step 5 (Bug 3) ──────────────────┤
Step 6 (Bug 4)                                    ├── All bugs done
Step 7 (Bug 5) → Step 8 (Bug 2)                   ┤
Step 9 (Bug 6)                                    ↓
                                        Step 10-14 (P0.3-P0.7 gates)
                                                   ↓
                                        Step 15 (P0.2 — depends on Bug 1+3)
                                                   ↓
                                        Step 16-18 (Verification + docs)
```

**Parallelizable**: Steps 4-9 (bug fixes) can be implemented sequentially in the same edit pass since they touch different code regions. Steps 10-14 (P0 gates) are mostly verification and can run in parallel.

---

## Rollback Plan

Each step is a discrete commit. If any step introduces a regression:
1. `git diff HEAD` to identify the change
2. `git revert HEAD` to undo the last commit
3. Fix the issue and re-commit

The git baseline (Step 3) ensures we always have a known-good state to return to.
