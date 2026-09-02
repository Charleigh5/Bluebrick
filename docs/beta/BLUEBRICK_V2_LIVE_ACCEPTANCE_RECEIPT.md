# BLUEBRICK 2.0 LIVE BETA ACCEPTANCE RECEIPT

## 1. Environment

repo: `C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick`  
worktree: canonical checkout; preserved generated dist and acceptance artifacts remain dirty  
branch: `bluebrick-assistant-slice1-foundation`  
HEAD: `9a26e89bb2c7e320924b39a7edc65fa86fa0559f`  
run IDs: `20260902-091551`, `20260902-093418`, `20260902-095413`  
SOLIDWORKS: 2025 SP5.0; existing PID 34736 dirty assembly preserved; Lab PID 23756 launched but add-in not observed loaded  
Lab DLL: `C:\BlueBrickLab\BlueBrick.Lab.dll`, SHA256 `32B5271C35D89DB55C5C566B6B905B11C43555B86E21DFBDB606033C5F97D9FE`  
frontend: React 19 + Vite 8.0.16; Lab identity embedded in source/bin asset triplet  
backend: Lab config expected `127.0.0.1:17179`; listener not observed  
database: local vault/relay static paths; legacy business DB not runtime-proven  
PDM: disabled/not runtime-proven  
Epicor: disabled/not runtime-proven

## 2. What Changed

| Exact file/repair | Change |
|---|---|
| `AssistantWeb/src/runtimeIdentity.ts` | Added non-secret build identity, full asset manifest, literal Vite env embedding, and DOM data attributes. |
| `AssistantWeb/src/App.tsx` | Mounted identity on default and hardware-CAD surfaces. |
| `AssistantWeb/src/execution-board/ExecutionBoardApp.tsx` | Mounted identity on execution-board surface. |
| `AssistantWeb/src/vira-lab/ViraLabApp.tsx` | Mounted identity on VIRA Lab surface. |
| `AssistantWeb/src/styles.css` | Added narrow-pane-safe identity styling. |
| `docs/beta/*` and `artifacts/beta-acceptance/20260902-095413/*` | Added governed manifests, matrix, test matrix, and receipts. |

Repair commits: `fbe14f3` and `9a26e89`; no Production files were edited.

## 3. Build/Test Evidence

| command | result | evidence |
|---|---|---|
| `npm run build` with Lab identity env | PASS | Vite 8.0.16; 45 modules; identity values embedded in `assistant-web.js`. |
| `bluebrick.ps1 -Action build -Target Lab -Configuration Lab` | PASS | MSBuild 17.14.51; 0 warnings, 0 errors; Lab DLL hash `32B527...`. |
| `npm run verify:output-lab` | PASS | exact three-file dist inventory and source/bin byte parity. |
| `vstest ... BlueBrick.UI.Tests.dll` | PASS | 266 passed, 1 credential-gated skip, 0 failed of 267. |
| `vstest ... BlueBrick.Relay.Tests.dll` | PASS | 4 passed, 0 failed. |
| frontend transport/UI/replay/execution-board/VIRA tests | PASS | focused contract and fixture tests passed. |
| controller doctor/prepare/launch dry-run/rollback dry-run | PASS | Lab-only boundaries and rollback path validated. |

## 4. Runtime Identity

| expected | observed | result |
|---|---|---|
| `LAB | BlueBrick 2.0 | 9a26e89bb2c7e320924b39a7edc65fa86fa0559f | 20260902-095413` in loaded React DOM | Bundle contains exact identity; live DOM not observed because Lab add-in did not activate | STATIC PASS / LIVE UNKNOWN |
| Lab DLL and config | Registry/target hashes matched before launch | PASS for staging/registration |
| `https://bluebrick-ui.invalid/index.html` | No fresh WebView telemetry or loaded URI | UNKNOWN |
| bridge `127.0.0.1:17179` | Not listening | FAIL runtime gate |

## 5. Legacy Parity

The legacy manifest identifies 15 capabilities from baseline `1904fc9924de4cd73dc24201e1bef9246e00ce5`. Source parity is not runtime parity. Core CAD, PDM, Epicor, card, export, task, CRM, and preference rows remain `BLOCKED_EXTERNAL`, `UNKNOWN`, or `DEFERRED_REQUIRES_APPROVAL`; no legacy feature is promoted to runtime PASS by the assistant wrapper.

## 6. Frontend/API Matrix

Static host-mediated flows are present for status, scopes, tools, messages, screenshots, active-document snapshots, local vault, PDM policy, and Epicor policy. Runtime endpoint behavior is `UNKNOWN` because 17179 never listened. Static defects remain: screenshot review posts `/assistant/review` with incomplete payload and no server route was found; Reject and More have no handlers; hardware CAD requests a route not found in `AgentHttpServer.cs`.

## 7. Database

The V2 local vault is file-backed and the relay uses isolated SQLite state; relay tests passed. The original Epicor/PDM business database architecture and schema were source-mapped but not queried. Result: `BLOCKED_EXTERNAL` / `NOT_LIVE`.

## 8. PDM

Historical direct `EdmVault5`/`LoginAuto`/`IEdmSearch*` paths were recovered. Lab PDM is disabled and no authenticated read-only PDM search was attempted. No checkout, check-in, state, property, or file write occurred. Result: `BLOCKED_EXTERNAL`.

## 9. Epicor

Historical `ClsEpicor` SQL callers and current parameterized part-only assistant path were mapped. Lab Epicor is disabled; no connection, query, or credential inspection occurred. Broader task/quote/opportunity/attachment parity remains deferred. Result: `BLOCKED_EXTERNAL`.

## 10. SOLIDWORKS Live Behavior

Lab registration, CodeBase, Lab DLL hash, and COM activation probe passed at the entry boundary, with one non-fatal ReleaseComObject warning from the validator. A separate SOLIDWORKS PID 23756 became responsive, but `BlueBrick.Lab.dll` was not observed in its modules, port 17179 was closed, and no WebView telemetry appeared. The existing PID 34736 unsaved assembly was not terminated, attached, or mutated. Result: `NOT_LIVE`.

## 11. Failure/Regression Tests

| test | expected failure | observed | result |
|---|---|---|---|
| Identity embedding | missing metadata must not masquerade as Lab | dynamic lookup defect found and repaired in new run | PASS after repair |
| Lab bridge | no host means no live acceptance | 17179 not listening | PASS classification; live gate blocked |
| SOLIDWORKS dirty session | do not terminate/mutate user document | dirty marker on PID 34736 preserved | PASS safety boundary |
| rollback | Lab-only restore; Production untouched | exact backup rollback exit 0 | PASS |
| PDM/Epicor unavailability | explicit unavailable state | not safely induced beyond disabled config | `NOT_INDUCED_SAFETY_BOUNDARY` |

## 12. Production Isolation

Before/after Production DLL SHA256: `C6C38DF37B5853D8D97D09D0FA7236735A4B6F0933CE7D4923E9C5D269CC10AE`.  
Before/after Production config SHA256: `2BC0B3E19169ADCC6C59376956B2F5E3B30034235C507DB5EB5CB0E3F93A9091`.  
Production timestamps remained unchanged; port 17178 remained owned by PID 4. `productionMutationObserved = false`.

## 13. Remaining Defects

| priority | defect | status |
|---|---|---|
| P0 | Lab add-in/WebView/17179 activation not proven | BLOCKED |
| P1 | Screenshot review route/payload mismatch | UNREPAIRED |
| P1 | Hardware CAD endpoint absent; Reject/More handlers absent | UNREPAIRED |
| P1 | Database, PDM, Epicor runtime acceptance absent | BLOCKED_EXTERNAL |
| P2 | Legacy card/export/task/CRM/preferences have no equivalent runtime proof | DEFERRED |

## 14. Beta Scores

Runtime Identity 35/100; Frontend Wiring 55/100; SOLIDWORKS Integration 20/100; Legacy Feature Parity 15/100; Database 20/100; PDM 0/100; Epicor 0/100; Failure Handling 55/100; Rollback 100/100; Production Isolation 100/100. Critical blocked gates override numeric scores.

## 15. Promotion Decision

`NOT_BETA_READY`

Highest evidence-supported state: `STATE 0 NOT_LIVE`.

## 16. Chief Beta Test Entry Point

- Chief can test the static frontend bundle and fixture-only execution-board/VIRA Lab lanes; a live BlueBrick 2.0 SOLIDWORKS beta is not yet available.
- The Lab launch was rolled back after activation failed. Do not use the existing dirty SOLIDWORKS session for acceptance.
- The expected UI identity is `LAB | BlueBrick 2.0 | 9a26e89bb2c7e320924b39a7edc65fa86fa0559f | 20260902-095413`.
- First five eventual live tests: identity/URI readback; real active-document context; local-vault known/no-result search; read-only PDM known/no-result search; read-only Epicor known/no-result search.
- Known limitations: Lab activation/bridge absent; PDM/Epicor disabled; legacy parity incomplete; no writes were tested.
- Report a defect with the exact run ID, control/action, observed state, screenshot or sanitized receipt, and whether the failure is static or live.

## 17. Execution Receipt

STATUS: `COMPLETED_WITH_DECLARED_GAPS`  
HIGHEST PROVEN STATE: `STATE 0 NOT_LIVE`  
RUN ID: `20260902-095413`  
SOURCE COMMIT: `9a26e89bb2c7e320924b39a7edc65fa86fa0559f`  
TESTED BUILD: Lab DLL `32B5271C35D89DB55C5C566B6B905B11C43555B86E21DFBDB606033C5F97D9FE`; frontend build `20260902-095413`  
PRODUCTION MUTATED: NO  
ROLLBACK VERIFIED: YES  
CRITICAL FAILURES: Lab add-in/WebView/17179 activation not proven; existing Lab UI identity not observed  
DATA GAPS: live WebView URI/DOM, backend route behavior, database/PDM/Epicor runtime, legacy core parity  
NEXT DEPENDENCY: owned Lab SOLIDWORKS activation/bridge and safe Computer Use or typed host evidence  
NEXT MOVE: diagnose why the fresh Lab process does not load the registered add-in, then begin a new acceptance run after repair
