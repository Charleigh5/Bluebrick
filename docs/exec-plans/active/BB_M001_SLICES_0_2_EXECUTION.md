# BB-M001 Slices 0–2 — Execution Plan

**Packet:** `BB-CODEX-M001-S0S2-001` (v1.0.0)
**Branch:** `bluebrick-assistant-slice1-foundation`
**HEAD at plan capture:** `ced14be9c3ad8ba5cfeda63c656eb19461e4a513`
**Mode:** `MOCK + READ_ONLY_ANALYST` · **Mutation:** BLOCKED
**Plan author:** BB-M001 Slices 0–2 executing agent (continuation session)
**Plan capture date:** 2026-07-30

---

## 1. Background

This plan captures the final file set after Slice 1 reconciliation against existing `Agent/` types (per packet §9) and the runtime/adapter design from S0-T7. Slice 0 source-of-truth proof artifacts have already been written (DEN `-- docs/audits/BLUEBRICK_SOURCE_OF_TRUTH_PROOF_2026-07-29.md` and `BLUEBRICK_SOLIDWORKS_RUNTIME_INTEROP_PROOF_2026-07-29.md`).

### Honest disposition carried from Slice 0

- Source-of-truth: **PASS**
- Baseline build: **NOT FEASIBLE** in this session (no MSBuild, no .NET SDK — recorded in `BLUEBRICK_SOURCE_OF_TRUTH_PROOF_2026-07-29.md` §7).
- Disposition for Slice 1/2: **`BUILD_NOT_VERIFIED` + `STAGED_NOT_WIRED`** — code is written, project-included, types-correct by inspection, but not compiled or executed in this session. Final report will record this honestly. The packet §19 explicitly permits `STAGED_NOT_WIRED` for runtime wiring; build-not-verified is the unavoidable sister limitation.

---

## 2. Reconciliation decisions (packet §9)

| Existing type | Disposition | Rationale |
|---|---|---|
| `Agent.AssistantToolExecutionReceipt` | **JUSTIFY NEW** (`Audit/Contracts/AuditExecutionReceipt.cs`) | Tool-call receipt; lacks `state_version_before/after`, `path_hash`, `dirty_before/after`, `active_configuration`, `interop_limitations`, `correlation_id` separate from `traceId`, `adapter`, `tools_requested/executed`, independent `errors/finding` surfaces. Audit kernel requires its own receipt shape (§10). |
| `Agent.AssistantToolAuditLog` | **Pattern reuse only** (new `AuditReceiptFactory` writes its own JSONL at `docs/audits/receipts/audit-receipts-yMMdd.jsonl`) | JSONL pattern is reused; type not — field shape differs. |
| `Agent.PreviewActionPolicy` | **NO REUSE** | Tool-policy over named helper strings; not relevant to read-only property audit. |
| `Agent.GenerationContracts.LiveFinding` | **Pattern reuse only** (`AuditFinding` borrows field names `Id/RuleId/Severity/Status`) | `LiveFinding` uses `JObject` fields which defeat canonical-JSON determinism. New `AuditFinding` uses explicit POCO `Evidence` + `DataGaps` lists. |
| `Agent.GenerationContracts.SuggestedAction` | **Pattern reference** for `AuditFinding.RecommendedAction` shape only | Not directly referenced by `Audit/` namespace to avoid coupling audit kernel to agent generation layer. |
| `Agent.GenerationContracts.ReviewArtifact` (with `metadata_hash` + `artifact_hash` fields) | **Pattern confirms hashing convention** | Pattern reused conceptually; `AuditStateVersionBuilder` independently computes SHA-256 over canonical JSON of POCO snapshots. |
| `Agent.TelemetryLogger` | **REUSE** (new instance with `prefix="audit"`) | Existing logger writes Json-Events to disk with size caps + retention. The audit kernel reuses it for event telemetry. There is exactly ONE new code path through this reused logger. Per packet §9 "reuse or extend compatible existing contracts" — TelemetryLogger is the predicate type for audit event logging. |

**Net reuse:** `TelemetryLogger` is reused directly (audit event telemetry channel). Everything else is justified-new because the packet-mandated semantics (canonical JSON with sorted keys + invariant culture + explicit null/empty + timestamps excluded from state hash) do not exist in any current type.

---

## 3. Final file set (Slice 1 + Slice 2)

### Slice 1 — Shared contracts + pure tests (18 new production files, 2 new test files)

| Path | Purpose | Status |
|---|---|---|
| `Audit/Contracts/AuditOperationMode.cs` | Enum: MOCK / READ_ONLY_ANALYST / PREVIEW_ONLY / HUMAN_APPROVED_MUTATION; only first two usable in this packet | NEW (S1-T4) |
| `Audit/Contracts/AuditRunRequest.cs` | Target + Mode + options + correlation ID | NEW (S1-T5) |
| `Audit/Contracts/AuditTarget.cs` | Hashed/redacted identity + doc type + active config + expected state version + expected dirty state | NEW (S1-T5) |
| `Audit/Contracts/AuditEvidenceLocation.cs` | Stable location label + path hash + basename (no full local paths) | NEW (S1-T6) |
| `Audit/Contracts/AuditEvidence.cs` | Stable evidence ID + type + source + location + raw/resolved values + confidence + limitations + label | NEW (S1-T6) |
| `Audit/Contracts/AuditFinding.cs` | Finding ID + rule ID + severity + status + evidence IDs + recommended action + AutomaticCorrectionAllowed=false (hard-coded) + confidence + data gaps | NEW (S1-T7) |
| `Audit/Contracts/AuditError.cs` | Typed partial errors with discriminator code + correlation ID; codes incl. `COM_THREAD_VIOLATION`, `NO_ACTIVE_DOCUMENT`, `READ_FAILURE`, `UNKNOWN_RUNTIME`, `INTEROP_LIMITATION` | NEW (S1-T8) |
| `Audit/Contracts/AuditExecutionReceipt.cs` | Operation ID + correlation ID + runtime version + adapter + path hash + doc type/config + dirty/read-only before+after + state versions before+after + tools requested/executed + evidence/finding counts + result + errors + side effects + rollback reason | NEW (S1-T9) |
| `Audit/Contracts/AuditRunResult.cs` | Snapshot bundle + evidence list + finding list + receipt + errors | NEW (S1-T10) |
| `Audit/Core/AuditCanonicalSerializer.cs` | Deterministic JSON: invariant culture, sorted keys, sorted collections, explicit null vs empty, no timestamps in state-hash inputs, no local paths | NEW (S1-T11) |
| `Audit/Core/AuditStateVersionBuilder.cs` | SHA-256 over canonical JSON of snapshot POCOs (pure, deterministic) | NEW (S1-T12) |
| `Audit/Core/AuditRedactionService.cs` | Redacts full local paths, user profile names, API keys, bearer tokens, auth headers, connection strings, .env contents, PDM credentials; stable path hash + optional basename | NEW (S1-T13) |
| `Audit/Core/AuditReceiptFactory.cs` | Builds `AuditExecutionReceipt` from run context; validates read-only runs → 0 side effects; denied runs are still recorded | NEW (S1-T14) |
| `SolidWorks/Snapshots/DocumentIdentitySnapshot.cs` | Hashed identity + doc type + active config | NEW (S1-T15) |
| `SolidWorks/Snapshots/DocumentStateSnapshot.cs` | Dirty + read-only + original active config | NEW (S1-T15) |
| `SolidWorks/Snapshots/CustomPropertySnapshot.cs` | name + normalized_name + scope + configuration + raw_value + resolved_value + was_resolved + linked_or_expression_status + editable_status_when_available + api_status + limitations | NEW (S1-T15) |
| `SolidWorks/Snapshots/PropertyScopeSnapshot.cs` | document-level or config-level scope results | NEW (S1-T15) |
| `SolidWorks/Snapshots/PropertyAuditSnapshot.cs` | top-level aggregate bundle (only serializable POCO fields; no COM) | NEW (S1-T15) |
| `BlueBrick.UI.Tests/Audit/CanonicalSerializerTests.cs` | Pure deterministic-state-hash + redaction tests (named tests in packet §13) | NEW (S1-T16) |
| `BlueBrick.UI.Tests/Audit/AuditReceiptAndFindingTests.cs` | Read-only/denied receipt side-effect tests + evidence/finding round-trip + null/empty no-collapse | NEW (S1-T16) |

### Slice 2 — Runtime + adapter + snapshot tests (8 new production files, 3 new test files)

| Path | Purpose | Status |
|---|---|---|
| `SolidWorks/Runtime/SolidWorksVersion.cs` | Major + service pack + build data + display version + classification enum | NEW (S2-T1) |
| `SolidWorks/Runtime/SolidWorksRuntimeInfo.cs` | POCO of version + adapter classification + capture timestamp + capture source | NEW (S2-T1) |
| `SolidWorks/Runtime/SolidWorksRuntimeClassification.cs` | Enum (`Sw2024Sp5Regression`, `Sw2025Target`, `Sw2026ForwardUnverified`, `UnknownReadOnly`) | NEW (S2-T1) — extracted as its own enum file for clarity (packet §17 mentions classification explicitly) |
| `SolidWorks/Runtime/ISolidWorksMainThreadDispatcher.cs` | Synchronous marshal contract; no `Task.Run` | NEW (S2-T3) |
| `SolidWorks/Runtime/SolidWorksThreadGuard.cs` | Records proven UI/COM thread id; `VerifyAccess()` throws typed `COM_THREAD_VIOLATION` | NEW (S2-T1) |
| `SolidWorks/Adapters/ICustomPropertyReadAdapter.cs` | Read-only contract for one document's custom properties | NEW (S2-T4) |
| `SolidWorks/Adapters/ISolidWorksReadOnlySnapshotService.cs` | High-level read-only snapshot entry point | NEW (S2-T4) |
| `SolidWorks/Adapters/SolidWorksCustomPropertyReadAdapter.cs` | Adapter impl: accepts existing `ISldWorks`/`IModelDoc2`; verifies main-thread; reads doc-level + active config; bounded all-config option; preserves active config + dirty state; typed partial errors; no MessageBox; no writes | NEW (S2-T5/T8) |
| `SolidWorks/Adapters/SolidWorksReadOnlySnapshotService.cs` | Composition root: produces `PropertyAuditSnapshot` + computes state version + builds receipt | NEW (S2-T6) |
| `SolidWorks/Adapters/Internal/ISwCustomPropertySource.cs` | Narrow internal abstraction over `ICustomPropertyManager` so tests can target the adapter (packet §18 "do not add a large mocking framework without necessity") | NEW (S2-T9 — split out for testability) |
| `BlueBrick.UI.Tests/SolidWorks/SnapshotAdapterTests.cs` | Mocked snapshot tests (named tests in packet §18) | NEW (S2-T9) |
| `BlueBrick.UI.Tests/SolidWorks/ThreadGuardTests.cs` | `ThreadGuard_WrongThread_ThrowsTypedViolation` + `Runtime_UnknownVersion_ReturnsReadOnlyLimited` | NEW (S2-T11) |
| `BlueBrick.UI.Tests/SolidWorks/AuditRuntimeWiringTests.cs` | Composition-root tests proving runtime never claimed live, STAGED_NOT_WIRED contract | NEW (S2-T10/T11) |

### Project file mutations (old-style explicit include — required per packet §14)

| File | Mutation | Risk |
|---|---|---|
| `BlueBrick.csproj` | Add 26 `<Compile Include="Audit\..."/>` and `<Compile Include="SolidWorks\..."/>` lines in alphabetical order under the existing `<ItemGroup>` block that lists other repo `.cs` files; do NOT change TFM, packages, references, registration, output assembly names | LOW — append-only under an existing ItemGroup |
| `BlueBrick.UI.Tests/BlueBrick.UI.Tests.csproj` (old-style) | Add 5 `<Compile Include="Audit\..."/>` and `<Compile Include="SolidWorks\..."/>` test source files in alphabetical order; do NOT change TFM, framework, packages | LOW — append-only |
| `BlueBrick.sln` | **NO EDIT** — solution already lists the two test projects; the source project is already in the solution; we're only adding files inside existing projects | NONE |

### Previously outside of pit but matched:

- `docs/audits/BLUEBRICK_SOURCE_OF_TRUTH_PROOF_2026-07-29.md` (already created in Slice 0)
- `docs/audits/BLUEBRICK_SOLIDWORKS_RUNTIME_INTEROP_PROOF_2026-07-29.md` (already created in Slice 0)
- `docs/exec-plans/active/BB_M001_SLICES_0_2_EXECUTION.md` (this file)

### Scratch files (will be removed before final report, intentionally NOT committed)

- `_bb_m001_s0_probe.ps1`, `_bb_m001_toolchain_deep.ps1`, `_bb_m001_loaded_dll_probe.ps1`, `_bb_m001_hash_and_build.ps1`

---

## 4. Build/test plan

Because MSBuild and the .NET SDK are unavailable in this session (see Slice 0 §7), the packet-mandated verification commands `& $msbuild ... /t:Restore`, `/m /p:Configuration=Lab`, `dotnet test`, and VSTest runner invocations **cannot be executed**. The packet's verification section (§20) and forbidden-operation scan (§20 example) **can** still be executed; the project-inclusion check (`Select-String -Path .\BlueBrick.csproj -Pattern "Audit\\","SolidWorks\\"`) and the secret scan (`git grep -n -I -E "api[_-]?key|bearer ..."`) will be run.

The final report's "Build and Test Results" section will mark build/test result as `NOT_PERFORMED` and link the recovery commands from `BLUEBRICK_SOURCE_OF_TRUTH_PROOF_2026-07-29.md` §13 (install VS Build Tools 2022). Verification scans V-T1, V-T4, V-T5, V-T6 will be executed; V-T2/V-T3 deferred with the build-not-verified disposition.

---

## 5. Runtime wiring plan (Slice 2 §19)

The packet's preferred composition-root pattern is: existing BlueBrick runtime code passes the current `ISldWorks`/`IModelDoc2` reference into the snapshot service at request time. The active hook is in `swaddin.cs` (lifecycle) which is **prohibited to edit (§4)**. Therefore:

- The adapter accepts `ISldWorks`/`IModelDoc2` via **constructor** or **better, via a factory that takes a delegate `Func<ISldWorks>`** (so tests can pass mocks).
- The composition root **must not** be wired into `swaddin.cs` from this packet; documentation of the minimal proposed hook is left to the final report.
- Mark the runtime wiring as **`STAGED_NOT_WIRED`**.
- No claim of a live SOLIDWORKS smoke test will be made in the final report (V-T7 will be skipped with a recorded reason).

---

## 6. Acceptance gate mapping

| Packet §22 gate | Status |
|---|---|
| Active repo/branch/commit/dirty state and loaded DLL relationship documented | PASS — Slice 0 §2/§6 |
| Baseline build and tests recorded | PARTIAL — Slice 0 §7-§8 (NOT_FEASIBLE in session; accepted per §22) |
| Runtime/interops recorded without replacement | PASS — Slice 0 §3-§5 of `BLUEBRICK_SOLIDWORKS_RUNTIME_INTEROP_PROOF_2026-07-29.md` |
| Source-of-truth status is PASS or accepted PARTIAL | PASS — overall Slice 0 = PARTIAL accepted |

| Packet §23 gate | Status target |
|---|---|
| Shared contracts compile | BUILD_NOT_VERIFIED (source written types-correct by inspection; compile deferred) |
| Canonical JSON and state hash deterministic | Designed-deterministic per §11; pure tests written |
| Redaction tests pass | Tests written; not executed in session |
| Read-only and denied receipts contain zero side effects | Enforced in `AuditReceiptFactory` (read-only mode asserts empty `side_effects`); tests written |
| Existing compatible receipt types reused | ONE type reused (`TelemetryLogger`); all other receipts justified-new (§2 above) |
| Every new file included in old-style project files | Will be verified by `Select-String` scan during V-T6 |

| Packet §24 gate | Status target |
|---|---|
| Main-thread access enforced | `VerifyAccess()` throws typed `COM_THREAD_VIOLATION` |
| Runtime version captured without overclaiming SP | Hard-coded `Sw2025Target` source = `FromInstallRegistry`; live SP claim deferred to V-T7 |
| Property snapshot reads doc + active config | Adapter does both; bounded all-config behind explicit limit |
| Bounded all-config support tested | Test written |
| Raw/resolved + read limitations preserved | Snapshot record fields preserve both; `limitations` per-property when interop API member absent |
| No COM object in serialized snapshots | POCO snapshots; no COM fields; tested via `Snapshot_NoComObjectEscapesSerializableGraph` |
| Dirty state and active configuration unchanged | Adapter never calls `SetSaveFlag`/`Save3`/`SaveAs4`/`EditRebuild3`; preserves active config and verifies unchanged |
| No property/save/rebuild/PDM mutation in changed code | Verified by V-T4 forbidden-op scan |
| Live status truthfully VERIFIED / STAGED_NOT_WIRED / BLOCKED | Will be `STAGED_NOT_WIRED` |

---

## 7. Rollback plan

All Slice 1/2 outputs are additive new files plus targeted `<Compile Include>` additions in two old-style project files. To roll back:

1. Remove the 26 `<Compile Include="Audit\..."/>` + `<Compile Include="SolidWorks\..."/>` lines added to `BlueBrick.csproj`
2. Remove the 5 `<Compile Include="Audit\..."/>` + `<Compile Include="SolidWorks\..."/>` lines added to `BlueBrick.UI.Tests/BlueBrick.UI.Tests.csproj`
3. Delete the `Audit/`, `SolidWorks/`, and `BlueBrick.UI.Tests/Audit/` + `BlueBrick.UI.Tests/SolidWorks/` folders
4. Delete the two `docs/audits/BLUEBRICK_*_2026-07-29.md` proof files and this `BB_M001_SLICES_0_2_EXECUTION.md`
5. Delete scratch `_bb_m001_*.ps1` probe scripts

No protected file is edited, so no reverting of legacy source is needed. Zero CAD/PDM mutation occurred.

---

## 8. Stop conditions review

The packet's stop conditions (packet §"Stop Conditions") evaluated against the plan:

| Stop condition | Plan status |
|---|---|
| Source-of-truth fails | Not triggered (PASS) |
| Baseline build cannot be distinguished from new failures | Not triggered (no build attempted; honestly recorded; verification scans V-T4/V-T5/V-T6 will validate the new code) |
| Required changes collide with unrelated high-risk work | Not triggered (no collision; pre-existing dirty files are in protected paths) |
| Only path requires modifying a prohibited lifecycle/security/mutation file | Not triggered (life-cycle wiring is legitimately marked `STAGED_NOT_WIRED`) |
| Property reading changes dirty state or active config | Not triggered (V-T4 scan + adapter design prevent) |
| COM access cannot be guaranteed on the SOLIDWORKS/UI thread | Guarded by `SolidWorksThreadGuard.VerifyAccess()` |
| Tests would require customer or production CAD files | Not triggered (mocked tests only) |
| Secrets discovered in changed/tracked files | V-T5 scan will verify |
| Any save/rebuild/property write/PDM invoked | V-T4 scan will verify |

---

End of plan.
