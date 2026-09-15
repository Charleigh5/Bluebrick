# BlueBrick R02 Live Smoke Escalation Receipt

STATUS: ROUTING_ESCALATION_REQUIRED
TASK_ID: BB20-UI-20260902-165104-R02
MODEL_ROUTE: GPT-5.6 Luna -> Sol escalation
REASONING: HIGH
ROUTING_CONFIDENCE: 86
USER_APPROVAL: Explicit approval for the controlled SolidWorks live smoke was received in the current turn.
PROMOTION: Not promoted. React preview verification stopped at the first runtime contradiction.

## Intent and gate

USER_INTENT: Verify the R02 frontend-only React preview through the existing registered BlueBrick runtime while preserving R01/R02 state.
SUCCESS_GATE: Current React UI visibly rendered in SOLIDWORKS with runtime evidence.
FIRST_UNRESOLVED_TRANSITION: Registered BlueBrick runtime and target frontend parity -> React WebView navigation, bootstrap, and visual checkpoint.

## Proven source and deployment state

REPOSITORY: C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick
BRANCH: bluebrick-assistant-slice1-foundation
HEAD: f3c0905137da6ac5ffe3523d3aa852c26fa18c48
WORKTREE: Dirty; existing R01/R02 changes preserved; no reset, clean, stage, commit, or unrelated edit performed.
SOURCE_DIST: C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick\AssistantWeb\dist
DEPLOYED_DIST: C:\BlueBrick\AssistantWeb\dist
ROLLBACK: C:\BlueBrick\backups\BB20-UI-20260902-165104-R02\AssistantWeb.dist.orig
ROLLBACK_VERIFICATION: PASS; backup/deployment/pre-deployment manifests and all three backup files exist; restore not executed.

R02 frontend copy was already complete before this continuation. Current source/target parity remained PASS:

| File | Source SHA-256 | Deployed SHA-256 |
|---|---|---|
| index.html | E370C9711592F2695CF0651F702AE809A69AC0DE9B635C6C5B4CC8A4A6C7237B | E370C9711592F2695CF0651F702AE809A69AC0DE9B635C6C5B4CC8A4A6C7237B |
| assistant-index.css | 8B0281A263CD60E9A83A3CE332DC6D0C9A3BE2525337F3E3847080CFB5C87B27 | 8B0281A263CD60E9A83A3CE332DC6D0C9A3BE2525337F3E3847080CFB5C87B27 |
| assistant-web.js | 9854EC54946987FBC91B069D62BBF56F416A8C42D99560316EBF52FF08BBE82B | 9854EC54946987FBC91B069D62BBF56F416A8C42D99560316EBF52FF08BBE82B |

FILES_BACKED_UP: index.html, assistant-index.css, assistant-web.js
FILES_COPIED: index.html, assistant-index.css, assistant-web.js
FILES_BUILT: AssistantWeb/dist/index.html, AssistantWeb/dist/assistant-index.css, AssistantWeb/dist/assistant-web.js from the prior R02 `npm.cmd run build`.
DLL_BUILD_IN_CONTINUATION: None.

## Static checks already closed

- `npm.cmd run check:deps` — PASS
- `npm.cmd run typecheck` — PASS
- `npm.cmd run build` — PASS; Vite emitted the existing large-JS-chunk warning
- `npm.cmd run verify:ui-activation` — PASS; static contract only
- `npm.cmd run verify:transport` — PASS
- `npm.cmd run verify:replay` — PASS
- `npm.cmd run smoke:narrow` — PASS; local static shell, not SolidWorks/WebView runtime proof
- Current `git diff --check` — PASS with existing LF/CRLF warnings only

## Live evidence collected

SOLIDWORKS_PID: 45076
SOLIDWORKS_START: 2026-09-03 08:03:07 -04:00
SOLIDWORKS_WINDOW: `SOLIDWORKS Professional 2025 SP5.0 - [80242726.SLDASM]`
SOLIDWORKS_RESPONDING: true
REGISTERED_GUID: `{C56E0AFF-0BD3-4364-90CB-1A581046CD7D}`
REGISTERED_CODEBASE: `file:///C:/BlueBrick/BlueBrick.DLL`
DEPLOYED_DLL: `C:\BlueBrick\BlueBrick.dll`, version `1.0.13.4`, SHA-256 `C6C38DF37B5853D8D97D09D0FA7236735A4B6F0933CE7D4923E9C5D269CC10AE`

The current-process startup window in `C:\BlueBrick\bluebrick-startup.log` records `ConnectToSW`, task-pane creation/hosting, `AgentHttpServer started`, and `ConnectToSW success` at 08:03:15-08:03:18. An authenticated read-only GET to `http://127.0.0.1:17178/agent/selfcheck` returned `healthy`, proving a live BlueBrick bridge responded. Native module enumeration did not list managed `BlueBrick.dll`; the runtime conclusion is therefore correlated to the startup log and live bridge, not an OS module-list claim.

The authenticated read-only GET to `/assistant/status` returned:

- `AssistantWebViewStatus`: `fallback-shell`
- `AssistantWebViewError`: `null`
- live `BridgePort`: `17178`
- `AddinMode`: `Production`
- `EnableUploads`: `false`
- `RequireExplicitUploadConsent`: `false`

The deployed production config at `C:\BlueBrick\config\appsettings.json` currently contains `Agent.BridgePort=35001`, `Assistant.UseReactWebView=true`, `Assistant.EnableUploads=true`, and `Assistant.RequireExplicitUploadConsent=true`. Port 35001 refused the read-only health probe while 17178 responded. No WebView diagnostic JSON/PNG was created since the current process start window; React bootstrap and visual capture are therefore not proven.

## Contradiction and escalation

EXPECTED: The registered runtime would use the R02 React WebView and expose a current React shell backed by the three hash-matched deployed assets.

OBSERVED: The running bridge reports the fallback shell, uses the production default port 17178 rather than the configured 35001, and reports default false values for other internal Assistant settings despite the config file containing true values.

CONTRADICTION: Runtime configuration/binary behavior does not match the proven current deployment/configuration contract. `Agent\AgentConfig.cs:247` explicitly annotates `AgentSettings.BridgePort`, while `Agent\AgentConfig.cs:308` defines `AssistantSettings.UseReactWebView` as an internal property without the corresponding explicit JSON property annotation. This is a concrete source-level candidate for configuration deserialization loss, but the running deployed binary/config generation must be resolved before attributing root cause.

## Required Sol continuation packet

MODEL_ESCALATION_REQUIRED
SOURCE_MODEL: LUNA
TARGET_MODEL: SOL
TARGET_REASONING: HIGH
TASK_ID: BB20-UI-20260902-165104-R02
USER_INTENT: Verify the current R02 React frontend in the existing SolidWorks runtime without registry/UAC/DLL/CAD/PDM/production mutation.
DESIRED_OUTCOME: Prove React WebView navigation, bootstrap, and visual rendering, or produce a narrow corrected lab-only route.
LAST_PROVEN_STATE: R02 frontend triplet is deployed with source/target SHA-256 parity; current BlueBrick bridge is alive and SolidWorks is responding.
FIRST_UNRESOLVED_TRANSITION: Live fallback host -> intended React WebView bootstrap and visual checkpoint.
EVIDENCE: This receipt, R02 FINAL_RECEIPT.md, deployed/source hashes, current process state, authenticated read-only bridge status, production config values, startup log, and WebViewDiagnostics directory state.
CLOSED_INVESTIGATIONS: R01/R02 recovery, source/HEAD/worktree preservation, frontend build, backup, frontend-only copy, static activation/transport/replay/narrow smokes.
FILES_CHANGED: No files changed during live smoke except this receipt.
COMMANDS_RUN: Read-only PowerShell process/module/registry/config/hash/status/log checks; prior R02 npm.cmd checks listed above.
ROLLBACK_STATE: Available at the R02 backup path; no restore or production rollback performed.
PERMISSION_BOUNDARY: Registry untouched; no HKLM write; no UAC; no DLL replacement; no CAD/PDM/production/customer mutation; no credentials exposed.
QUESTION_FOR_SOL: Resolve the running binary/config generation mismatch and state whether the smallest safe next probe is a corrected source-level binding fix plus lab-only host validation, without changing C:\BlueBrick production DLL/config or restarting the R01 investigation.
RECOMMENDED_NEXT_PROBE: In a Sol-owned narrow lane, compare the loaded deployed DLL generation to the current AgentConfig source and validate explicit JSON binding/authoritative config-path selection in a distinct Lab runtime (GUID/port/user-data root); return a procedure before any production host mutation.

## Final state

REACT_BOOTSTRAP: NOT_PROVEN
VISUAL_CHECKPOINT: NOT_CAPTURED
FALLBACK_ACTIVE: PROVEN_BY_LIVE_ASSISTANT_STATUS
REGISTRY_STATE: UNTOUCHED
DLL_STATE: UNTOUCHED
SOLIDWORKS_MUTATION: NONE
PDM_MUTATION: NONE
PRODUCTION_MUTATION_IN_CONTINUATION: NONE
UNRESOLVED_GAPS: React WebView navigation/resource responses, 17 callback bridge, DOM readiness, screenshot, and authoritative runtime config/binary identity.
DECISION: ROUTING_ESCALATION_REQUIRED
