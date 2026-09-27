# STATE 2 `UI_BETA_READY` — Read-Only CAD Vertical Slice

## Decision

- **Runtime result:** `PASS`
- **Evidence ladder:** `STATE 2 — UI_BETA_READY`
- **Run/build ID:** `BB04-STATE2-READONLY-20260927-R02`
- **Source commit:** `c877aa4be2402bb92a7e175702fae19ed98d3e60`
- **Gates:** 28/28 PASS
- **Repair commit:** PENDING — the two runtime-evidence fixes are currently uncommitted
- **CAD/PDM/provider/Production mutation:** none

## Vertical slice

**Capability:** `solidworks.get_active_document_snapshot`

A disposable copy of the SOLIDWORKS-installed `GenerateAssembly\Part.SLDPRT` was opened in the owned Lab SOLIDWORKS process. The live host bridge executed both the audit-snapshot route and the assistant-tool route against that real Part.

## Why this qualifies

- **Real SOLIDWORKS context:** active `IModelDoc2` Part, configuration `Default`, stable document identity hash, live runtime version `33.5.0`.
- **Original capability slice:** host bridge → `AssistantToolService` → `SolidWorksAuditComposition` → `SwLiveDocumentSource` → typed snapshot → audit receipt + tool receipt.
- **No provider dependency:** neither leg calls a model or external service.
- **No mutation:** `MutationCount=0`, `SideEffects=[]`, `DirtyBefore=false`, `DirtyAfter=false`.
- **Durable evidence:** the tool receipt `84bc499dfc57440fbd475255fdda828b` is present in `/assistant/tool-audit`.

## Runtime evidence

| Gate | Result |
| --- | --- |
| HTTP/envelope | 200; schema `2026-06-01.v1`; non-empty correlation IDs |
| Runtime classification | `Sw2025Target` from live `RevisionNumber() = 33.5.0` |
| Document | `Part`; configuration `Default`; identity hash present |
| State invariants | dirty false before/after; configuration stable |
| Receipt | `Completed`; adapter `SolidWorksCustomPropertyReadAdapter`; no findings/rollback/side effects |
| State version | non-empty and byte-equal before/after |
| Tool receipt | read-only; mode/version `READ_ONLY_ANALYST`; mutation count 0; no approval |
| Policy | Lab allow decision observed as `safe_tool_name` |
| Tool item | mutation count 0; runtime 33.5.0; correlation matches trace |
| Audit | receipt visible in tool audit |
| Disposable asset hash | `D171DAFB4FA34A02AAD2BA3C83BE314097E0E8A8BFE1671D71CBEE2E1234DDC3` before/after |
| Production isolation | DLL `0F4CF55D…` and config `2BC0B3E1…` unchanged |
| WebView | R02/LAB/c877aa4 identity; React mounted; 17 callbacks; no telemetry errors |
| Host status | `RUNTIME_GENERATION_MATCH`; selfcheck healthy |
| Teardown | 0 SOLIDWORKS processes; ports 17178/17179 closed; no ownership record; license service Stopped |

Full machine-readable results: [`gate-summary.json`](gate-summary.json). Row contract: [`acceptance.json`](acceptance.json).

## Repair required by the first attempt

R01 executed the same slice but failed the strict evidence gate:

- live `33.5.0` was misclassified as `UnknownReadOnly` because the classifier only matched strings containing `2025`;
- the read-only receipt left `StateVersionBefore` empty.

The R02 worktree repair maps live 32/33/34 revision families to the correct SOLIDWORKS generation and records equal state versions before/after. Focused tests: **70/70 PASS**, including `Runtime_LiveRevisionNumber_33_5_0MapsToSw2025Target`.

R01 evidence is preserved in the sibling `BB04-STATE2-READONLY-20260927-R01` folder.

## Declared limitations

- `interop_GetPropertyNames_unavailable` remains in the snapshot scopes (2024 interop against the 2025 runtime).
- The tool receipt's `DocumentType` is `Unknown`; the audit receipt and tool item both identify `Part`.
- The React shell has no control for this read-only tool; the capability was executed host/API → host. No UI-triggered capability execution is claimed.
- Native visual capture was unavailable because the owned SOLIDWORKS window could not be foregrounded into the capture session. Runtime receipts, not an image, prove the slice.

## Governance disposition

`STATE 2 UI_BETA_READY` is achieved at runtime. The evidence repair and this receipt are not yet committed. `SolidWorksAuditComposition.cs` contains a disjoint Sprint 05 hunk, so any later commit must stage only the State 2 state-version hunk from that file.

No Provider, PDM, Epicor, database, Production, or user CAD path was mutated. The disposable part is outside the repository and Production.
