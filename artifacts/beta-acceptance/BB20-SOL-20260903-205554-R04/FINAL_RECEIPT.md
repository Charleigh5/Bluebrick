# Final Receipt

## Decision

STATUS: `BLOCKED_ENVIRONMENT`  
TASK_ID: `BB20-SOL-CONFIG-LAB-ACCEPTANCE-v1.0`  
MODEL: `GPT-5.6 Sol`  
REASONING: `HIGH`  
ROUTING_CONFIDENCE: `98`  
RUN_ID: `BB20-SOL-20260903-205554-R04`  
SCORE: `60/100` with critical-gate override

## Intent and authority

USER_INTENT: Repair the proven config/runtime-generation divergence and accept React in an isolated Lab runtime.  
SUCCESS_GATE: `LAB_REACT_ACCEPTED` only with loaded Lab DLL, effective config, generation match, WebView navigation, React DOM/mount, bridge, screenshot, interaction and clean telemetry.  
AUTHORIZED: Canonical repo edits/tests, `C:\BlueBrickLab` build/deploy, HKCU Lab registration, separately owned Lab startup, read-only evidence.  
PROHIBITED: Production DLL/config/registration/process mutation; CAD/PDM/Vault/Epicor/Salesforce/database writes; UAC/HKLM; credentials; attachment to the pre-existing production process.

## Starting identity (G0)

REPOSITORY: `C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick`  
BRANCH: `bluebrick-assistant-slice1-foundation`  
STARTING_SHA: `f3c0905137da6ac5ffe3523d3aa852c26fa18c48`  
WORKTREE_STATE: Dirty; all pre-existing tracked/untracked work preserved.  
PRODUCTION_PROCESS: PID `62920`, responding, `SOLIDWORKS Professional 2025 SP5.0 - [80242617.SLDASM *]`.  
PRODUCTION_DLL: `C6C38DF37B5853D8D97D09D0FA7236735A4B6F0933CE7D4923E9C5D269CC10AE`  
STARTING_RELEASE_DLL: `2C8A40067C446D14FE47B6ADADC6C9C6DA4862CF4F66F7F8FFCC07F397A75A7B`  
STARTING_LAB_DLL: `32B5271C35D89DB55C5C566B6B905B11C43555B86E21DFBDB606033C5F97D9FE`  
PRODUCTION_CONFIG: `2BC0B3E19169ADCC6C59376956B2F5E3B30034235C507DB5EB5CB0E3F93A9091`  
STARTING_REPO_CONFIG: `FBAB1714B17CCA824E293167C4D6394951B5FC896626503EC13DA970FCFC9C2B`  
STARTING_LAB_CONFIG: `D4EC9F049E09F4815428406AC6E09F2F48C8A5BE42478C9F98ED471E5AE1C3B5`  
STARTING_FRONTEND: index `E370C971...C7237B`; CSS `8B0281A2...87B27`; JS `9854EC54...E82B`.  
PORTS: `17178` listening via HTTP.sys; `17179` and `35001` absent.  
PRODUCTION_REGISTRATION: `file:///C:/BlueBrick/BlueBrick.DLL`.  
PRODUCTION_STATUS: HTTP 200, `fallback-shell`, `BridgePort=17178`, `AddinMode=Production`, uploads/consent false.  
LATEST_PRODUCTION_WEBVIEW: `about:blank`, no React root/header, no screenshot.

## Proven causal transition

G1 reproduced the defect before implementation: a representative JSON fixture bound annotated `Agent.BridgePort=35001` but failed `Assistant.UseReactWebView=true`. This confirmed the serializer contract as the earliest broken transition.

## Changes

- Explicit schema and critical configuration mapping/validation in `Agent/AgentConfig.cs`.
- Non-secret configuration diagnostics surfaced by `/assistant/status` model fields.
- `RuntimeGenerationGuard` verifies target, schema, build IDs, DLL/config and frontend hashes.
- Lab host rejects generation mismatch before React navigation.
- WebView bootstrap requires a mounted React identity marker and captures build/source/environment.
- Frontend emits `data-bluebrick-app-mounted=true`.
- Lab lifecycle builds frontend with source/build identity, rebuilds C#, stages deployment, writes a coherent manifest and retains Lab-only rollback.
- Config/generation regression matrix added to `NativePortAndStartupTests`.
- Repository configs declare `ConfigSchemaVersion=1`.

Pre-existing user changes in `AssistantWeb/scripts/verify-packaged-output-contract.mjs` were not edited. `scripts/bluebrick.ps1` and generated `AssistantWeb/dist/assistant-web.js` had pre-existing edits and now contain mixed prior/current work; they were not reset or broadly replaced.

## Verification

| Check | Result |
|---|---|
| Pre-repair representative config test | Expected FAIL at `UseReactWebView` |
| Focused config/generation tests | 20/20 PASS |
| Focused config + WebView activation tests | 32/32 PASS |
| Full Release UI test suite | 275 PASS, 0 FAIL, 1 credential-gated SKIP |
| AssistantWeb typecheck | PASS |
| UI activation contract | PASS; static ceiling declared |
| Lab packaged-output contract | PASS |
| PowerShell lifecycle parse | PASS |
| Lab frontend build | PASS; Vite chunk-size warning |
| Lab MSBuild Rebuild | PASS; 28 existing warnings, 0 errors |
| Exact Lab assembly config reflection | PASS: 17179 / true / true / true / schema 1 / valid |
| Deployed generation guard | `RUNTIME_GENERATION_MATCH` |
| Built/deployed SHA parity | PASS for DLL, config, index, CSS and JS |
| HKCU Lab registration validation | PASS |
| Distinct Lab SOLIDWORKS startup | PASS, PID `63828` |
| Loaded Lab module / listener | FAIL: zero BlueBrick modules; no `17179` |
| Production preservation | PASS: PID/title and DLL/config hashes unchanged |

## Built/deployed generation

BUILD_ID: `BB20-SOL-20260903-205554-R04`  
SOURCE_COMMIT: `f3c0905137da6ac5ffe3523d3aa852c26fa18c48` (dirty source recorded separately)  
LAB_DLL: `97B8EDEF6F6C7E09871CC573B9C75866671AA8F4DCD02C059CD5B21AB2EEA3EE`  
LAB_CONFIG: `1ECCB27D42CE641D1DFDBA2A4B923A42F37DBEC2AA8806B79A210C7A1AF434A4`  
INDEX: `E370C9711592F2695CF0651F702AE809A69AC0DE9B635C6C5B4CC8A4A6C7237B`  
CSS: `8B0281A263CD60E9A83A3CE332DC6D0C9A3BE2525337F3E3847080CFB5C87B27`  
JS: `BE5FCCA6BFC6A279099325488AA2EDD869DE555DCBD3008A114D5F9FC89BB42D`  
MANIFEST: `C:\BlueBrickLab\runtime-manifest.json`

## First failure

FIRST_FAILED_TRANSITION: `owned Lab SOLIDWORKS process -> Lab add-in discovery/load`  
FAILURE_CODE: `LAB_ADDIN_NOT_LOADED`  
EXPECTED: PID `63828` loads `C:\BlueBrickLab\BlueBrick.Lab.dll`, then listens on `17179`.  
OBSERVED: PID responds but contains zero BlueBrick modules; only production `17178` listens; no Lab diagnostic root exists.  
EVIDENCE: Process/module inventory, port inventory, HKCU registration validator, absent Lab diagnostics.  
ENVIRONMENT_BLOCKER: Computer Use returned `apps: []`; safe native Add-Ins inspection and screenshot were unavailable. HKLM/UAC is prohibited and not attempted.

## Runtime gates

G7A process identity: PASS  
G7B add-in identity: FAIL  
G7C effective configuration: BLOCKED  
G7D in-process generation identity: BLOCKED (deployed offline guard PASS only)  
G7E WebView2: BLOCKED  
G7F navigation: BLOCKED  
G7G React root: BLOCKED  
G7H React mounted: BLOCKED  
G7I frontend identity: BLOCKED  
G7J bridge: BLOCKED  
G7K screenshot: BLOCKED  
G7L interaction: BLOCKED  
G7M telemetry: BLOCKED

## Rollback and boundary receipt

ROLLBACK_AVAILABLE: Yes  
ROLLBACK_LOCATION: `C:\BlueBrickLab\backups\BB20-SOL-20260903-205554-R04`  
ROLLBACK_TESTED: Automatic Lab-only rollback executed successfully after the first wrapper-argument failure; deployment was then retried successfully.  
PRODUCTION_MUTATED: No  
CAD_MUTATED: No  
PDM_MUTATED: No  
DB_MUTATED: No  
REGISTRY_CHANGED: HKCU Lab GUID only  
UNAUTHORIZED_MUTATION_OBSERVED: No

## Context delta / continuation

ROOT_CAUSE: Confirmed and repaired for current source/Lab build.  
PROMOTION: `BLOCKED_ENVIRONMENT`; not `LAB_REACT_ACCEPTED`.  
KNOWN: G0-G6 pass; distinct Lab PID exists; HKCU registration is coherent; add-in did not load.  
RETRIEVABLE: Add-Ins discovery/error state from PID `63828` when native Computer Use is available.  
HUMAN_DECISION: If HKCU discovery is absent in SOLIDWORKS 2025, decide whether to authorize one-time HKLM Lab discovery/UAC in a separate framework.  
DEFERRED: G7C-G7M, negative live paths N1-N6, screenshot and interaction.  
NEXT_MOVE: In the owned PID `63828`, inspect Tools > Add-Ins for `BlueBrick Lab`. If present, load it and resume at G7B. If absent, retain `BLOCKED_ENVIRONMENT`; do not alter production registration.

## Chronicle metadata

WHY_SAVED: Preserve the first evidence-backed config repair and exact live Lab stopping point.  
RETRIEVAL_TRIGGER: Resume BlueBrick Lab React acceptance, investigate Lab add-in discovery, or consider one-time HKLM Lab registration.  
SOURCE: Current canonical checkout, exact deployed files, owned Lab process, read-only production status.  
STATUS: `BLOCKED_ENVIRONMENT`.  
SUPERSEDES: `BB20-UI-20260903-143600-R03` only for the config root-cause and G0-G6 Lab preparation; it does not supersede production runtime evidence.  
RECHECK: Recheck dirty state, production unsaved indicator, PID ownership, Lab registration, deployed hashes, and ports before any continuation.

