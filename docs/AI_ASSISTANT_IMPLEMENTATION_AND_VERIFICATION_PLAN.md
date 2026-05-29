# Bluebrick AI Assistant Implementation And Verification Plan

Last updated: 2026-05-28

## Scope Alignment Against Rebuilt Master Plan

This plan has been reconciled against `C:\Users\cweir\Downloads\bluebrick_ai_assistant_rebuilt_master_plan.md`.

Important rule from that review: the current implementation slices are useful progress, but the assistant must now be governed by explicit P0 gates before any broad feature expansion or mutation-capable SolidWorks/PDM work.

The downloaded master plan adds these required refinements to the full scope:

- prove the active source-of-truth workspace before further implementation claims.
- close or prove fixed the second-chat `A task was canceled` regression.
- document AionUI/model-provider authority and secret boundaries.
- create a route manifest for every `/assistant/*`, `/sw/*`, `/pdm/*`, relay, and lab endpoint.
- harden local bridge and WebView2 message security.
- introduce explicit tool policy, authorization, audit log, and execution receipts.
- keep SolidWorks/PDM mutation blocked until read-only adapters, preview diffs, approval gates, and receipts exist.
- define a read-only SolidWorks snapshot adapter before any approved mutation workflow.
- treat React/Vercel AI SDK/AI Elements as a later packaged WebView2 panel milestone, not something to force into inline C# HTML now.
- maintain 2024 SP5 as the first SolidWorks target and treat 2026 support as a separate compatibility track.

## Full-Scope Refinements From Master Plan Review

This section is the reconciled engineering contract. It converts the downloaded master plan into concrete scope that can be implemented and verified without widening risk.

### Workspace And Source Authority

| Workspace | Role | Mutation Policy | Use In This Plan |
|---|---|---:|---|
| `VIRA BLUEBRICK` | legacy/original source reference | read-only | naming, add-in lifecycle, task pane/PMPage patterns, stable legacy behavior |
| `Bluebrick` | active implementation workspace | gated write | assistant panel, local bridge, relay, tests, SolidWorks adapters |
| `BlueBrick.ProjectSource` | governance/comparison layer if present | gated docs only | plans, ADRs, route maps, source-of-truth proof, handoffs |

Rule: do not treat aspirational docs, archived classes, copied Drive folders, or stale GitHub clones as implemented truth unless code/config/tests in the active `Bluebrick` workspace confirm them.

### Provider And Credential Boundary

Bluebrick must remain NVIDIA-compatible while allowing OpenAI-compatible and AionUI-routed models. The provider boundary is:

- NVIDIA-compatible profile remains a safe default and should not require cloud-specific UI assumptions.
- OpenAI Platform API keys, subscription-backed OAuth, or quota-bearing credentials may be used only through an approved runtime secret resolver, local broker, or OAuth flow.
- AionUI may be the model/provider authority, but Bluebrick must consume only a sanitized provider snapshot or a broker endpoint; it must not scrape AionUI config files for secrets.
- Registry persistence stores only selected profile IDs and non-secret display/capability metadata.
- Secret references use names such as `runtime-only`, `OPENAI_API_KEY`, `AIONUI_BROKER`, or `NVIDIA_COMPATIBLE_KEY`, not actual values.
- Missing credentials produce a visible provider-unavailable state and do not crash the panel.
- Vision-disabled profiles must reject screenshot analysis with a user-visible model capability message.

### Agent Modes

| Mode | Allowed | Blocked | First Phase |
|---|---|---|---|
| `MOCK` | UI, contracts, deterministic fake analysis, local tests | cloud calls, CAD/PDM mutation | P0 |
| `READ_ONLY_ANALYST` | model chat, screenshots after approval, local vault search, read-only PDM/Epicor/Salesforce summaries | mutation, checkout/checkin, save/rebuild/export | P1-P4 |
| `PREVIEW_ONLY` | proposed CAD/document changes, old/new property diffs, export plans | execution | P8 |
| `HUMAN_APPROVED_MUTATION` | exactly one approved action against a throwaway/test file | implicit or batch action | P9 |
| `BATCH_MODE` | none for current scope | all batch mutation | out of scope |

### Tool Classes

| Class | Examples | Required Controls |
|---|---|---|
| read | active document snapshot, selection snapshot, local vault search, PDM metadata | localhost auth, schema validation, timeout, resource limits, receipt |
| preview | custom-property diff, drawing-note plan, export packet plan | policy check, preview object, correlation ID, no COM mutation |
| mutation | apply property, create note, export file | explicit approval, main-thread COM execution, checkpoint/rollback note, receipt |
| external | Epicor, Salesforce, OpenAI/AionUI/NVIDIA provider calls | secret boundary, allowlist, timeout, redacted logs |
| forbidden | checkout/checkin, save-as, rebuild, batch edits, arbitrary `/sw/*` route calls | deny by default |

### Scope Status Labels

Use these labels in docs, tests, route manifests, and UI status text:

| Label | Meaning |
|---|---|
| `IMPLEMENTED` | runtime path exists and is wired |
| `PARTIAL` | runtime path exists but lacks live/manual validation or hardening |
| `STAGED_NOT_WIRED` | file/contract exists but no runtime path consumes it |
| `DOCS_ONLY` | described in plans only |
| `MISSING` | expected component absent |
| `BLOCKED_BY_POLICY` | intentionally denied until approval/security gates exist |
| `UNKNOWN` | not inspected or not enough evidence |

## P0 Gates Added From Master Plan

These gates are now part of the plan and should be completed before additional feature slices that expand capability or risk.

### P0.1 Source-Of-Truth Gate

Objective: prove Codex is modifying the same active `Bluebrick` workspace that SolidWorks will load.

Artifact:

```text
docs/BLUEBRICK_SOURCE_OF_TRUTH_PROOF.md
```

Required command evidence:

```powershell
pwd
git status --short
git branch --show-current
git log --oneline --decorate -n 5
git diff --stat
git diff --name-status
git ls-files Agent BlueBrick.Relay config docs BlueBrick.UI.Tests BlueBrick.Relay.Tests Forms AssistantPanel.cs AssistantPanel.Designer.cs
```

Pass condition:

- path is the intended active workspace.
- `Agent/`, `BlueBrick.Relay/`, `config/`, `docs/`, and test projects are either tracked or intentionally staged as local WIP.
- no patching occurs in stale GitHub clones, copied Drive folders, build output folders, or unrelated workspaces.

Current gate result: PARTIAL. The active workspace, branch, commit, and dirty state were captured, but assistant/relay/config/docs/test files are currently untracked WIP and not yet a durable Git baseline.

### P0.2 Cancellation Regression Gate

Problem to prove fixed: a second chat in the AI Assistant tab previously surfaced raw `A task was canceled.` and panel refresh temporarily worked around it.

Required behavior:

| Scenario | Expected |
|---|---|
| Send chat #1 | succeeds |
| Send chat #2 without panel refresh | succeeds |
| Cancel active request | only current request stops |
| Send chat #3 after cancel | succeeds |
| Timeout | friendly classified timeout error |
| Bridge offline | friendly bridge unavailable error |
| Provider error | provider-specific error category, no stack/secrets |

Required implementation refinements:

- fresh `CancellationTokenSource` per request. Implemented in `AssistantPanel.SendAsync`.
- never reuse a canceled token. Implemented by using request-local ownership and `OwnsStreamCancellationSource`.
- distinguish user cancel, timeout, bridge disconnect, provider error, JSON parse error, and WebView navigation cancellation.
- propagate request ID/correlation ID from WebView/UI through bridge/provider.
- add a Stop action only after the cancellation model is deterministic.

Current gate result: PARTIAL. The panel now uses request-local cancellation ownership, passes the token into streaming and fallback JSON calls, and avoids clearing a newer request token from an older request's `finally` block. `Agent/AssistantErrorClassifier.cs` now classifies cancellation, timeout, bridge unavailable, auth failure, HTTP failure, provider failure, and JSON parse failure into stable error codes. Focused unit coverage exists for ownership behavior and classifier mappings. Live second-chat UI validation and live bridge/provider failure validation are still missing.

Target tests:

```text
AssistantChatCancellationTests
  Send_FirstAndSecondSequentialMessages_NoPanelRefresh_Succeeds
  Cancel_ActiveRequest_DoesNotPoisonNextRequest
  Timeout_ShowsFriendlyClassifiedError
  BridgeOffline_ShowsFriendlyBridgeError
```

### P0.3 AionUI Model Authority ADR

Decision to record: AionUI should be treated as the model/provider authority where applicable, but BlueBrick must not scrape, duplicate, or persist AionUI secrets.

Rules:

- registry may persist only selected profile IDs.
- provider keys must stay outside repo, config examples, logs, transcripts, screenshots, and registry plaintext where possible.
- OpenAI, NVIDIA-compatible, AionUI, local, and future broker profiles must resolve secrets at runtime.
- unavailable secret or broker means visible unavailable state, not a crash.
- screenshot attached to a vision-disabled profile must be denied or prompt a model switch.

Target document:

```text
docs/BLUEBRICK_AIONUI_MODEL_AUTHORITY_ADR.md
```

Current gate result: CREATED. The ADR records AionUI as the preferred model/provider authority when available, keeps NVIDIA-compatible and OpenAI-compatible profiles in scope, and forbids persisted provider secrets in repo/config/log/UI surfaces.

### P0.4 Model Profile Contract

The current `AssistantModelProfile` implementation should evolve toward this durable shape:

```json
{
  "id": "aionui-default",
  "display_name": "AionUI Default",
  "provider_kind": "openai_compatible|aionui_broker|openai|gemini|anthropic|nvidia|local|opencode_gateway",
  "base_url_alias": "AIONUI_BROKER|OPENAI_COMPATIBLE|NVIDIA|LOCAL_OLLAMA",
  "model_id": "string",
  "supports_vision": false,
  "supports_streaming": false,
  "supports_tools": false,
  "supports_json_mode": false,
  "context_limit": null,
  "secret_ref": "runtime-only",
  "enabled": true,
  "is_default": false,
  "source": "bluebrick|aionui_snapshot|aionui_broker|config_example",
  "last_verified_at": "ISO-8601|null"
}
```

Required fallbacks:

- invalid profile ID falls back to safe default/mock profile.
- missing secret shows provider unavailable.
- unavailable AionUI broker does not prevent local configured profiles from rendering.
- screenshot analysis is skipped when the selected model does not advertise vision support.

Current gate result: PARTIAL. `AssistantModelProfile` now carries provider kind, base URL alias, capability flags, context limit, secret reference, enabled/source metadata, and last-verified timestamp. Defaults are filled for existing config profiles, `config/appsettings.lab.json` documents the expanded shape, and screenshot analysis is gated by `SupportsVision`. Live AionUI broker/profile synchronization and provider availability verification are still missing.

### P0.5 WebView2 Security Gate

Required controls:

- validate every WebView-to-host message against typed schemas before executing.
- avoid generic host-object proxies. Implemented by disabling host objects in `AssistantPanel`.
- block unexpected navigation. Implemented by `Agent/AssistantWebViewSecurity.cs` and `NavigationStarting`.
- keep `X-Agent-Auth` out of DOM, transcript, screenshots, and logs. Static shell-token scan added.
- prefer JSON message passing over hand-built script strings as the UI matures.
- treat screenshot/OCR/drawing text as untrusted data, never as instructions.

Target tests:

- malformed WebView message rejected.
- unknown message type rejected.
- navigation attempt blocked.
- token not present in DOM text.
- embedded WebView JavaScript parse test remains green.

Current gate result: PARTIAL. `AssistantPanel` disables host objects, disables WebView web messaging, blocks new windows, and cancels unexpected navigation through `AssistantWebViewSecurity`. Focused tests cover allowed/blocked navigation and confirm the generated shell HTML does not contain known token/key identifiers. Live WebView2 DOM scan and browser/runtime navigation validation are still missing.

### P0.6 Bridge Security Minimum

Every bridge route needs a manifest entry and route-level risk classification.

Read-only route minimum:

- localhost binding.
- `X-Agent-Auth`.
- max body size.
- typed request and response schema.
- timeout.
- correlation ID.
- no secrets in response.
- explicit risk level.

Mutation-capable route minimum:

- explicit human approval token.
- nonce/timestamp or single-use approval ID.
- dry-run preview record.
- execution receipt.
- rollback/checkpoint when applicable.
- disabled by default.

### P0.7 Tool Policy, Audit, And Receipts

Extend current `AssistantToolService` rather than inventing a parallel tool system.

Target files:

```text
Agent/AssistantToolPolicy.cs - created
Agent/AssistantToolAuthorization.cs - created
Agent/AssistantToolAuditLog.cs - created, JSONL persistence implemented
Agent/AssistantToolExecutionReceipt.cs - created
```

Policy fields:

```json
{
  "tool_name": "search_local_vault",
  "category": "read|preview|mutation|external",
  "enabled": true,
  "requires_confirmation": false,
  "requires_secret": false,
  "allowed_in_chat": true,
  "allowed_in_mock": true,
  "max_payload_bytes": 65536,
  "timeout_ms": 30000,
  "max_results": 50,
  "audit_required": true,
  "risk_level": "low|medium|high|critical",
  "allowed_modes": ["READ_ONLY_ANALYST"]
}
```

Deny rules:

- unknown tool: deny.
- disabled tool: deny with visible reason.
- missing config/secret: deny with visible reason.
- mutation without approval: deny.
- `/sw/*` and mutating `/pdm/*` cannot be invoked from general chat.

Current gate result: PARTIAL. `Agent/AssistantToolPolicy.cs` now denies assistant-driven `/sw/*`, native `/pdm/*`, `/lab/vault/reset`, unknown mutation routes, and route-shaped tool-name aliases. `AssistantToolService` evaluates tool names through this policy before catalog lookup and attaches `AssistantToolExecutionReceipt` records to tool results. `AssistantToolAuthorization` exists, and `AssistantToolAuditLog` now writes redacted JSONL receipts when a vault log root is configured. UI receipt surfacing and live route-level validation are still missing.

UI/API update: `/assistant/tool-audit` now exposes recent redacted receipts, and `AssistantPanel` renders recent Activity Receipts plus per-result receipt metadata for assistant tool calls.

Receipt shape:

```json
{
  "receipt_id": "uuid",
  "timestamp": "ISO-8601",
  "session_id": "uuid",
  "correlation_id": "uuid",
  "mode": "READ_ONLY_ANALYST|PREVIEW_ONLY|HUMAN_APPROVED_MUTATION",
  "model_profile_id": "string",
  "tool_name": "string",
  "tool_category": "read|preview|mutation|external",
  "input_summary": "redacted string",
  "approval": {
    "required": false,
    "granted": false,
    "approval_id": null
  },
  "result": "success|partial|failed|denied",
  "duration_ms": 0,
  "safe_to_retry": false,
  "errors": []
}
```

## Route Manifest Requirement

Add and maintain:

```text
docs/BLUEBRICK_ASSISTANT_ROUTE_MANIFEST.md
```

Current gate result: CREATED. The manifest inventories `/agent/*`, `/assistant/*`, `/sw/*`, `/pdm/*`, `/qa/run`, `/lab/*`, `/chatgpt/*`, and `/relay/*` routes declared in `Agent/AgentHttpServer.cs` and classifies CAD/PDM/lab reset/job mutation surfaces as blocked from assistant-driven use until policy, approval, and receipts exist.

Initial manifest rows:

| Route | Method | Risk | Auth | Current State | Required Test |
|---|---|---:|---:|---|---|
| `/assistant/status` | GET | Low | Yes | implemented | status/auth test |
| `/assistant/models` | GET | Low | Yes | implemented | profile catalog test |
| `/assistant/model` | POST | Medium | Yes | implemented | fallback/persistence test |
| `/assistant/tools` | GET | Low | Yes | implemented | catalog test |
| `/assistant/tool` | POST | Medium/High | Yes | implemented | policy deny/allow tests |
| `/assistant/screenshot` | POST | Medium | Yes | implemented | mock + live capture test |
| `/assistant/screenshot/analyze` | POST | Medium | Yes | mock implemented | vision/privacy gate test |
| `/assistant/integrations` | GET | Low | Yes | implemented | disabled-state test |
| `/assistant/document-catalog` | GET | Low | Yes | implemented | descriptor test |
| `/sw/*` | varies | Critical | Yes + approval required | existing/guarded | deny from chat test |
| `/pdm/*` mutation | varies | Critical | Yes + approval required | existing/guarded | deny from chat test |

## SolidWorks Adapter Scope Added

The next major product milestone after P0 is a read-only SolidWorks snapshot adapter, not mutation.

Target structure:

```text
SolidWorks/Runtime/SolidWorksRuntimeInfo.cs
SolidWorks/Runtime/SolidWorksVersion.cs
SolidWorks/Runtime/SolidWorksThreadGuard.cs
SolidWorks/Runtime/SolidWorksMainThreadDispatcher.cs
SolidWorks/Adapters/ISolidWorksAppAdapter.cs
SolidWorks/Adapters/IModelDocAdapter.cs
SolidWorks/Adapters/IDrawingDocAdapter.cs
SolidWorks/Adapters/IAssemblyDocAdapter.cs
SolidWorks/Adapters/IPartDocAdapter.cs
SolidWorks/Adapters/ISelectionAdapter.cs
SolidWorks/Adapters/ICustomPropertyAdapter.cs
SolidWorks/Adapters/IExportAdapter.cs
Agent/SolidWorksReadOnlySnapshotService.cs
Agent/AssistantContextBuilder.cs
```

Read-only first capabilities:

| Capability | Assistant Use | Risk | Phase |
|---|---|---:|---|
| SolidWorks version/SP detection | version gating | Low | P1 |
| active document title/type/path hash | context card | Low/Privacy | P1 |
| dirty/read-only state | mutation gate | Low | P1 |
| selection snapshot | explain selected entities | Medium | P1 |
| custom properties read | quote/review data | Medium | P1 |
| drawing sheet/view names | drawing context | Medium | P1 |
| BOM/table read | BOM validation | Medium | P2 |
| assembly component tree read | structure summary | Medium | P2 |

Blocked until later:

- save/save-as.
- rebuild.
- checkout/checkin.
- dimension/feature edits.
- batch file operations.
- PDM state changes.

COM/threading requirements:

- no SolidWorks COM calls from arbitrary background threads.
- marshal COM work through the add-in/main thread.
- convert COM objects into serializable snapshots quickly.
- avoid long-lived COM objects inside async closures.
- unknown thread/version state returns read-only blocked response.

## Current Evidence Snapshot

- The assistant panel is mounted by `Forms/FrmPane.cs` through `EnsureAssistantPanelAsync()` and hosted in `pnlAssistantHost`.
- The WinForms/WebView2 assistant UI lives in `AssistantPanel.cs` and `AssistantPanel.Designer.cs`.
- The local bridge is `Agent/AgentHttpServer.cs`; it protects every route with `X-Agent-Auth`.
- The assistant client is `Agent/AgentPanelClient.cs`.
- The model/runtime service is `Agent/OpenAiAssistantService.cs`.
- Relay/MCP-facing web service lives in `BlueBrick.Relay/`.
- PDM actions are available through `/pdm/*` and SolidWorks actions through `/sw/*`; these must remain gated and must not be invoked from general chat without explicit user confirmation.

## Completed Implementation Slices

### Slice 1 - Model Routing

Completed:

- `AssistantModelProfile` catalog added.
- `/assistant/models` and `/assistant/model` added to the bridge.
- assistant model selection persists to the Bluebrick registry.
- NVIDIA-compatible profile remains the default.
- OpenAI and AionUI profiles are visible in config.
- model profiles expose capability flags for vision/tools/JSON mode.
- assistant panel has a model dropdown.

Verified:

- `BlueBrick.csproj` Lab build passed.
- `BlueBrick.UI.Tests` build passed after fixing test package hint paths.
- focused `LabWorkspaceTests` passed.
- `BlueBrick.Relay.Tests` passed.

### Slice 2 - Panel Shell And Screenshot Contracts

Completed:

- assistant shell composer was expanded to multiline input.
- action buttons now have clearer visual roles:
  - capture highlighted
  - mode/test/working actions neutral
  - reset vault marked as destructive
- WebView chat surface now has a structured header, status rail, empty state, and readable message cards.
- screenshot capture now produces an `AssistantScreenshotArtifact` metadata object with:
  - session id
  - file path
  - capture timestamp
  - window title
  - image dimensions
  - annotation list
  - extracted contact list
- `/assistant/screenshot` returns both `path` and `artifact`.
- contract classes added for `AssistantScreenshotAnnotation` and `AssistantExtractedContact`.
- focused tests now cover screenshot annotation/contact data shape.

Verified:

- `BlueBrick.csproj` Lab build passed.
- `BlueBrick.UI.Tests` build passed.
- focused `LabWorkspaceTests` passed.
- `BlueBrick.Relay.Tests` passed.

Still unverified:

- rendered assistant panel inside SolidWorks after these UI changes.
- real screenshot capture while SolidWorks is foreground.
- real OCR/contact extraction; only contracts exist so far.

### Slice 3 - Assistant Tool Catalog And Safe Local Search Contract

Completed:

- `AssistantToolDescriptor`, `AssistantToolRequest`, `AssistantToolResult`, and `AssistantToolResultItem` contracts added.
- `Agent/AssistantToolService.cs` added as the assistant-facing tool boundary.
- `/assistant/tools` added to expose the assistant tool catalog.
- `/assistant/tool` added for assistant tool execution.
- `search_local_vault` is enabled as a read-only local vault search.
- `search_pdm` is visible but disabled until a read-only assistant wrapper is implemented.
- `search_epicor` is visible but disabled until legacy Epicor SQL/UI code is replaced with a parameterized config-gated service.
- `capture_screenshot` is visible in the catalog as a read-only visual tool contract.
- tests now prove the catalog separates safe local search from disabled PDM/Epicor connectors.
- `AgentPanelClient` can fetch assistant tools and execute assistant tool calls.
- the assistant panel now has a `Vault` action that runs `search_local_vault` using the prompt box as the query.
- local vault reset is no longer one-click; it now requires an explicit confirmation dialog.
- a focused test now indexes a generated local vault artifact and verifies `search_local_vault` returns it as a read-only assistant result.
- the WebView assistant shell now renders bridge, mode, relay, model, and tool-catalog state instead of hiding that context in WinForms-only controls.
- local vault results now render as structured result cards in the transcript instead of plain text.
- screenshot capture now appends a first-class screenshot artifact card into the transcript with source window, file name, dimensions, annotation count, and contact count.
- screenshot artifact normalization is covered by a focused UI test.
- `/assistant/screenshot/analyze` now exists as the screenshot analysis boundary.
- mock screenshot analysis adds a deterministic review-region annotation and extracts email/phone-shaped contact data from available screenshot metadata/hint text.
- capture now calls the analysis endpoint before rendering the screenshot card, so annotation/contact counts can populate immediately in mock mode.
- PDM and Epicor assistant search now have explicit config gates under `AssistantTools`.
- PDM assistant search has a read-only EPDM search wrapper that is disabled unless `AssistantTools.EnablePdmSearch` is explicitly true.
- Epicor assistant search has a parameterized read-only part-search wrapper that is disabled unless `AssistantTools.EnableEpicorSearch` is true and the configured connection-string environment variable is present.
- config files document disabled defaults for PDM/Epicor assistant search.
- `/assistant/integrations` now exposes assistant-readable integration status for Salesforce, PDM, and Epicor.
- `/assistant/document-catalog` now exposes useful assistant document/artifact types, including drawing PDFs, PDF packets, STEP/DXF exports, screenshot artifacts, and a planned Salesforce opportunity brief.
- Salesforce is explicitly classified as planned/read-only-first/OAuth-required; the archived legacy class remains reference material, not runtime integration.
- `AgentPanelClient` can fetch integration and document catalogs.
- the WebView assistant shell now renders compact Integration and Document context cards so Salesforce/PDM/Epicor status and available artifact types are visible in-panel.

Verified:

- `BlueBrick.csproj` Lab build passed.
- `BlueBrick.UI.Tests` build passed.
- focused `LabWorkspaceTests` command exited successfully.
- extracted embedded WebView JavaScript parsed successfully with Node.
- mock screenshot analysis test passed through the focused `LabWorkspaceTests` command.
- PDM/Epicor config-gating test passed through the focused `LabWorkspaceTests` command.
- Salesforce/document catalog test passed through the focused `LabWorkspaceTests` command.
- WebView catalog-rendering JavaScript parsed successfully with Node.

Still unverified:

- `/assistant/tools` and `/assistant/tool` through a live running add-in bridge.
- visual rendering of the refreshed WebView assistant shell inside SolidWorks.
- visual rendering of the integration/document context cards inside SolidWorks.
- real screenshot card rendering after capturing a SolidWorks foreground window.
- real OCR/vision extraction from screenshot pixels.
- local vault search from the rendered assistant panel inside SolidWorks.
- real PDM/Epicor search, intentionally not enabled yet.

## Product Target

Build the Bluebrick AI Assistant into a functional SolidWorks task-pane copilot that can:

1. Chat with selectable model/provider profiles.
2. Capture SolidWorks screenshots and attach them to model requests.
3. Support screenshot review, annotation, and later structured contact extraction.
4. Search local vault/PDM/Epicor data through explicit, audited actions.
5. Support Salesforce only through a new OAuth-backed integration, not the archived legacy class.
6. Present a usable, professional assistant panel instead of a cramped button grid.
7. Validate every bridge, relay, assistant, and risky CAD/PDM workflow with repeatable tests.

## SDK Direction

### Vercel AI SDK / AI Elements

Use these for any new web-based assistant surface when the panel moves from inline HTML to a packaged React/WebView2 app.

Recommended role:

- `@ai-sdk/react` `useChat` for chat state and streaming if/when the assistant panel becomes a React app.
- AI Elements for production-ready message, conversation, prompt input, tool-call, reasoning, and markdown components.
- Vercel AI Gateway only if Bluebrick adds a Node/TypeScript backend or web panel runtime that can own those credentials and routing.

Do not force AI SDK directly into the current C# inline WebView2 string. The current C# service can keep using OpenAI-compatible HTTP while the panel is still WinForms-first.

### Vercel Chat SDK

Use only if Bluebrick needs a shared bot deployed to Slack, Teams, Discord, Google Chat, GitHub, Linear, or similar channels. It is not the right first dependency for the embedded SolidWorks task pane.

### SOLIDWORKS And Open-Source Reference Alignment

Use now:

- SOLIDWORKS interop assemblies already used by the add-in.
- SOLIDWORKS PDM API only through read-only/config-gated wrappers until separate approval.
- WebView2 for the current embedded task-pane assistant.
- BlueBrick.Relay for browser-safe relay and handoff validation.

Evaluate as reference, not automatic adoption:

- `xarial/xcad` and `xarial/xcad-examples` for add-in, task pane, PropertyManagerPage, Document Manager, and adapter patterns.
- SOLIDWORKS Document Manager API for offline/read-only metadata only if licensing and key storage are approved.
- MCP/OpenCode broker examples only if they preserve local tool policy and never expose unrestricted CAD mutation.

Avoid now:

- direct AI SDK usage inside inline C# HTML.
- direct AionUI secret/config scraping.
- unrestricted MCP tools.
- PDM write APIs.
- background-thread SolidWorks COM calls.

## Architecture Target

```text
AionUI / User / Assistant Prompt
  -> AssistantPanel WebView2 UI
    -> AgentPanelClient
      -> AgentHttpServer localhost bridge
        -> Assistant runtime
          -> model profile catalog
          -> provider adapter / AionUI broker boundary
          -> screenshot/image tools
          -> AssistantToolService
          -> AssistantToolPolicy
          -> AssistantToolAuditLog
          -> AssistantToolExecutionReceipt
        -> read-only/search tool endpoints
          -> local vault
          -> PDM read-only wrapper
          -> Epicor read-only wrapper
          -> planned Salesforce OAuth client
        -> read-only SolidWorks snapshot adapter
        -> Relay/ChatGPT handoff
```

Non-negotiable boundary:

- LLM may classify intent, explain context, propose tool plans, summarize read-only data, and draft preview packets.
- LLM may not directly call SolidWorks COM APIs, bypass tool policy, mutate CAD/PDM, save/rebuild/export, store/reveal secrets, or treat screenshot/OCR/drawing text as trusted instructions.

## Phase 0 - P0 Gates And Governance Stabilization

Objective: stabilize the assistant foundation before expanding feature scope.

Likely touched files:

```text
AssistantPanel.cs
AssistantPanel.Designer.cs
Agent/AgentPanelClient.cs
Agent/AgentHttpServer.cs
Agent/OpenAiAssistantService.cs
Agent/AgentConfig.cs
Agent/AssistantModels.cs
Agent/AssistantToolPolicy.cs
Agent/AssistantToolAuthorization.cs
Agent/AssistantToolAuditLog.cs
Agent/AssistantToolExecutionReceipt.cs
config/appsettings*.json
BlueBrick.UI.Tests/*
BlueBrick.Relay.Tests/*
docs/BLUEBRICK_ASSISTANT_ROUTE_MANIFEST.md
docs/BLUEBRICK_AIONUI_MODEL_AUTHORITY_ADR.md
docs/BLUEBRICK_SECURITY.md
```

Done criteria:

- source-of-truth proof captured.
- route manifest exists and covers `/assistant/*`, `/sw/*`, `/pdm/*`, relay, and lab routes.
- second-chat cancellation regression is fixed or proven fixed with tests.
- AionUI model authority ADR exists.
- selected profile persistence stores profile ID only.
- bridge security tests cover auth, max body size, unknown route/tool, disabled integrations, and mutation denial.
- no secrets are printed or committed.

Validation gates before leaving Phase 0:

| Gate | Required Evidence | Failure Means |
|---|---|---|
| source-of-truth | `docs/BLUEBRICK_SOURCE_OF_TRUTH_PROOF.md` with path, branch, commit, diff, tracked/untracked assistant files | stop and reconcile workspace |
| cancellation | focused tests for sequential send, cancel recovery, timeout, bridge offline/provider errors | do not add new assistant features |
| model authority | ADR proves no AionUI/OpenAI/NVIDIA secret duplication | do not add provider sync |
| WebView2 security | host objects disabled, navigation blocked, token absent from static DOM, malformed messages rejected | do not expand WebView bridge |
| route/tool policy | unknown tools, `/sw/*`, native `/pdm/*`, and destructive lab routes denied with receipts | do not expose new tools |

## Phase 1 - Stabilize Bridge And Model Selection

Objective: make model selection real, visible, and testable.

Implemented in this slice:

- Add `AssistantModelProfile` catalog.
- Add `/assistant/models`.
- Add `/assistant/model`.
- Persist selected model profile in registry.
- Add model selector to `AssistantPanel`.
- Keep NVIDIA-compatible default while allowing OpenAI and AionUI profiles.

Done criteria:

- `BlueBrick.csproj` builds in `Lab`.
- `/assistant/models` returns configured model profiles.
- model dropdown appears in the assistant panel.
- selected model updates assistant status and request routing.
- no secret values are stored in repo.
- missing OpenAI/AionUI/NVIDIA-compatible credentials show provider-unavailable status instead of an exception.
- OpenAI subscription/API-key/OAuth credential support is routed through a runtime secret resolver or broker, not a checked-in config value.

Validation:

```powershell
& "$env:USERPROFILE\.dotnet\dotnet.exe" build .\BlueBrick.csproj -c Lab -v minimal --no-restore
& "$env:USERPROFILE\.dotnet\dotnet.exe" test .\BlueBrick.UI.Tests\BlueBrick.UI.Tests.csproj --no-build -c Debug --filter "FullyQualifiedName~LabWorkspaceTests" --verbosity minimal
```

## Phase 2 - Redesign Assistant Panel UI

Objective: replace the cramped button-grid UI with a dense but usable assistant workspace.

Target layout:

- Top compact status bar:
  - provider/model selector
  - mode pill: mock/real
  - bridge indicator
  - relay indicator
- Chat body:
  - message list
  - streaming state
  - attachment chips
  - model/source footer per response
- Action rail:
  - capture screenshot
  - attach file
  - search vault/PDM
  - search Epicor
  - open working folder
  - open ChatGPT handoff
- Safety section:
  - no CAD/PDM write actions without confirmation
  - destructive actions hidden behind explicit confirmation dialogs
- Input composer:
  - multiline prompt
  - send/stop
  - selected context summary

Implementation options:

- Short term: improve existing inline HTML/CSS in `AssistantPanel.cs` and WinForms layout in `AssistantPanel.Designer.cs`. Initial shell refresh is complete, but manual SolidWorks visual QA is still required.
- Current short-term state: bridge/mode/model/tool state renders inside WebView, local vault results render as cards, and destructive reset requires explicit confirmation.
- Medium term: move the panel UI to a local static React app loaded by WebView2.
- Long term: use Vercel AI Elements in that React app for conversation components.

Done criteria:

- Panel is usable at task-pane width.
- Text does not overflow buttons or controls.
- All major actions have clear status feedback.
- Mock mode remains fully usable without external keys.

## Phase 3 - Screenshot Review, Annotation, And Contact Extraction

Objective: make screenshots first-class assistant artifacts.

Current state:

- `Agent/AssistantImageTools.cs` can capture the foreground window and prepare image attachments.
- `OpenAiAssistantService` can send image attachments through OpenAI-compatible chat-completions image payloads.
- screenshot metadata, annotation, and extracted-contact contracts now exist.
- `AssistantPanel` renders screenshot artifacts as transcript cards with dimensions, source title, annotation count, and contact count.
- `AssistantScreenshotAnalyzer` provides a mock analysis contract and metadata-based contact extraction placeholder.

Needed work:

- Capture the SolidWorks/task-pane window intentionally, not just arbitrary foreground window.
- add active document title/path to screenshot metadata.
- add model/provider to screenshot metadata.
- add region-based screenshot annotation rendering.
- implement model-driven OCR/vision extraction into the `AssistantExtractedContact` contract.
- Add review UI for accepting/rejecting extracted contacts.

Required privacy metadata:

```json
{
  "artifact_id": "uuid",
  "session_id": "uuid",
  "source_window_title": "SOLIDWORKS",
  "solidworks_document_title": "string",
  "solidworks_document_path_hash": "sha256",
  "redaction_applied": false,
  "sent_to_model": false,
  "retention_policy": "delete_on_session_end|manual_keep",
  "annotation_count": 0,
  "contact_count": 0,
  "model_profile_id": "string|null"
}
```

Cloud-send rule: before a screenshot is sent to a real cloud model, the user must see and approve that send. Mock mode never sends screenshots externally.

Validation:

- unit test image preparation with sample PNG/PDF.
- mock-mode screenshot flow creates a file and session attachment.
- real-mode test only after API key is configured.

## Phase 4 - Tool Search Integration

Objective: expose useful search tools to the assistant without letting chat mutate CAD/PDM state.

Current state:

- `Agent/AssistantToolService.cs` owns the first assistant-facing tool boundary.
- `Agent/AssistantToolPolicy.cs` owns the first route/tool deny policy for CAD, native PDM, destructive lab, and unknown mutation routes.
- `Agent/AssistantToolExecutionReceipt.cs` defines receipt metadata attached to assistant tool results.
- `Agent/AssistantToolAuditLog.cs` records in-memory receipts for the current process and persists redacted JSONL receipts under the configured vault log root.
- `GET /assistant/tools` returns read-only tool descriptors.
- `GET /assistant/tool-audit` returns recent redacted assistant tool receipts.
- `POST /assistant/tool` executes currently enabled assistant tools.
- `search_local_vault` uses `LocalVaultWorkspace.Search()` and returns normalized result items.
- `AssistantPanel` exposes a visible search-source selector plus a generic `Search` button; `/vault`, `/pdm`, and `/epicor` prompt prefixes remain supported as overrides.
- the search-source selector now updates the host status label and button text for the selected source, including disabled PDM/Epicor reasons.
- `search_pdm` is discoverable and can execute a read-only EPDM filename search only when explicitly enabled by config.
- `search_epicor` is discoverable and can execute a parameterized read-only Epicor part search only when explicitly enabled by config and an environment connection string.

Read-only first:

- local vault search: contract implemented
- PDM search: implemented behind disabled-by-default config gate
- PDM file metadata lookup: missing assistant wrapper
- PDM file history lookup: missing assistant wrapper
- PDM contains/where-used references: missing assistant wrapper
- PDM local cache/status lookup: missing assistant wrapper
- Epicor part search: implemented behind disabled-by-default config/env gate
- Epicor opportunity/task lookup: staged but disabled
- generated artifact search: covered only if present in the local vault index

Write or mutation gated:

- PDM checkout/checkin
- generate documents
- generate STEP/DXF/PDF packages
- apply SolidWorks properties
- open or modify CAD documents

PDM read-only wrapper target:

```text
Agent/AssistantToolService
  -> Agent/Pdm/PdmReadOnlyToolAdapter.cs
      -> PdmConnectionResolver
      -> PdmSearchService
      -> PdmMetadataReader
      -> PdmReferenceReader
      -> PdmResultSanitizer
      -> AssistantToolResult
```

Required PDM config shape:

```json
{
  "AssistantTools": {
    "EnablePdmSearch": false,
    "PdmVaultName": "",
    "PdmReadOnlyMaxResults": 25,
    "PdmTimeoutMs": 15000
  }
}
```

PDM result redaction:

- redact full local paths unless the user approves local-path display.
- avoid unnecessary usernames and private folder names in model-bound summaries.
- keep vault internal IDs out of model prompts unless required for a follow-up read-only lookup.
- never expose checkout/checkin or workflow-state mutation through the general assistant chat path.

Epicor wrapper target:

```text
Agent/Epicor/EpicorReadOnlySearchService.cs
Agent/Epicor/EpicorQueryPolicy.cs
Agent/Epicor/EpicorResultSanitizer.cs
```

Epicor rules:

- disabled by default.
- connection string comes from an environment variable or approved secret store only.
- parameterized queries only.
- log query shape and result count, not sensitive input values or connection details.
- first live validation uses a known safe, non-sensitive part query.

Salesforce wrapper target:

```text
Agent/Salesforce/SalesforceOAuthStatusService.cs
Agent/Salesforce/SalesforceReadOnlyClient.cs
Agent/Salesforce/SalesforceResultSanitizer.cs
```

Salesforce rules:

- OAuth 2.0 connected app only.
- token storage outside repo/config, preferably Windows Credential Manager or another approved secret store.
- first objects are read-only `Account`, `Contact`, `Opportunity`, and approved quote/document link metadata.
- no writeback until a separate ADR and approval gate exist.

Needed work:

- Extend `AssistantToolPolicy` with route-level integration checks before any write-capable tool is exposed.
- Expand audit/receipt visibility in the host UI for future PDM/Epicor/SolidWorks approval workflows.
- Return tool availability summary in `/assistant/status`.
- Add explicit confirmation UI for write actions.
- Log every tool call with trace id, session id, action, inputs summary, result, and user approval.
- Improve the `search_local_vault` UI result rendering after live panel QA.
- Improve the visible search-source selector styling during the broader React/WebView2 panel redesign.
- Run approved live PDM search validation on a safe query.
- Run approved live Epicor read-only search validation with a non-secret environment connection string.
- Expand Epicor wrappers for opportunity/task lookup after part search is validated.

Validation:

- policy tests prove `/sw/*`, native `/pdm/*`, `/lab/vault/reset`, and route-shaped tool aliases cannot be triggered through assistant tool execution.
- receipt tests prove allowed and denied tool attempts emit receipt metadata, in-memory audit events, and redacted JSONL persisted audit records.
- read-only search tests use fakes or local samples.
- UI command-routing tests prove visible selector choices and `/vault`, `/pdm`, `/epicor` prefixes map to the correct assistant tools.
- manual PDM tests only after user approval.

## Phase 5 - Salesforce Integration Decision

Current state:

- legacy `ClsSalesForce.cs` is deleted/archived.
- `Forms/FrmPane.cs` Salesforce region now shows "Salesforce integration archived."
- Salesforce UI still exists in the task pane and options.
- `AssistantProductCatalog.GetIntegrations()` exposes Salesforce as a planned read-only-first OAuth integration.
- `GET /assistant/integrations` exposes that status to the assistant panel/relay layer.
- `GET /assistant/document-catalog` includes a planned `salesforce-opportunity-brief` artifact.

Recommended path:

- build a new OAuth 2.0 integration rather than reviving the archived class.
- store tokens outside repo and never in config files.
- start with read-only objects:
  - Account
  - Contact
  - Opportunity
  - Quote/document links if available
- only add writeback after a separate approval gate.

Open decision:

- choose Salesforce Connected App details and scopes.
- choose approved token storage location, with Windows Credential Manager or another approved secret store preferred over repo/config files.
- choose the first read-only SOQL object allowlist.

## Phase 5b - Assistant Document Catalog

Current state:

- implemented document descriptors:
  - Drawing PDF
  - PDF Packet
  - STEP Export
  - DXF Export
  - Assistant Screenshot Artifact
- planned descriptor:
  - Salesforce Opportunity Brief

Recommended next documents:

- Opportunity/customer context brief from Salesforce/Epicor/PDM metadata.
- Manufacturing release checklist that references generated PDF, STEP, DXF, and packet status.
- Screenshot review report with annotations, accepted/rejected contacts, and trace id.
- Local vault search result summary for handoff into ChatGPT/Relay.

## Phase 6 - Relay / ChatGPT / MCP Verification

Objective: prove hosted relay, OAuth, and tool handoff work without risking CAD state.

Required checks:

- `GET /health`
- `GET /.well-known/oauth-protected-resource`
- expected unauthenticated `/mcp` challenge
- local `/chatgpt/session/create`
- handoff page render
- tunnel offline result
- tunnel connected result

Do not run:

- production deployment
- live OAuth registration against a real tenant without approval
- `/sw/*`, mutating `/pdm/*`, or `/lab/vault/reset` without explicit operator approval

## Phase 7 - Full Manual SolidWorks Validation

Only after build/test and relay checks pass:

1. Start `BlueBrick.Relay`.
2. Start SolidWorks manually.
3. Load BlueBrick Lab add-in.
4. Confirm assistant panel loads.
5. Confirm model dropdown loads profiles.
6. Create new session.
7. Capture screenshot.
8. Send mock-mode prompt with screenshot.
9. Switch to real mode only after key configuration is confirmed.
10. Test one read-only local vault search.
11. Test one read-only PDM search.
12. Test one Epicor read-only search.
13. Validate logs and trace ids.

Do not run during this smoke:

- CAD save/save-as/rebuild.
- PDM checkout/checkin/workflow-state changes.
- `/lab/vault/reset`.
- arbitrary `/sw/*` route calls.
- real cloud screenshot analysis unless user has explicitly approved the screenshot send and selected a vision-capable model.

## Phase 8 - Preview-Only SolidWorks Planner

Objective: let the assistant propose CAD/document changes without executing them.

Examples:

- add/update drawing note: preview text, target sheet/view, and risk.
- set custom property: preview old/new values.
- export PDF packet: preview output paths, overwrite risk, and included sheets.

Target files:

```text
Agent/SolidWorksMutationPlanner.cs
Agent/SolidWorksPreviewDiff.cs
Agent/SolidWorksApprovalGate.cs
Agent/PreviewSessionCoordinator.cs
Agent/PreviewActionPolicy.cs
```

Done criteria:

- all proposed actions are preview-only.
- no COM mutation occurs.
- user sees a human-readable diff.
- policy classifies risk and required approval.
- every preview has correlation ID and future receipt shape.

## Phase 9 - First Approved Mutation

Objective: execute exactly one low-risk approved mutation on a throwaway test file after P0-P4 pass.

Preferred first mutation: set/update a custom property on a test part or drawing.

Required controls:

- test file only.
- checkpoint or copy.
- old/new value diff.
- explicit approval dialog.
- main-thread COM execution.
- execution receipt.
- rollback instruction.

Still blocked:

- save-as.
- PDM checkout/checkin.
- rebuild.
- geometry edits.
- drawing BOM edits.
- batch operations.

## Phase 10 - Packaged React Assistant Panel

Objective: replace inline HTML with a maintainable local WebView2 app only after route/security/cancellation gates are stable.

Recommended stack:

```text
assistant-ui/
  React + TypeScript + Vite
  TanStack Query
  @ai-sdk/react useChat if local transport exists
  AI Elements for conversation/tool-call UI
  Zod schemas or generated route contracts
  Playwright UI tests where feasible
```

Done criteria:

- WebView2 loads local packaged app.
- route contracts are versioned.
- auth token is not DOM-visible.
- UI tests cover transcript, model selector, screenshots, disabled integrations, cancellation, local vault search, and catalog cards.

## Phase 11 - SOLIDWORKS 2026 Compatibility Track

Objective: forward-support SOLIDWORKS 2026 without breaking the 2024 SP5 target.

Required evidence:

- official SOLIDWORKS 2024 SP05 API docs attached/linked.
- official SOLIDWORKS 2026 API docs attached/linked.
- PDM Professional API docs attached/linked.
- installed interop assembly versions recorded.
- startup version detector implemented.
- 2024 regression tests pass.
- 2026 contract tests pass before declaring compatibility.

## Expanded Test Matrix

### Static/build

```powershell
where dotnet
dotnet --info
where msbuild
where nuget
dotnet restore BlueBrick.sln
dotnet build BlueBrick.csproj -c Lab -v minimal
dotnet test BlueBrick.UI.Tests\BlueBrick.UI.Tests.csproj -c Debug --filter "FullyQualifiedName~LabWorkspaceTests"
dotnet test BlueBrick.Relay.Tests\BlueBrick.Relay.Tests.csproj -c Debug
```

### Bridge/security

| Test | Expected |
|---|---|
| missing `X-Agent-Auth` | 401/403 |
| wrong `X-Agent-Auth` | 401/403 |
| oversized body | 413 or denied |
| unknown route | 404 |
| unknown tool | denied |
| disabled PDM/Epicor | disabled status, no execution |
| mutation without approval | denied |
| token in DOM scan | not found |

### Live SolidWorks smoke

| Step | Action | Expected evidence |
|---|---|---|
| 1 | Start relay | `/health` OK |
| 2 | Start SolidWorks | no crash |
| 3 | Load BlueBrick Lab add-in | task pane visible |
| 4 | Open assistant | model dropdown visible |
| 5 | Send mock chat #1 | response card |
| 6 | Send mock chat #2 | no cancellation error |
| 7 | Cancel request then chat | next chat succeeds |
| 8 | Capture screenshot | artifact card rendered |
| 9 | Search local vault | structured cards |
| 10 | Try disabled PDM/Epicor | clear disabled status |
| 11 | Close SolidWorks | no orphan bridge/log errors |

## Documentation To Add Or Update

```text
docs/BLUEBRICK_ARCHITECTURE.md
docs/BLUEBRICK_SECURITY.md
docs/BLUEBRICK_ASSISTANT_ROUTE_MANIFEST.md - created
docs/BLUEBRICK_AGENT_TOOL_REGISTRY.md
docs/BLUEBRICK_SOLIDWORKS_API_GOVERNANCE.md
docs/BLUEBRICK_PDM_INTEGRATION_PLAN.md
docs/BLUEBRICK_AIONUI_MODEL_AUTHORITY_ADR.md - created
docs/BLUEBRICK_SOURCE_OF_TRUTH_PROOF.md - created
docs/BLUEBRICK_TEST_GATE.md
docs/BLUEBRICK_DECISIONS.md
docs/BLUEBRICK_MANUAL_SOLIDWORKS_SMOKE.md
```

## Open Research Queue

Official docs to attach or verify:

- SOLIDWORKS 2024 SP05 API Help.
- SOLIDWORKS 2026 API Help.
- SOLIDWORKS 2025/2026 What’s New API notes.
- PDM Professional API Help for 2024/2026.
- Document Manager API docs and license requirements.

Open-source references to inspect:

- `xarial/xcad`.
- `xarial/xcad-examples`.
- maintained SOLIDWORKS PDM samples using `IEdmVault5`, `IEdmSearch`, and metadata reads.
- safe local broker/MCP examples that do not expose arbitrary tool execution.

## Minimum Stable Acceptance Suite

- `BlueBrick` Lab build succeeds.
- UI test project builds.
- focused assistant/model tests pass.
- relay tests pass.
- browser-safe relay checks pass.
- manual SolidWorks assistant smoke test passes.
- no secret printed or committed.
- no CAD/PDM mutating endpoint is called without approval.

## Known Gaps Remaining

- No React/Vercel AI Elements panel exists yet.
- No local Node/TypeScript assistant frontend package exists yet.
- Screenshot annotation and contact extraction contracts exist, but no model-driven extraction or review UI exists yet.
- Salesforce integration is intentionally not implemented.
- Epicor and PDM search are legacy/static-code paths and now have disabled assistant-facing catalog entries pending safe wrappers.
- Full manual SolidWorks smoke test has not been rerun after this plan update.
- Source-of-truth proof exists, but the gate remains partial until current assistant/relay/config/docs/test WIP is intentionally baselined or documented as local-only.
- Route manifest exists and now needs enforcement tests.
- AionUI model authority ADR exists; local profile contract and vision gating are partially implemented, but broker synchronization is missing.
- Cancellation regression tests have not yet been added.
- Cancellation ownership and error-classifier regression coverage exists, but full second-chat and live bridge/provider failure tests are still missing.
- WebView2 static shell-token and navigation guard tests exist, but live DOM/navigation validation is still missing.
- Initial tool policy, authorization contract, JSONL audit persistence, execution receipts, `/assistant/tool-audit`, and basic UI receipt surfacing exist; live route-level validation and approval workflow UI are still missing.
- Read-only SolidWorks snapshot adapter has not yet been implemented.
