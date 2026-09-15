# BlueBrick React Preview R02 Receipt

## Decision

STATUS: BLUEBRICK_PREVIEW_BLOCKED
TASK_ID: BB20-UI-20260902-165104-R02
MODEL: GPT-5.6 Luna
REASONING: HIGH
RUN_ID: BB20-UI-20260902-165104-R02
ROUTING_CONFIDENCE: 86

USER_INTENT: Advance the smallest reversible no-admin React frontend preview through the existing registered BlueBrick runtime.
SUCCESS_GATE: Current React UI visibly rendered in SOLIDWORKS with runtime evidence.

## Starting identity

REPOSITORY: C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick
BRANCH: bluebrick-assistant-slice1-foundation
STARTING_SHA: f3c0905137da6ac5ffe3523d3aa852c26fa18c48
WORKTREE_STATE: Dirty; existing R01 changes preserved.
RUNTIME_IDENTITY: HKCR CLSID {C56E0AFF-0BD3-4364-90CB-1A581046CD7D}; CodeBase C:\BlueBrick\BlueBrick.dll; version 1.0.13.4.

## Evidence and build

SOURCE_DIST: C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick\AssistantWeb\dist
DEPLOYED_DIST: C:\BlueBrick\AssistantWeb\dist

SOURCE_ARTIFACTS:

| File | Size | SHA-256 |
|---|---:|---|
| index.html | 390 | E370C9711592F2695CF0651F702AE809A69AC0DE9B635C6C5B4CC8A4A6C7237B |
| assistant-index.css | 39961 | 8B0281A263CD60E9A83A3CE332DC6D0C9A3BE2525337F3E3847080CFB5C87B27 |
| assistant-web.js | 2392081 | 9854EC54946987FBC91B069D62BBF56F416A8C42D99560316EBF52FF08BBE82B |

BUILD_COMMANDS:

- `npm.cmd run check:deps` — PASS
- `npm.cmd run typecheck` — PASS
- `npm.cmd run build` — PASS; Vite warning: JS chunk exceeds 500 kB
- `npm.cmd run verify:ui-activation` — PASS; static contract only
- `npm.cmd run verify:transport` — PASS
- `npm.cmd run verify:replay` — PASS
- `npm.cmd run smoke:narrow` — PASS; local static shell, no SOLIDWORKS/live connector

The initial `npm run check:deps` invocation failed with `Unknown command: "pm"` due to the PowerShell `npm` wrapper; the same step was rerun through `npm.cmd` and passed. No project script ran during the failed invocation.

## Deployment

BACKUP: C:\BlueBrick\backups\BB20-UI-20260902-165104-R02\AssistantWeb.dist.orig
BACKUP_VERIFICATION: PASS; Robocopy exit 1 (files copied), manifest/hash parity verified.
FILES_BACKED_UP: index.html, assistant-index.css, assistant-web.js
FILES_COPIED: index.html, assistant-index.css, assistant-web.js
SOURCE_TO_DEPLOYED_HASH_PARITY: PASS

DEPLOYED_BEFORE:

| File | SHA-256 |
|---|---|
| index.html | E370C9711592F2695CF0651F702AE809A69AC0DE9B635C6C5B4CC8A4A6C7237B |
| assistant-index.css | 3298B3F959CD5A3D038AAAFF063D7BCE240B148E289B619E789BE9BCEBA2833D |
| assistant-web.js | 817E6D68BADC2EF95633BAF9BA6C19D76AA1D8E8ADE3A65FF87179B6E7A01A65 |

DEPLOYED_AFTER: Matches SOURCE_ARTIFACTS exactly.
DLL_CHANGED: NO; SHA-256 remains C6C38DF37B5853D8D97D09D0FA7236735A4B6F0933CE7D4923E9C5D269CC10AE.
CONFIG_CHANGED: NO; production config SHA-256 remains 2BC0B3E19169ADCC6C59376956B2F5E3B30034235C507DB5EB5CB0E3F93A9091.
REGISTRY_CHANGED: NO; registry was read-only inspected and not written.

## Runtime and visual evidence

SOLIDWORKS_PID: 49356
LOADED_DLL: NOT_PROVEN; read-only module inspection did not see BlueBrick.dll in the existing process.
WEBVIEW_DIST: STATIC_SOURCE_EXPECTATION = C:\BlueBrick\AssistantWeb\dist; live resolution NOT_PROVEN.
INDEX: STATIC_PRESENT
CSS: STATIC_PRESENT_AND_HASH_MATCHED
JS: STATIC_PRESENT_AND_HASH_MATCHED
REACT_BOOTSTRAP: NOT_PROVEN
FALLBACK_ACTIVE: NOT_PROVEN
SCREENSHOT: NOT_CAPTURED

Live SOLIDWORKS smoke was not launched or attached because `LAB_SMOKE_APPROVED` was unset. Static/file-scheme smokes do not prove WebView2 or React runtime acceptance.

## Safety boundaries

CAD_MUTATION: NO
PDM_MUTATION: NO
EPICOR_MUTATION: NO
DB_MUTATION: NO
UNAUTHORIZED_MUTATION_OBSERVED: NO
UAC_OR_REGISTRY_ACTION: NONE

## Closure

LAST_PROVEN_TRANSITION: build -> backup -> frontend-only copy -> source/deployed SHA-256 parity
FIRST_FAILED_TRANSITION: npm wrapper invocation; recovered with npm.cmd.
FIRST_RUNTIME_GAP: live SOLIDWORKS/WebView/React visual acceptance.
ROLLBACK_AVAILABLE: YES
ROLLBACK_LOCATION: C:\BlueBrick\backups\BB20-UI-20260902-165104-R02
ROLLBACK_TESTED: Backup manifest/hash parity verified; restore not executed.
PROMOTION: Frontend deployment verified; overall preview remains BLOCKED pending live evidence.
NEXT_DEPENDENCY: Explicit `LAB_SMOKE_APPROVED=true` for a controlled, safe SOLIDWORKS visual smoke.
NEXT_MOVE: After approval, safely reload/relaunch the existing registered runtime, prove loaded DLL/WebView dist/resource loads/React bootstrap, capture the visual checkpoint, and stop for Chief review.
