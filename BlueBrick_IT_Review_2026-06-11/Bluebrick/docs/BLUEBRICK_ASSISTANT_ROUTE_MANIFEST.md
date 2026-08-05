# BlueBrick Assistant Route Manifest

Last updated: 2026-05-28

## Purpose

This manifest records the local agent bridge routes currently declared in `Agent/AgentHttpServer.cs`. It separates low-risk assistant/catalog endpoints from CAD, PDM, lab, relay, and job routes that need explicit policy, approval, tests, and execution receipts before assistant-driven use.

All routes require `X-Agent-Auth` at the bridge layer. `/sw/*` and `/pdm/*` also have an Origin guard for non-local origins. Authentication is necessary but not sufficient for mutation safety.

## Route Summary

| Route | Method | Risk | Current State | Assistant Use Policy | Required Test |
|---|---:|---:|---|---|---|
| `/agent/overlay/show` | POST | Medium | Implemented | Host UI only; not model-direct | auth + UI state test |
| `/agent/overlay/hide` | POST | Medium | Implemented | Host UI only; not model-direct | auth + UI state test |
| `/agent/telemetry/summary` | GET | Low | Implemented | Read-only allowed | auth + schema test |
| `/agent/telemetry/events` | GET | Low/Privacy | Implemented | Read-only, redact before model context | auth + limit bounds test |
| `/agent/telemetry/trace` | GET | Low/Privacy | Implemented | Read-only, trace ID required | missing trace ID + schema test |
| `/agent/selfcheck` | GET | Low | Implemented | Read-only allowed | degraded/healthy schema test |
| `/agent/knowledge_base/hotset` | GET | Low/Privacy | Stub/implemented empty | Read-only allowed after privacy review | schema test |
| `/agent/knowledge_base/refresh` | POST | Medium | Implemented stub | Human/UI action only | auth + no-destructive-side-effect test |
| `/assistant/status` | GET | Low | Implemented | Allowed | status/auth test |
| `/assistant/models` | GET | Low | Implemented | Allowed | profile catalog test |
| `/assistant/tools` | GET | Low | Implemented | Allowed | tool catalog test |
| `/assistant/tool-audit` | GET | Low/Privacy | Implemented | Allowed after receipt redaction | persisted receipt schema/redaction test |
| `/assistant/integrations` | GET | Low | Implemented | Allowed | disabled-state catalog test |
| `/assistant/document-catalog` | GET | Low | Implemented | Allowed | descriptor schema test |
| `/assistant/session` | POST | Low | Implemented | Allowed | session schema test |
| `/assistant/test` | POST | Low | Implemented | Developer/test only | deterministic smoke test |
| `/assistant/mode` | POST | Medium | Implemented | UI-controlled only | valid/invalid mode test |
| `/assistant/model` | POST | Medium | Implemented | UI-controlled only | fallback + persistence test |
| `/assistant/tool` | POST | Medium/High | Implemented | Must pass tool policy before model use | deny/allow/receipt tests |
| `/assistant/message` | POST | Medium | Implemented | Allowed after cancellation/error hardening | second-chat cancellation regression test |
| `/assistant/screenshot` | POST | Medium/Privacy | Implemented | User-triggered only | screenshot metadata + no secret exposure test |
| `/assistant/screenshot/analyze` | POST | Medium/Privacy | Mock implemented | Deny if selected model lacks vision | vision/privacy gate test |
| `/assistant/history` | POST | Low/Privacy | Implemented | Read-only transcript access only | redaction/schema test |
| `/sw/open` | POST | Critical | Implemented | Blocked from assistant until preview + approval + receipt | deny from chat test |
| `/sw/create_drawing` | POST | Critical | Implemented | Blocked from assistant until preview + approval + receipt | deny from chat test |
| `/sw/add_views` | POST | Critical | Implemented | Blocked from assistant until preview + approval + receipt | deny from chat test |
| `/sw/apply_properties` | POST | Critical | Implemented | Blocked from assistant until preview + approval + receipt | deny from chat test |
| `/sw/generate_step` | POST | Critical | Implemented | Blocked from assistant until preview + approval + receipt | deny from chat test |
| `/sw/live-review/start` | POST | Critical | Implemented | Blocked from assistant until read-only snapshot + approval | deny from chat test |
| `/sw/generate-review` | POST | Critical | Implemented | Blocked from assistant until read-only snapshot + approval | deny from chat test |
| `/sw/live-review/checkpoint` | POST | Critical | Implemented | Blocked from assistant until review workflow is governed | deny from chat test |
| `/sw/live-review/decision` | POST | Critical | Implemented | Blocked from assistant until review workflow is governed | deny from chat test |
| `/sw/live-review/apply-action` | POST | Critical | Implemented | Blocked from assistant until mutation policy exists | deny from chat test |
| `/sw/live-review/finalize` | POST | Critical | Implemented | Blocked from assistant until receipt/audit exists | deny from chat test |
| `/sw/jobs/override` | POST | Critical | Implemented | Human-only override | deny from chat test |
| `/sw/jobs/{id}` | GET/POST override variant | High/Critical | Implemented dynamic route | Read-only GET allowed only after redaction; POST blocked | get schema + override deny test |
| `/pdm/check_out` | POST | Critical | Implemented | Blocked from assistant until explicit approval + receipt | deny from chat test |
| `/pdm/check_in` | POST | Critical | Implemented | Blocked from assistant until explicit approval + receipt | deny from chat test |
| `/pdm/search` | POST | Medium/Privacy | Implemented | Read-only search allowed only through safe wrapper | config-gated wrapper test |
| `/pdm/get_props` | POST | Medium/Privacy | Implemented | Read-only allowed only through safe wrapper | path validation + schema test |
| `/pdm/get_file` | POST | High/Privacy | Implemented | Block until file-scope policy exists | deny from chat test |
| `/qa/run` | POST | Medium | Implemented | Developer/test only | no production side-effect test |
| `/lab/vault/reindex` | POST | Medium | Implemented | User-triggered only | index receipt test |
| `/lab/vault/reset` | POST | High/Destructive | Implemented | Human-confirmed only; never model-direct | deny from chat + confirmation test |
| `/lab/vault/status` | POST | Low | Implemented | Allowed | schema test |
| `/chatgpt/session/create` | POST | Medium | Implemented | Relay/session flow only | auth + schema test |
| `/chatgpt/session/{id}/...` | Varies | Medium | Implemented dynamic route | Session-scoped only | path routing + auth test |
| `/relay/register` | POST | Medium | Implemented | Relay service only | auth + schema test |
| `/relay/heartbeat` | POST | Low | Implemented | Relay service only | heartbeat state test |
| `/relay/tool-result` | POST | Medium | Implemented | Relay service only | result validation test |

## Risk Rules

- Critical routes can change CAD files, PDM state, job decisions, or local working artifacts. They must not be callable from assistant text without an explicit tool policy decision, human approval, and durable receipt.
- Medium/Privacy routes can expose customer, CAD, screenshot, transcript, or file metadata. They need redaction and least-context rules before model context injection.
- Low routes can still leak operational details. They should keep `traceId` and status fields but avoid secrets, file contents, raw stack traces, and provider keys.

## Immediate Required Hardening

1. Extend `Agent/AssistantToolPolicy.cs` into full route-level policy metadata instead of maintaining safety only in prose.
2. Add broader integration tests proving assistant chat cannot invoke `/sw/*`, native `/pdm/*`, destructive `/pdm/*`, or `/lab/vault/reset`.
3. Continue expanding persisted execution receipt visibility for every approved medium/high/critical tool action in the UI.
4. Add redaction tests for telemetry, history, screenshots, and PDM metadata.
5. Keep read-only wrappers separate from bridge-native mutation routes.
6. Surface receipt IDs/status in the assistant UI for user trust and supportability.

## Current Enforcement Evidence

`Agent/AssistantToolPolicy.cs` now denies assistant-driven CAD, native PDM, destructive lab reset, unknown mutation routes, and route-shaped tool aliases before `AssistantToolService` catalog execution. `AssistantToolExecutionReceipt` metadata is attached to allowed and denied assistant tool results, and `AssistantToolAuditLog` records process-local plus redacted JSONL receipts when a vault log root is configured. `/assistant/tool-audit` exposes recent redacted receipts, and `AssistantPanel` now renders an Activity Receipts section plus receipt metadata on tool result cards. Unit coverage exists in `BlueBrick.UI.Tests/LabWorkspaceTests.cs` for the first policy, receipt, persisted-audit, and receipt-normalization slices.
