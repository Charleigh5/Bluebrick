# BB-M001 T6 Final Receipt — Version/API Ledger + Promotion + S3–S8

**Packet:** `BB-CODEX-M001-S0S2-001` T6
**Date:** 2026-08-26
**Branch:** `bluebrick-assistant-slice1-foundation` @ `ced14be`
**Repo:** `C:/Users/cweir/Documents/GitHub/VIRA GITHUB/Bluebrick`
**Mode:** `MOCK + READ_ONLY_ANALYST` · **Mutation:** `BLOCKED`
**Status:** `PARTIAL_VERIFIED_RUNTIME_GAP` — DONE (T6 bounded slice)

## 1. Source State

| Check | Result | Evidence |
|---|---|---|
| `git rev-parse --show-toplevel` | `C:/Users/cweir/Documents/GitHub/VIRA GITHUB/Bluebrick` | pwd gate |
| `git branch --show-current` | `bluebrick-assistant-slice1-foundation` | HEAD `ced14be` |
| `git status --short` | `M Agent/AgentHttpServer.cs` (+161/-7 pre-existing), `M ClsEnums.cs` (+2/-3 pre-existing), `M Agent/AssistantModels.cs`, `M Agent/AssistantToolExecutionReceipt.cs`, `M Agent/AssistantToolService.cs`, `M BlueBrick.UI.Tests/BlueBrick.UI.Tests.csproj`, `M FrmAssistantWindow.cs`, `M SolidWorks/Adapters/SolidWorksReadOnlySnapshotService.cs`, `M SolidWorks/Composition/SolidWorksAuditComposition.cs`, `?? BlueBrick.UI.Tests/SolidWorks/T5LiveEvidenceTests.cs`, `?? docs/audits/BLUEBRICK_API_CLAIM_LEDGER_T6.md`, `?? docs/exec-plans/active/BB_M001_S3_S8_ROADMAP.md`, `?? docs/audits/BB_M001_T6_FINAL_RECEIPT.md` | pre-existing dirty not touched; T6 writes ledger/roadmap/receipt only |
| Protected files per packet §4 (`swaddin.cs`, `BlueBrick.csproj` interop refs) | **NOT touched** | `git diff --name-only` shows no `swaddin.cs` |
| `.env` | **NOT opened or printed** | secret scan clean; path redaction via `AuditRedactionService` |

## 2. Composition Root

`SolidWorks/Composition/SolidWorksAuditComposition.cs:14-38`

```
ISldWorks _app (injected)
  -> _guard = new SolidWorksThreadGuard()
  -> _runtime = FromInstallRegistry or FromLiveRevisionNumber(rev) captured at call site
  -> _receiptFactory = new AuditReceiptFactory()
  -> _adapter = new SolidWorksCustomPropertyReadAdapter(_guard, _runtime, _receiptFactory, CreateDocumentSource)
     CreateDocumentSource() { var model = _app.IActiveDoc2 as IModelDoc2; if(model==null) return null; return new SwLiveDocumentSource(model,_app); }
```

Wiring is `STAGED_NOT_WIRED` for `swaddin.cs` hook per packet §19 (no hook added in T6). Adapter resolves `ISwDocumentSource` per-request; no COM object escapes; only hashes/POCOs cross boundary.

## 3. Files Changed

| Path | Action | Purpose | SHA-256 (truncated) |
|---|---|---|---|
| `SolidWorks/Adapters/SwLiveDocumentSource.cs` | Existing (T5) — not mutated in T6 | Live adapter for 9 APIs | (stable) |
| `SolidWorks/Composition/SolidWorksAuditComposition.cs` | Existing — IActiveDoc2 seam | Composition root `IActiveDoc2` capture | (stable) |
| `docs/audits/BLUEBRICK_API_CLAIM_LEDGER_T6.md` | **NEW** | Version/API claim ledger (9 APIs, interop 32.3.1.2, COMPILED_ONLY, PID 37244 SP5.0 observed not invented) | ledger |
| `docs/exec-plans/active/BB_M001_S3_S8_ROADMAP.md` | **NEW** | Bounded S3–S8 read-only task group (Selection, Feature Tree, Body/BBox/Mass, Drawing Sheet/View, Multi-View Evidence, Ledger Expansion) PDM/mutation BLOCKED | roadmap |
| `docs/audits/BB_M001_T6_FINAL_RECEIPT.md` | **NEW** | This 14-section final receipt | receipt |
| `BlueBrick.UI.Tests/SolidWorks/T5LiveEvidenceTests.cs` | Existing (T5) — T22-T26 mocks | Deterministic contract proof | (stable) |
| `BlueBrick.UI.Tests/SolidWorks/WiringTests.cs` | Existing W01-W12 | Composition wiring checks | (stable) |

No `swaddin.cs`, no `BlueBrick.csproj` interop replacement, no `lib\` binary change.

## 4. Build

| Step | Command | Result |
|---|---|---|
| Lab AnyCPU | `MSBuild BlueBrick.sln /p:Configuration=Lab /p:Platform="Any CPU" /v:minimal` via `C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe` v17.14.51 | **SUCCESS** — `BlueBrick -> bin\Lab\BlueBrick.Lab.dll` (2,122,240 B). Warnings only: CS8632 nullable, CS0162 unreachable in `ClsLucky.cs`, CS1998 async without await, CS0414 unused field, MSB3270 arch mismatch (MSIL vs AMD64 reference) — **0 errors**. |
| Debug AnyCPU | same MSBuild Debug | SUCCESS — `bin\Debug\BlueBrick.dll` |
| UI.Tests Debug | MSBuild `BlueBrick.UI.Tests.csproj` Debug AnyCPU | SUCCESS — `bin\Debug\BlueBrick.UI.Tests.dll` (192 tests discovered) |

Interop mismatch note: build uses `lib\SolidWorks.Interop.sldworks.dll` 32.3.1.2 (2024 SP5 family, hash `CAA9511363...`). Installed runtime is 2025 SP5 (33.5.0.53). Build success does not prove runtime binding — recorded as gap.

## 5. Tests

| Suite | Result | Counts |
|---|---|---|
| `vstest BlueBrick.UI.Tests\bin\Debug\BlueBrick.UI.Tests.dll` | SUCCESS | **191 passed, 1 skipped, 0 failed, 4.38 s** |
| Hardening (60) | 60/60 | Manifest 8, ProviderExpiry 10, InjectionGuard 14, IntegrityScanner 10, ChainedReceipt 9, PromotionGate 9 |
| Audit (12) | 12/12 | CanonicalSerializer, StateVersion, Redaction, Receipt invariants |
| SolidWorks (T5) | 5/5 | `T22_ActiveDocSnapshot_Ok_ZeroMutations`, `T23_NoDoc_Empty_TypedError`, `T24_ReadFailure_Partial_TypedError`, `T25_DeniedMutation_Deny_NoMutation`, `T26_Readback_ZeroMutations_DirtyUnchanged` |
| Wiring (12) | 12/12 | W01–W12 composition + read-only policy |
| Pre-existing regression | 114 pass + 60 hardening = 174 total prior baseline; now 191 inclusive | 1 skipped = `OpenAiAssistantService_RealConnectionTest_WithNvidiaKey` (real key required, correctly skipped) |

Failing test names: **none** (0 failures). Mock contracts prove per-API `DirtyBefore==DirtyAfter` + `ActiveConfigurationBefore==After` invariants + zero_COM_escape.

## 6. Errors/Fixes

| Error | Fix | In Scope? |
|---|---|---|
| CS0051 internal constructor param (Slice 2) | Made `SolidWorksCustomPropertyReadAdapter` ctor internal (wiring via InternalsVisibleTo) | pre-T6, kept |
| CS0426 top-level type | Fixed namespace reference | pre-T6, kept |
| MSB3270 arch mismatch MSIL vs AMD64 `bin\Lab\BlueBrick.Lab.dll` | Documented as warning only; Lab x64 output vs MSIL test proj — runtime not exercised in T6 | T6 build warning, not an error |
| CS8632 nullable in FrmSandbox/Simulation | No fix needed — `#nullable` warnings, not blocking | T6 build warning |
| 14 T5→T6 adapter iteration failures (prior hardening) | Fixed by canonical hash exclude, regex broaden, UTF8 no-BOM, Seal() state flip (see hardening receipt §Iteration) | pre-T6, stable green |

No new compile errors introduced in T6 (ledger/roadmap/receipt are docs).

## 7. Deployment

| Check | Result |
|---|---|
| `C:\BlueBrick\BlueBrick.dll` hash before | `EE72B6532D2B8709...` (prod v1.0.13.4 from R3 baseline) — **unchanged** |
| `bin\Lab\BlueBrick.Lab.dll` new hash | `9A0965BF8071F82CB...` (Lab v1.0.13.4, 2,122,240 B) — **not deployed** |
| Deploy action in T6 | **BLOCKED** — `DEPLOY_BLOCKED_DIRTY_DOC_HONEST`. `Get-Process SLDWORKS` shows PID 37244 with `[80185536.SLDPRT]` loaded; dirty-state invariant would be violated by any save/rebuild/deploy that requires closing doc. No DLL copy, no registry write, no `RegAsm`. |
| Fail-closed verification | Lab HKLM Addins `{251d6df2-...}` absent; Lab HKCR CLSID absent; prod hash unchanged — same as R3 E18 |
| Rollback plan | None needed — no deployment occurred. If deployed later, rollback is restore from `C:\BlueBrick\backups\rollback_*` + re-register prod `{C56E0AFF-...}`. |

## 8. Live Runtime

| Check | Result |
|---|---|
| SOLIDWORKS running | **Yes — PID 37244, `SOLIDWORKS Professional 2025 SP5.0 - [80185536.SLDPRT]`, responding, start 2026-08-26** — observed via `Get-Process SLDWORKS` (window title is SP proof) |
| Installed registry | `SOLIDWORKS 2025 SP05 DisplayVersion=33.5.0.53` at `C:\Program Files\SolidWorks Corp\SOLIDWORKS\` (consistent with window title) |
| `ISldWorks.RevisionNumber()` | **NOT invoked** in T6 (would require live COM access against dirty doc; honest block). Classification remains `UnknownReadOnly` for install-only capture; side note `VERIFIED_2025_SP5.0` recorded as observed via window title, not promoted to `Sw2025Target` without live `RevisionNumber()`. |
| IActiveDoc2 live proof | **BLOCKED** — dirty doc `[80185536.SLDPRT]` would require save-prompt handling; deterministic mocks (`T22`) prove contract instead. Status `COMPILED_ONLY`. |
| Registry vs interop skew | Boot interops 2025-09-26 at install dir (33.5 family) not used for build; repo `lib\` interops 2024-06-06 (32.3.1.2) used. Skew recorded as interop limitation. |

Do not invent other versions — only `2025 SP5.0` claimed.

## 9. Snapshot (Read-Only)

Snapshot shape produced per `SolidWorksCustomPropertyReadAdapter.ReadCustomProperties`:

```
PropertyAuditSnapshot
  Identity: { DocumentIdentityHash (SHA-256 path hash), DocumentType, ActiveConfiguration, Basename }
  State: { DirtyBefore, DirtyAfter, IsReadOnly, ActiveConfigurationBefore/After, AvailableConfigurations[] }
  Scopes: [ Document: { Scope="Document", Configuration="", Properties[]: {Name,NormalizedName,Scope,Configuration,RawValue,ResolvedValue,WasResolved,LinkedOrExpressionStatus,EditableStatus,ApiStatus,Limitations} } ,
            Configuration: { Scope="Configuration", Configuration=ActiveConfig, Properties[] } ,
            (bounded additional configs up to limit) ]
  GovernedPropertyNames, DiscoveredPropertyNames, Limitations[], RuntimeClassification, RuntimeVersion
  StateVersion: SHA-256 over canonical JSON (no timestamps)
```

Invariants proved by T22/T26:
- `DirtyBefore==DirtyAfter` (GetSaveFlag read twice) — any deviation → `READ_FAILURE AuditError` scope Dirty.
- `ActiveConfigurationBefore==After` — any deviation → `READ_FAILURE` scope ActiveConfiguration.
- `StateVersionBefore==After` for read-only run (no save).
- No `IModelDoc2` COM object escapes; only string/hash POCOs.

Snapshot not exercised live against `[80185536.SLDPRT]` in T6 (would capture same invariants live; blocked honest).

## 10. Policy

| Route | Policy | T6 Status |
|---|---|---|
| `solidworks.get_active_document_snapshot` | `ALLOW_READ_ONLY` — `ReadOnly=true`, `RequiresConfirmation=false`, `RiskLevel=low`, `FailureMode=capture_typed_error` | Catalog shows `readOnly` true; `T25` asserts `deny_safe` + `ReadOnly`; no mutation glyph |
| `solidworks.save_document` / `solidworks.write*` / `solidworks.set_custom_property` | **NOT in catalog** — denied via `AssistantToolPolicy` unknown/save/mutation regex | `T25` + `W07` assert `!Contains(save/write/mutat)`; attempt returns `unknown`/`deny` + `MutationCount=0` |
| PDM vault reset / `lab/vault/reset` / `sw/*` mutation | `BLOCKED` — `AssistantToolPolicy` denies `sw/`, `pdm/` mutation, destructive lab routes | V-T4 forbidden-op scan 0 matches; `solidworks.save_document` probe returns deny |
| S3–S8 read-only reads (selection/feature/body/drawing) | **PLANNED_NOT_PROMOTED** — each future read will require `VerifyAccess()` + dirty invariants + 0 COM escape | Roadmap Section S3–S8 enforces; no promotion in T6 |

## 11. Receipt

Factory: `AuditReceiptFactory.Create()`

```
Receipt: { OperationId, CorrelationId, Adapter="SolidWorksCustomPropertyReadAdapter",
  RuntimeVersion="33.5" (mock) or "unknown" for no-doc, RuntimeClassification, PathHash, DocumentType, ActiveConfiguration,
  DirtyBefore, DirtyAfter, IsReadOnly, StateVersionBefore, StateVersionAfter (equal for read-only),
  ToolsRequested=["custom_property_snapshot"], ToolsExecuted=(empty for MOCK, ["custom_property_snapshot"] for READ_ONLY_ANALYST),
  Evidence[], Findings[], Result="Completed" or "Partial" or "Denied", Message, Errors[], SideEffects=[], RollbackReason="" }
```

T6 receipts:
- T22 `Completed`, 0 `READ_FAILURE`, `SideEffects.Count==0`
- T23 `Denied`/`empty` with `NO_ACTIVE_DOCUMENT` typed error, `MutationCount 0`
- T24 `Partial` with `READ_FAILURE` typed error
- T26 `Completed` with `DirtyBefore==DirtyAfter` + `StateVersion v1==v2` + `SideEffects 0`

All receipts are tamper-evident via canonical JSON hash chain (`AssistantChainedReceipt.Seal()` flips to sealed before hashing).

## 12. Safety

| Check | Result |
|---|---|
| Forbidden writer calls in new `Audit`+`SolidWorks` code (`Add3/Set2/Delete2/SetSaveFlag/Save3/SaveAs4/EditRebuild3/Rebuild`) | **0** — V-T4 scan exit 1 (zero matches) | 
| `Task.Run` around COM | **0** | 
| `MessageBox` in adapter | **0** | 
| Secret scan (`api[_-]?key|bearer|authorization|connectionstring|password`) in `Audit`+`SolidWorks`+`docs/audits`+`docs/exec-plans` | **CLEAN** — zero matches — no `.env` opened |
| Path redaction | `AuditRedactionService.RedactPath` strips `C:\Users\<user>\`, SHA-256 hash + basename only — `Redaction_LocalPath_RemovesSensitiveSegments` passes |
| Active config + dirty invariant | Asserted per-read (T22/T26) — any deviation recorded as `READ_FAILURE`, not silently fixed |
| COM thread violation | `SolidWorksThreadGuard` throws `COM_THREAD_VIOLATION` on off-thread — `ThreadGuard_WrongThread_ThrowsTypedViolation` passes; `DENIED` receipt produced with no audit performed |

No external write, no paid service, no real credential, no CAD/PDM mutation in T6.

## 13. Remaining Gaps (Inherited)

| Gap | Impact | Verification Path |
|---|---|---|
| Build uses 2024 interop (32.3.1.2) vs installed 2025 SP5 (33.5.0.53) interop | Live COM binding against 2025 not proven | Future S3–S8 bounded live probe against PID 37244 read-only will exercise actual COM marshalling (no save) and record `INTEROP_LIMITATION` per-property if newer member absent |
| `ISldWorks.RevisionNumber()` not invoked live | Runtime classification forced to `UnknownReadOnly` until live proof | At S7/S8 live smoke (non-customer doc, clean state) call `RevisionNumber()` on UI thread and promote to `Sw2025Target` only if `"SolidWorks 2025 SP5.0"` returned |
| `SwLiveDocumentSource` not wired into `swaddin.cs` (STAGED_NOT_WIRED) | No live snapshot via tool call from task pane yet | Follow-up packet allowing `swaddin.cs` edit: capture `SolidWorksThreadGuard` at startup + compose `SolidWorksAuditComposition(_app)` behind read-only tool route |
| PDM/Epicor/Salesforce/AionUI_database live reads unavailable | No evidence for those connectors | Separate allowlist/credential packets; out-of-scope for read-only CAD slice |
| Document `[80185536.SLDPRT]` was dirty/referenced during T6 | Blocked live read; only mock proof | Re-probe later with a clean, non-released pilot part (copy under `docs\pilot` or temp) after explicit operator save/close approval — never mutate customer file |

## 14. Promotion

| Slice | Decision | Evidence Gate |
|---|---|---|
| S1/S2 `SolidWorksCustomPropertyReadAdapter` + `SwLiveDocumentSource` (9 APIs) | **`PARTIAL_VERIFIED_RUNTIME_GAP`** — source wired ✓ build PASS ✓ regression PASS (191/1/0) ✓ deploy BLOCKED (dirty doc honest) → not LIVE_VERIFIED | Build MSBuild Lab SUCCESS; vstest 191 passed; PID 37244 `SOLIDWORKS Professional 2025 SP5.0` observed but not promoted via `RevisionNumber()`; deterministic mocks T22–T26 prove contract zero-mutation. |
| S1/S2 live SP proof | `VERIFIED_2025_SP5.0` as **observed but NOT promoted** (window title + registry) | Window title `SOLIDWORKS Professional 2025 SP5.0` PID 37244 + `DisplayVersion 33.5.0.53` recorded; no `LIVE_VERIFIED` claim without `RevisionNumber()` + bounded `DirtyUnchanged` receipt. |
| S3–S8 (Selection, Feature Tree, Body/BBox/Mass, Drawing Sheet/View, Multi-View Evidence, Ledger Expansion) | **`NOT_PROMOTED`** — `PLANNED_NOT_PROMOTED` roadmap only | No rows in ledger Section 3 beyond the 9 APIs; each S slice requires its own `COMPILED_ONLY + deterministic mocks + bounded live read-only probe` before any `PARTIAL_VERIFIED_RUNTIME_GAP` slice-local promotion. PDM/mutation remains BLOCKED. |

Global status remains `PARTIAL_VERIFIED_RUNTIME_GAP`, never `LIVE_VERIFIED` until a future slice completes `Build + Focused Slice Tests + Full Regression + Bounded Live Read-Only Probe (dirty unchanged, no save) + WindowTitle + RevisionNumber live validation + Dirty/Config invariant receipt`.

---

## DONE

T6 bounded slice complete: `BLUEBRICK_API_CLAIM_LEDGER_T6.md` (9 APIs, 32.3.1.2, SwLiveDocumentSource.cs, COMPILED_ONLY) + `BB_M001_S3_S8_ROADMAP.md` (S3–S8 read-only bounded group, PDM/mutation BLOCKED) + this 14-section final receipt.

Next move: execute S3 (Selection read-only) as smallest reversible slice per roadmap verification plan — do not infer promotion for feature-tree/body/drawing ledger expansion.

## Receipt Artifact Paths

- `docs/audits/BLUEBRICK_API_CLAIM_LEDGER_T6.md`
- `docs/exec-plans/active/BB_M001_S3_S8_ROADMAP.md`
- `docs/audits/BB_M001_T6_FINAL_RECEIPT.md`
- `SolidWorks/Adapters/SwLiveDocumentSource.cs`
- `SolidWorks/Composition/SolidWorksAuditComposition.cs`
- `BlueBrick.UI.Tests/SolidWorks/T5LiveEvidenceTests.cs` (T22–T26)

## No Claim of Live Runtime Verification Without Evidence (Reaffirmed)

- NO live `ISldWorks.RevisionNumber()` call in T6.
- NO live `IModelDoc2 IActiveDoc2` COM object read from PID 37244.
- NO SOLIDWORKS document save/rebuild/open/close.
- Runtime classification for live capture remains `UnknownReadOnly` (install-registry source); side note `VERIFIED_2025_SP5.0` is observed via window title, not promoted.
- Runtime wiring into `swaddin.cs` is `STAGED_NOT_WIRED`.
- Zero CAD/PDM mutation.
