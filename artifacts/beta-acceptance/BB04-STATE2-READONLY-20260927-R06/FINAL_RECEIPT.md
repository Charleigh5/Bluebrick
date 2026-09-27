# STATE 2 `UI_BETA_READY` — Final R06 Read-Only CAD Vertical Slice

## Decision

- **Runtime result:** `PASS`
- **Evidence ladder:** `STATE 2 — UI_BETA_READY`
- **Acceptance run:** `BB04-STATE2-READONLY-20260927-R06`
- **Governing build:** `BB04-STATE2-READONLY-20260927-R05`
- **Source commit:** `c877aa4be2402bb92a7e175702fae19ed98d3e60`
- **Gates:** 28/28 PASS
- **CAD/PDM/provider/Production mutation:** none
- **Repair commit:** `5a01afeacf135c609e219e3355d82f0ad1220229`; the Sprint 05 `VerifyAccess` hunk remains unstaged

## Vertical slice

`solidworks.get_active_document_snapshot` ran against a disposable copy of the SOLIDWORKS-installed `GenerateAssembly\Part.SLDPRT` in an owned Lab SOLIDWORKS process. The host bridge executed both the audit-snapshot route and the assistant-tool route against that real Part.

## Proof

- Live `RevisionNumber() = 33.5.0` classified `Sw2025Target`
- Part identity, `Default` configuration, dirty false before/after
- Equal non-empty `StateVersionBefore/After`
- `MutationCount = 0`, `SideEffects = []`, `RollbackReason = ""`
- Lab allow decision observed as `safe_tool_name`
- Tool receipt visible in `/assistant/tool-audit`
- R05 WebView identity is `LAB | BlueBrick 2.0 | c877aa4… | BB04-STATE2-READONLY-20260927-R05`, React mounted, 17 callbacks, no telemetry errors
- `RUNTIME_GENERATION_MATCH`; selfcheck healthy
- Disposable Part hash, external Sprint 05 smoke-asset hash, and Production DLL/config hashes unchanged
- Normal document close, managed process exit, ports closed, license service Stopped

Full results: [`gate-summary.json`](gate-summary.json). Structured row: [`acceptance.json`](acceptance.json). Runtime identity: [`runtime-identity.json`](runtime-identity.json).

## Evidence history

- `BB04-STATE2-READONLY-20260927-R01`: first execution exposed live `33.5.0 → UnknownReadOnly` and empty `StateVersionBefore`.
- `BB04-STATE2-READONLY-20260927-R02`: repaired runtime evidence passed 28/28 on the R04 bundle.
- `BB04-STATE2-READONLY-20260927-R05`: rebuilt the final bundle after the Sprint 05 dist collision; 27/28 applicable checks passed, with the external smoke asset explicitly baselined as changed by another lane.
- `BB04-STATE2-READONLY-20260927-R06`: validation-only pass against the unchanged R05 bundle; all 28 applicable gates passed.

## Declared limitations

- `interop_GetPropertyNames_unavailable` remains in the snapshot scopes.
- Tool receipt `DocumentType` is `Unknown`; the audit receipt and tool item identify `Part`.
- The React shell has no control for this tool, so the capability ran host/API → host.
- Native visual capture was unavailable because the owned window could not be foregrounded into the capture session; runtime receipts prove the slice.

## Governance disposition

`STATE 2 UI_BETA_READY` is achieved at runtime. The repair and receipts are not yet committed; any commit must stage only the State 2 state-version hunk from the shared composition file.
