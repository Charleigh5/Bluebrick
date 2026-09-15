# BlueBrick React Preview R03 Receipt

## Decision

STATUS: ROUTING_ESCALATION_REQUIRED
TASK_ID: BB20-UI-20260903-143600-R03
DATE_TIME: 2026-09-03T14:36:00-04:00
MODEL: GPT-5.6 Luna execution lane; hard-routed to GPT-5.6 Sol
REASONING_MODE: HIGH
ROUTING_CONFIDENCE: 98
RUN_ID: BB20-UI-20260903-143600-R03

USER_INTENT: Determine the first proven divergence behind fallback-shell and reach a directly evidenced React/WebView2 preview without touching protected CAD/PDM/production state.
SUCCESS_GATE: Current React UI visibly rendered in SOLIDWORKS with matching runtime identity and bounded interaction evidence.
PRIOR_STATUS: ROUTING_ESCALATION_REQUIRED (R02)
FINAL_STATUS: ROUTING_ESCALATION_REQUIRED
TASK_CLASS: Cross-system runtime root-cause diagnosis and live acceptance
MUTATION_AUTHORITY: Read-only source/process/registry/config/localhost diagnostics and local receipt writing authorized; production DLL/config replacement, registry writes, SOLIDWORKS lifecycle changes, CAD/PDM/Epicor/database writes are not authorized.
CURRENT_SAFETY_BOUNDARY: No SOLIDWORKS close, restart, attach, or UI mutation. The current window title contains an unsaved-document indicator ("2 *").

## Starting identity

REPOSITORY: C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick
BRANCH: bluebrick-assistant-slice1-foundation
STARTING_SHA: f3c0905137da6ac5ffe3523d3aa852c26fa18c48
ENDING_SHA: f3c0905137da6ac5ffe3523d3aa852c26fa18c48
WORKTREE_STATE: Pre-existing dirty source/output work preserved. This R03 receipt and escalation receipt are the only files added by this continuation; no reset, clean, stash, stage, commit, or push performed.
REPOSITORY_AGENTS: No repository-level AGENTS.md found under the canonical checkout or its VIRA GITHUB parent. The supplied execution instructions and current source/receipts governed this run.

## Current runtime

CURRENT_SOLIDWORKS_PID: 62920
SOLIDWORKS_START_TIME: 2026-09-03T09:49:58.1053904-04:00
SOLIDWORKS_RESPONDING: true
SOLIDWORKS_WINDOW: SOLIDWORKS Professional 2025 SP5.0 - [80242617 - 2 *]
SOLIDWORKS_EXE: C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\SLDWORKS.exe
BLUEBRICK_DLL_PATH: C:\BlueBrick\BlueBrick.dll
BLUEBRICK_DLL_HASH: C6C38DF37B5853D8D97D09D0FA7236735A4B6F0933CE7D4923E9C5D269CC10AE
BLUEBRICK_DLL_VERSION: 1.0.13.4
CONFIG_PATH: C:\BlueBrick\config\appsettings.json
CONFIG_HASH: 2BC0B3E19169ADCC6C59376956B2F5E3B30034235C507DB5EB5CB0E3F93A9091
REGISTRATION_CODEBASE: file:///C:/BlueBrick/BlueBrick.DLL

The native module list did not expose the managed BlueBrick assembly. Runtime identity is therefore correlated from the registered CodeBase, startup log dependency loads from C:\BlueBrick, the current Production bridge, and exact deployed-assembly reflection; it is not claimed from native module enumeration.

## Configuration sources and effective values

CONFIG_SOURCES:

1. Current source candidate: C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick\config\appsettings.json, SHA-256 FBAB1714B17CCA824E293167C4D6394951B5FC896626503EC13DA970FCFC9C2B. Disk values: Agent.BridgePort=17178; Assistant.UseReactWebView=true; EnableUploads=true; RequireExplicitUploadConsent=true.
2. Production runtime candidate: C:\BlueBrick\config\appsettings.json, SHA-256 2BC0B3E19169ADCC6C59376956B2F5E3B30034235C507DB5EB5CB0E3F93A9091. Disk values: Agent.BridgePort=35001; Assistant.UseReactWebView=true; EnableUploads=true; RequireExplicitUploadConsent=true.
3. Lab source/deployment candidate: appsettings.lab.json, SHA-256 D4EC9F049E09F4815428406AC6E09F2F48C8A5BE42478C9F98ED471E5AE1C3B5; Lab bridge contract is 17179.

CONFIG_PRECEDENCE: Current source reads the fixed file selected by AppIdentity.ConfigPath(baseDirectory): assembly-location ancestry -> config\appsettings.json for Production or config\appsettings.lab.json for Lab -> JsonConvert.DeserializeObject -> ApplyDefaults. AssistantPanel calls AgentConfig.Load during assistant initialization. No environment-variable or user-config override path was found in the inspected current source; no hot-reload path was found.

EFFECTIVE_CONFIG_FROM_EXACT_DEPLOYED_DLL: Read-only reflection invoked C:\BlueBrick\BlueBrick.dll AgentConfig.Load() against its own config path and returned AgentBridgePort=17178, UseReactWebView=false, EnableUploads=false, RequireExplicitUploadConsent=false, Model=meta/llama-3.1-70b-instruct. This directly reproduces the defaulting behavior from the deployed artifact.

EFFECTIVE_CONFIG_RUNTIME: BridgePort=17178; AddinMode=Production; VaultMode=PDM; Configured=true; EnableUploads=false; RequireExplicitUploadConsent=false; AssistantWebViewStatus=fallback-shell; AssistantWebViewError=null.

## Port semantics

PORT_17178_ROLE: Production BlueBrick bridge. AppIdentity.BridgePort and LabDeploymentContract.ProductionBridgePort both define 17178. Authenticated GET /agent/selfcheck returned HTTP 200 healthy; authenticated GET /assistant/status returned HTTP 200.
PORT_17179_ROLE: Lab BlueBrick bridge. AppIdentity and LabDeploymentContract define 17179. No listener observed during this run.
PORT_35001_ROLE: No current runtime service role found in AppIdentity, AgentHttpServer, or LabDeploymentContract. It is present in the deployed production config and historical test/receipt references, but no listener was observed. It is a stale/configuration value, not evidence that 35001 and 17178 should be identical.
PORT_LISTENER_SNAPSHOT: 17178 LISTEN owned by PID 4/System through the Windows HTTP listener layer; 17179 absent; 35001 absent.

## First divergence and root cause

INITIAL_WEBVIEW_STATUS: fallback-shell (direct authenticated runtime status)
FINAL_WEBVIEW_STATUS: fallback-shell
FIRST_DIVERGENCE: Intended current source/build and deployed React contract -> effective runtime AgentConfig. The registered production runtime is an older C:\BlueBrick assembly, and its JSON contract ignores the internal Agent/Assistant settings, so UseReactWebView becomes false before React navigation is selected.
ROOT_CAUSE: PROVEN_RUNTIME + PROVEN_STATIC. The running registered production DLL is not the current source/build generation (deployed hash C6C38D..., modified 2026-08-27; current bin\Release hash 2C8A4006..., modified 2026-09-01; deployed assembly lacks current source's AgentConfig.LoadFrom method). The exact deployed DLL's AgentConfig.Load() reads C:\BlueBrick\config\appsettings.json but returns defaulted AgentBridgePort=17178 and UseReactWebView=false even though that file contains 35001 and true. Current source also leaves AgentConfig.Assistant and AssistantSettings.UseReactWebView as internal properties without JsonProperty annotations. AssistantWebViewHost then takes the documented false-config branch: StartReactNavigation() fails BeginReactLoad, NavigateFallbackAsync() calls NavigateToString(), and status is reported as fallback-shell.
ROOT_CAUSE_CONFIDENCE: HIGH for the observed fallback path; corrected source/build/lab runtime still required before promotion.
COUNTEREVIDENCE: The process is fresh relative to R02 frontend deployment, so stale startup state alone is not sufficient. WebView diagnostics show coreReady=true, so WebView2 initialization failure is not the first failure. Frontend source/production-deployed triplet hashes match, so frontend parity alone is not the cause. Port 17178 is the proven Production role; port difference is not itself a defect.

## Hypothesis matrix

| Hypothesis | Evidence For | Evidence Against | Test/result | Classification | Confidence |
|---|---|---|---|---|---|
| H1 stale process/startup state | Configuration is loaded during initialization | PID 62920 started 2026-09-03 after R02 frontend deployment and still reports fallback | Fresh current process remained fallback | REJECTED as sole root cause | High |
| H2 configuration precedence/binding mismatch | Source/deployed config hashes differ; deployed DLL reflection returns defaults from its own config | Exact runtime source path needs Sol repair/instrumentation for durable reporting | Distinct disk values -> deployed effective defaults | PROVEN contributing cause | High |
| H3 port semantic misinterpretation | Historical 35001 references exist | Current source maps Production=17178 and Lab=17179; 35001 has no runtime role | Source map + listener/health checks | REJECTED as root cause | High |
| H4 WebView2 initialization failure | None | Current bootstrap receipts have coreReady=true | Current diagnostics | REJECTED as first failure | High |
| H5 React path/navigation failure | React URI never appears | StartReactNavigation is skipped by false effective flag; fallback about:blank is observed | Current diagnostics and host source | NOT REACHED after earlier failure | High |
| H6 JS/React bootstrap failure | No React root or identity marker | React document was never loaded; only inline fallback script is present | Current diagnostics | NOT REACHED | High |
| H7 wrong/stale DLL/add-in generation | Registry points to C:\BlueBrick; deployed hash differs from current build; deployed assembly method shape is older | Managed module is not visible in native module list | Registry/startup log + exact deployed reflection | PROVEN | High |

## WebView and frontend evidence

WEBVIEW_CORE: PROVEN_RUNTIME coreReady=true.
WEBVIEW_DIAGNOSTIC: Latest current receipt C:\Users\cweir\AppData\Local\Temp\BlueBrick\WebViewDiagnostics\20260903_135108_055_bootstrap_periodic_25.json. It records documentUrl=about:blank, readyState=complete, rootChildCount=-1, blueBrickHeaderPresent=false, inline script only, no telemetry errors/resourceErrors/unhandledRejections, and result=BOOTSTRAP_RECEIPT_COMPLETE. The post-navigation and late receipts show the same fallback-document shape.
REACT_BOOTSTRAP: NOT_PROVEN; React document was not loaded.
DOM_MARKER: NOT_PROVEN; runtime identity DOM element was not present.
VISUAL_SCREENSHOT: NOT_PROVEN; diagnostic directory contains zero PNG files.
INTERACTION_SMOKE: NOT_RUN; React surface was not proven.

FRONTEND_SOURCE_HASHES:

- index.html: E370C9711592F2695CF0651F702AE809A69AC0DE9B635C6C5B4CC8A4A6C7237B
- assistant-index.css: 8B0281A263CD60E9A83A3CE332DC6D0C9A3BE2525337F3E3847080CFB5C87B27
- assistant-web.js: 9854EC54946987FBC91B069D62BBF56F416A8C42D99560316EBF52FF08BBE82B

DEPLOYED_HASHES_PRODUCTION: Exact match for all three files.
DEPLOYED_HASHES_LAB: index/css match; assistant-web.js is 84B5772808D0A9099E85A9775473BC9980459BC9FC65A05441D5A0915DF29678.
HASH_PARITY: Production frontend source/deployed PASS; production DLL/config versus current source/build NOT PASS.
RUNTIME_IDENTITY_EXPECTED: Current production bundle contains the identity surface but its embedded identity values resolve to UNKNOWN under the current artifact scan; the older Lab identity manifest expects LAB source commit 9a26e89bb2c7e320924b39a7edc65fa86fa0559f and build 20260902-095413. That older Lab manifest is not current Production runtime proof.
RUNTIME_IDENTITY_OBSERVED: NOT_REACHED.
IDENTITY_MATCH: NOT_PROVEN.

## Verification

| Command/probe | Result |
|---|---|
| git branch/rev-parse/status/worktree/log/diff --check in canonical checkout | PASS; pre-existing dirty state preserved; diff check only reported LF/CRLF warnings |
| Current SOLIDWORKS process/CIM/process response/module inspection | PASS; PID 62920 responding; managed BlueBrick not exposed by native module list |
| SHA-256 production DLL/config/frontend, source config, source/bin DLL, Lab artifacts | PASS; evidence captured above |
| Read-only HKCR/HKCU/HKLM registration inspection | PASS; production CodeBase points to C:\BlueBrick; no registry write |
| Authenticated GET /agent/selfcheck on 17178 | HTTP 200 healthy |
| Authenticated GET /assistant/status on 17178 | HTTP 200; fallback-shell; Production; BridgePort 17178; assistant booleans false |
| Read-only reflection of exact deployed DLL AgentConfig.Load() | PASS; reproduced effective defaults from deployed config |
| Read-only reflection of current bin\Release AgentConfig.LoadFrom() | PASS; current build also returned UseReactWebView=false from source config, exposing the remaining Assistant binding gap |
| Current WebViewDiagnostics JSON readback | PASS as evidence; core ready, fallback about:blank, no React root/marker |

BUILDS_RUN: None in R03. R02 static/build checks are inherited evidence only and were not rerun.
TESTS_RUN: None in R03. No source repair was attempted.

## Protected-state receipt

CAD_CHANGED: NO observed.
PDM_CHANGED: NO.
EPICOR_CHANGED: NO.
DATABASE_CHANGED: NO.
REGISTRY_CHANGED: NO.
DLL_CHANGED: NO.
CONFIG_CHANGED: NO.
PRODUCTION_MUTATED: NO.
UNAUTHORIZED_MUTATION_OBSERVED: NO.
CAD_STATE: No lifecycle action performed; unsaved-document indicator observed and preserved.

ROLLBACK_PATH: C:\BlueBrick\backups\BB20-UI-20260902-165104-R02
ROLLBACK_VERIFIED: YES for backup/deployment/pre-deployment manifest and backup parity from R02; restore not executed.

## Escalation and next move

MODEL_ESCALATION_REQUIRED: YES
SOURCE_MODEL: LUNA
TARGET_MODEL: SOL
TARGET_REASONING: HIGH
QUESTION_FOR_SOL: Repair and prove the configuration-binding/deployment-generation seam in a Lab-only or otherwise non-production route, then return a deterministic procedure for React activation without touching the active unsaved SOLIDWORKS session.
NEXT_DEPENDENCY: Sol-owned C# configuration contract repair/review and a fresh Lab-capable runtime. Any Lab registration/UAC or SOLIDWORKS close/restart remains a human/safety boundary.
NEXT_RECOMMENDED_MOVE: Add a regression test with disk values distinct from compiled defaults; make the Assistant top-level and JSON-configurable nested settings bind explicitly or use a reviewed public/opt-in contract; build Release/Lab; verify source/build/deployed identities; keep C:\BlueBrick production DLL/config unchanged; use C:\BlueBrickLab only after its registration boundary is approved and unsaved CAD safety is resolved.

KNOWN_GAPS: No corrected DLL was built/deployed; no production replacement; no Lab registration repair; no React navigation/DOM identity/visual screenshot/interaction smoke; no direct managed-module enumeration; no full runtime config-source telemetry beyond exact deployed reflection and correlated bridge/log evidence.
UNSUPPORTED_CLAIMS_REJECTED: React PASS, visual PASS, runtime identity match, bundle loaded by WebView, current source DLL loaded by SOLIDWORKS, port 35001 service ownership, and safe SOLIDWORKS restart are all rejected as unsupported.
WARNINGS: R02 receipt is stale for current purposes. Current source config and production config differ. Current source bundle identity scan resolves UNKNOWN. The current SOLIDWORKS window shows an unsaved indicator.
PROMOTION_DECISION: ROUTING_ESCALATION_REQUIRED
