# BB-M001 S3–S8 Bounded Read-Only Roadmap — T6

**Packet:** `BB-CODEX-M001-S0S2-001` continuation
**Date:** 2026-08-26
**Branch:** `bluebrick-assistant-slice1-foundation` @ `ced14be`
**Status:** `PLANNED_NOT_PROMOTED` — S1/S2 remain `PARTIAL_VERIFIED_RUNTIME_GAP`; S3–S8 not promoted, read-only APIs only, PDM/mutation BLOCKED.
**Promotion ceiling:** No selection/feature-tree/body/drawing claim is LIVE_VERIFIED until per-slice `COMPILED_ONLY + deterministic mocks + bounded live read-only proof` passes.

## 0. Global Guard (Applies To All S3–S8)

- PDM/Epicor/Salesforce/vault mutation: **BLOCKED** (no `IEdmVault5::`, `IEdmFile5::LockFile/UnlockFile`, `Save3/SaveAs4`, `SetSaveFlag`, `Add3/Set2/Delete2`, `EditRebuild3`).
- SOLIDWORKS launch: only via existing `SLDWORKS.exe PID 37244` read-only; no new instance launch, no document open/close, no save.
- Every read verifies `SolidWorksThreadGuard` (UI thread) and `DirtyBefore==DirtyAfter` + `ActiveConfigurationBefore==After` invariants.
- Every string/path hashed before receipt; raw values never cross tool boundary; `AuditRedactionService` required.
- `INTEROP_LIMITATION` recorded per-property when 2024 interop (32.3.1.2) lacks a 2025-only member; missing member never guessed.
- Tests per slice: deterministic `ISw*Source` stub + bounded live read-only probe against `[80185536.SLDPRT]` (read-only, no save) only after composition wiring proven.

## S3 — Selection (Read-Only)

**Purpose:** Enumerate current selection without changing it.
**Allowed APIs (read-only):** `IModelDoc2.ISelectionManager`, `ISelectionMgr.GetSelectedObjectCount2(-1)`, `GetSelectedObjectType3(index,-1)`, `GetSelectedObject6(index,-1)` returning COM object stub (no activation), `IModelDoc2.GetSelectedObjects` count only.
**Prohibited:** `ISelectionMgr.AddToSelection`, `DeSelect2`, `ClearSelection2`, `SetSelectedObjectMark`, anything that changes selection.
**Acceptance:** Snapshot returns `selectedCount`, `selectedTypeLabels[]` (type enum ints → labels, no COM object escape), `selectionHash` (canonical JSON SHA-256 of type list), `limitations[]`. Deterministic mock proves contract; live probe confirms count against open doc without mutating selection.

## S4 — Feature Tree (Read-Only)

**Purpose:** Bounded traversal of the FeatureManager tree.
**Allowed APIs (read-only):** `IModelDoc2.FeatureManager`, `IFeatureManager.GetFeatures(false)`, `IFeature.GetTypeName2`, `IFeature.Name`, `IFeature.GetNextFeature`, `IFeature.GetChildren` (limit 500 features, depth 32).
**Prohibited:** `Feature.ModifyDefinition2`, `Insert*`, `DeleteSelection2`, suppression setters, `EditRebuild3`.
**Acceptance:** Snapshot returns `featureCount`, `features[{nameHash,typeHash,parentHash}]` sorted deterministically, `truncated` flag, `warnings[]`. No COM object escapes; only hashes cross boundary.

## S5 — Body / BBox / Mass (Read-Only, No Geometry Mutation)

**Purpose:** Bounded bounding-box + mass properties evidence from an already-loaded body without forcing rebuild.
**Allowed APIs (read-only):** `IModelDoc2.GetBodies2(swSolidBody, false)`, `IBody2.GetBodyBox()` (returns 6 doubles), `IModelDocExtension.GetMassProperties2` (no `CreateMassProperty` with save), `IBody2.GetMassProperties` getter-only.
**Prohibited:** `CreateBodyFrom*`, `Insert*, CreateMassProperty with assignment, `ForceRebuild3`, `Rebuild`.
**Acceptance:** Snapshot returns `bodyCount`, `bodies[{bboxHash,massHash,centerHash}]` with hash-redacted doubles, `unitsKind`, `limitations[]` when body is lightweight/suppressed. Deterministic mock with synthetic BBox; live probe validates BBox hash stability across reads (no dirty change).

## S6 — Drawing Sheet/View (Read-Only Native Drawing Evidence, PC-D-001 Reuse)

**Purpose:** All-sheet getter inventory per `SOLIDWORKS_API_RESEARCH_LEDGER` PC-D-001.
**Allowed APIs (read-only):** `IDrawingDoc.GetSheetNames`, `GetSheetCount`, `Sheet[name]`, `ISheet.GetProperties2`, `GetViews`, `IView.GetReferencedModelName` (hash), `ReferencedConfiguration` getter, `GetTableAnnotations` + `Type/RowCount/ColumnCount/DisplayedText2(...,false)` (text hashed).
**Prohibited:** `ActivateSheet`, `SheetNext/Previous`, `ActivateView`, `ReferencedConfiguration=` setter, `ReferencedDocument` open, `Save3`, `Text2` setter.
**Acceptance:** Snapshot returns `sheets[{nameHash,propertiesHash,views[]}]`, `views[{nameHash,type,refModelHash,refConfigHash,tables[]}]`, `incomparable/incomplete` states when inactive-sheet traversal unavailable on 2024 interop (recorded as `INTEROP_LIMITATION`, not silently omitted).

## S7 — Multi-View Image Evidence (Read-Only, No Save)

**Purpose:** Generate deterministic, bounded, view-state-preserving evidence images for packet review without saving the document.
**Allowed APIs (read-only):** `IModelDoc2.GetModelViewCount`, `IModelView.GetViewOrientation`, `IModelDoc2.ShowNamedView2("", opt) transient? BLOCKED`; instead use **offscreen capture via existing `FrmAgentWindow` Screenshot path** (`GenerateReviewJobManager` preview hook) only when composition explicitly approves transient `ShowNamedView2` with restore — S7 defers transient view change to a dedicated risk-gated sub-slice (S7a) and initially does **screenshot-of-current-view only** (`capture_screenshot` existing tool) with 512 KiB image cap + hash.
**Prohibited:** `SaveAs4` (PNG/JPG), `EditRebuild3`, persistent `ShowNamedView2` without restore, `ActivateSheet`.
**Acceptance:** Artifact returns `currentViewHash`, `imageHash`, `orientationLabel`, `limitations[]` when multi-view unavailable without view switch. S7a (future) introduces bounded `IView::GetOrientation` -> `Display` mapping with explicit `ViewSwitchedTemporarily` error code if dirty changes — but not promoted in T6.

## S8 — API Claim Ledger Expansion (Read-Only)

**Purpose:** Extend `BLUEBRICK_API_CLAIM_LEDGER_T6.md` with S3–S7 read-only APIs after per-slice proof.
**Allowed work:** Append rows for `ISelectionMgr.*`, `IFeatureManager.*`, `IBody2.GetBodyBox`, `IDrawingDoc.*`, `IView.*` with interop 32.3.1.2, source file, `COMPILED_ONLY` until per-slice live probe passes.
**Prohibited:** Promoting any S3–S8 row to `LIVE_VERIFIED` without deterministic mock + bounded live read-only probe + `DirtyUnchanged` receipt. Do not overwrite T6 Section 3 rows.
**Acceptance:** Ledger version bumps to `T7/T8...`; each new row references its slice receipt (e.g., `BB_M001_S3_SELECTION_RECEIPT.md`).

## Promotion Rule Per Slice (S3–S8)

`source wired (seam exists) ✓ build PASS ✓ deterministic mock tests PASS ✓ bounded live read-only probe PASS (dirty unchanged, no mutation) → PARTIAL_VERIFIED_RUNTIME_GAP slice-local; S1/S2 remain PARTIAL_VERIFIED_RUNTIME_GAP globally; never LIVE_VERIFIED without full `RevisionNumber()+WindowTitle` live validation + save-invariant receipt.`

PDM/mutation remains `BLOCKED`; `MUTATION_APPROVED=true` never inferred from read-only success.

## Verification Plan Per Slice

1. Write `SolidWorks/Adapters/Internal/ISw*Source` seam extension unit under `SolidWorks/Adapters/Internal/` (internal, no COM escape).
2. Add `BlueBrick.UI.Tests/SolidWorks/S<n>*Tests.cs` with 3–5 deterministic cases (red→green).
3. Build `MSBuild Lab AnyCPU` — 0 errors.
4. `vstest` focused slice + full regression (191+ existing).
5. Bounded live probe (single read against PID 37244 `[80185536.SLDPRT]` read-only; no save) — only after `COMPILED_ONLY` passes — record `DirtyBefore==DirtyAfter` in receipt.
6. Append ledger row; do **not** promote beyond `COMPILED_ONLY` + `PARTIAL_VERIFIED_RUNTIME_GAP` slice-local until live probe evidence captured.

## No-Promotion Record (T6)

Selection, Feature Tree, Body/BBox/Mass, Drawing Sheet/View, Multi-View Image Evidence remain `NOT_PROMOTED` in T6. Ledger Section 3 is the only promoted API set (IActiveDoc2 etc) with status `COMPILED_ONLY`. S3–S8 rows do not exist yet.

## References

- `SolidWorks/Adapters/SwLiveDocumentSource.cs` (S1/S2 baseline)
- `docs/audits/BLUEBRICK_API_CLAIM_LEDGER_T6.md` (T6 ledger)
- `docs/audits/BLUEBRICK_SOLIDWORKS_RUNTIME_INTEROP_PROOF_2026-07-29.md` (interop baseline)
- `docs/SOLIDWORKS_API_RESEARCH_LEDGER.md` PC-D-001 (Phase D drawing surface)
