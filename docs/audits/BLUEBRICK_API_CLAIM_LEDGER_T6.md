# BlueBrick API Claim Ledger — T6 Version/API Ledger + Promotion

**Packet:** `BB-CODEX-M001-S0S2-001` T6
**Date:** 2026-08-26
**Branch:** `bluebrick-assistant-slice1-foundation` @ `ced14be`
**Mode:** `MOCK + READ_ONLY_ANALYST` · **Mutation:** `BLOCKED`
**Promotion ceiling:** `PARTIAL_VERIFIED_RUNTIME_GAP` (not LIVE_VERIFIED)

## 1. Installed Runtime Actually Observed — Do Not Invent

| Runtime | Evidence | PID | Window Title | DisplayVersion | InstallLocation | Classification |
|---|---|---|---|---|---|---|
| SOLIDWORKS Professional 2025 SP5.0 | Window enumeration T5 | 37244 | `SOLIDWORKS Professional 2025 SP5.0 - [80185536.SLDPRT]` | `33.5.0.53` | `C:\Program Files\SolidWorks Corp\SOLIDWORKS\` | `Sw2025Target (observed via window title + registry)` |

No other SOLIDWORKS versions are claimed. 2024 SP5 interops in `lib\` are the regression compile surface, not a runtime claim. 2026 or other SP values not observed.

Observed evidence snapshot 2026-08-26:
- `Get-Process SLDWORKS` -> `PID=37244 Title=[SOLIDWORKS Professional 2025 SP5.0 - [80185536.SLDPRT]]` alive, responding.
- Registry `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\` -> `SOLIDWORKS 2025 SP05 33.5.0.53` (consistent).
- `ISldWorks.RevisionNumber()` NOT invoked in T6 (dirty-doc honest block) — window title is the only SP proof used.

## 2. Interop Assembly Baseline (Build-Time Contract)

| Assembly | HintPath | AssemblyVersion | FileVersion | SHA-256 | mtime | Used By |
|---|---|---|---|---|---|---|
| SolidWorks.Interop.sldworks.dll | `lib\SolidWorks.Interop.sldworks.dll` | `32.3.1.2` | null (interop has no FileVersion block) | `CAA9511363F03AC4F09BE017F9D906D4B96CB92AD4D725E4A925FFBD7582E521` | 2024-06-06 21:02:36 | All APIs below |
| SolidWorks.Interop.swconst.dll | `lib\SolidWorks.Interop.swconst.dll` | `32.3.1.2` | null | `629435478A82F394F38F05FC7C894B5310342E0E9C376D09F5A3A60270FB992C` | 2024-06-06 21:02:40 | `swDocumentTypes_e` in `GetType()` branch |

Build-time interops are 2024 SP5 family (32.3.1.2). Runtime SOLIDWORKS is 2025 SP5 (33.5.0.53). Adapter compiles against 2024 interop; missing 2025-only members would record `INTEROP_LIMITATION` — not upgraded in Slice 1/2 per packet §14.

## 3. Per-API Claim Table — New Adapter (SwLiveDocumentSource)

All rows use interop `SolidWorks.Interop.sldworks, Version=32.3.1.2, PublicKeyToken=7c4797c3e4eeac03` unless noted (*swconst*).

| # | API | Interop Assembly | Interop Version | Source Location | Line | Runtime Result | Status | Mock Proof | Notes |
|---|---|---|---|---|---|---|---|---|---|
| 1 | `ISldWorks.IActiveDoc2` | SolidWorks.Interop.sldworks | 32.3.1.2 | `SolidWorks/Composition/SolidWorksAuditComposition.cs` | 33 | COMPILED_ONLY | PARTIAL_VERIFIED_RUNTIME_GAP | `T5LiveEvidenceTests.T22` + `W03` via `ISwDocumentSource` stub | Live proof blocked by dirty doc (honest). Composition root resolves `IActiveDoc2 as IModelDoc2`; deterministic mock proves null-safe contract. |
| 2 | `IModelDoc2.GetActiveConfiguration()` via `GetActiveConfigurationName()` | SolidWorks.Interop.sldworks | 32.3.1.2 | `SolidWorks/Adapters/SwLiveDocumentSource.cs` | 34-35 | COMPILED_ONLY | PARTIAL_VERIFIED_RUNTIME_GAP | `T22`, `T26` asserts `ActiveConfiguration` preserved dirty unchanged | Getter only; no `IConfiguration` setter. |
| 3 | `IModelDoc2.GetConfigurationNames()` | SolidWorks.Interop.sldworks | 32.3.1.2 | `SolidWorks/Adapters/SwLiveDocumentSource.cs` | 38-39 | COMPILED_ONLY | PARTIAL_VERIFIED_RUNTIME_GAP | `T26` bounded all-config path; `Snapshot_AllConfigurations_RespectsLimit` | Returns `string[]`; bounded by `ConfigurationReadLimit`. |
| 4 | `ICustomPropertyManager.Get2(string,out string,out string)` | SolidWorks.Interop.sldworks | 32.3.1.2 | `SolidWorks/Adapters/SwLiveDocumentSource.cs` | 78 | COMPILED_ONLY | PARTIAL_VERIFIED_RUNTIME_GAP | `Snapshot_DocumentProperties_PreservesRawAndResolvedValues` + `T24` partial | Cached read; `TryGet` wraps `Get2`; `Get3` not invoked on 2024 interop. |
| 5 | `ICustomPropertyManager.GetNames()` | SolidWorks.Interop.sldworks | 32.3.1.2 | `SolidWorks/Adapters/SwLiveDocumentSource.cs` | 67 | COMPILED_ONLY | PARTIAL_VERIFIED_RUNTIME_GAP | `Snapshot_*` + `W03` null doc path | Enumerates property names; null -> `INTEROP_LIMITATION`. |
| 6 | `IModelDoc2.GetSaveFlag()` via `GetDirty()` | SolidWorks.Interop.sldworks | 32.3.1.2 | `SolidWorks/Adapters/SwLiveDocumentSource.cs` | 41 | COMPILED_ONLY | PARTIAL_VERIFIED_RUNTIME_GAP | `T22`, `T26` asserts `DirtyBefore==DirtyAfter` | Dirty flag read twice; invariant violation -> `READ_FAILURE`. |
| 7 | `IModelDoc2.IsOpenedReadOnly()` via `GetIsReadOnly()` | SolidWorks.Interop.sldworks | 32.3.1.2 | `SolidWorks/Adapters/SwLiveDocumentSource.cs` | 42 | COMPILED_ONLY | PARTIAL_VERIFIED_RUNTIME_GAP | `Snapshot_*` + `T22` | Boolean read only. |
| 8 | `IModelDoc2.GetPathName()` via `GetPath()` | SolidWorks.Interop.sldworks | 32.3.1.2 | `SolidWorks/Adapters/SwLiveDocumentSource.cs` | 55 | COMPILED_ONLY | PARTIAL_VERIFIED_RUNTIME_GAP | `Snapshot_NoComObjectEscapesSerializableGraph` + redaction test | Path immediately hashed via `AuditRedactionService.RedactPath`. |
| 9 | `IModelDoc2.GetType()` via `GetDocumentType()` | SolidWorks.Interop.sldworks (+ swconst) | 32.3.1.2 | `SolidWorks/Adapters/SwLiveDocumentSource.cs` | 47 | COMPILED_ONLY | PARTIAL_VERIFIED_RUNTIME_GAP | `Snapshot_*` doc type branches | Maps `swDocumentTypes_e` to Part/Assembly/Drawing/Unknown; no activation. |

All 9 APIs classify `READ_ONLY` per `docs/architecture/solidworks-reader-api-inventory.json` pattern; 0 `MUTATION` entries in scope. No `Add3/Set2/Delete2/SetSaveFlag/Save3/SaveAs4/EditRebuild3/Rebuild/MessageBox` invoked.

## 4. Promotion Rule (Honest)

Source wired? Build PASS? Regression PASS? Deploy BLOCKED (dirty doc honest) => **PARTIAL_VERIFIED_RUNTIME_GAP**

| Gate | Result | Evidence |
|---|---|---|
| Source wired (seam exists, composition root creates `SwLiveDocumentSource` from `IActiveDoc2`) | ✓ | `SolidWorksAuditComposition.cs:33-35` + `SwLiveDocumentSource.cs` |
| Build PASS | ✓ | `MSBuild BlueBrick.sln /p:Configuration=Lab` SUCCESS (warnings only, 0 errors) 2026-08-26 |
| Regression PASS | ✓ | `vstest 191 passed, 1 skipped, 0 failed` (T22-T26 + W01-W12 + hardening + audit) |
| Deploy BLOCKED (dirty doc honest) | BLOCKED | SOLIDWORKS PID 37244 has `[80185536.SLDPRT]` loaded; dirty-state invariant would be violated by live save/rebuild attempt; no live COM proof executed |
| **Promotion** | **PARTIAL_VERIFIED_RUNTIME_GAP** | Not `LIVE_VERIFIED`. |

If runtime version specifically proven via mock + window title, record side note:

| Observed Version Note | Value | Promoted? |
|---|---|---|
| `VERIFIED_2025_SP5.0` as observed via window title + `DisplayVersion=33.5.0.53` | `SOLIDWORKS Professional 2025 SP5.0` PID 37244 | Recorded as **observed but NOT promoted** (no `RevisionNumber()` live proof, no `COMPLETED` receipt promotion). |

## 5. What Is Not Claimed

- No claim that live `IActiveDoc2` COM call succeeded in this session.
- No claim that `Get2/GetNames` returned real document values from `[80185536.SLDPRT]`.
- No claim that `GetSaveFlag` live value was read; dirty doc blocked live invocation.
- No PDM/vault/Epicor/Salesforce mutation proof.
- No promotion for Selection/FeatureTree/Body/BBox/Mass/Drawing/Sheet/View/Multi-View Evidence (S3-S8) — roadmap only.

## 6. Ledger Maintenance

This ledger is the authoritative T6 API claim source. Next update on S3 start must append (not overwrite) selection `ISelectionMgr` claims after read-only proof; ledger expansion is S8.

## 7. References

- `SolidWorks/Adapters/SwLiveDocumentSource.cs` (live adapter)
- `SolidWorks/Composition/SolidWorksAuditComposition.cs` (IActiveDoc2 seam)
- `docs/audits/BLUEBRICK_SOLIDWORKS_RUNTIME_INTEROP_PROOF_2026-07-29.md` (install registry + lib interop inventory)
- `BlueBrick.UI.Tests/SolidWorks/T5LiveEvidenceTests.cs` (deterministic mock proof T22-T26)
- `docs/audits/BB_M001_T6_FINAL_RECEIPT.md` (14-section final receipt, Detailed snapshot + policy + receipt + safety)
