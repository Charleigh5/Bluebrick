# BB05-UI-SNAPSHOT-TRIGGER-20260927-R01 — React UI trigger for the read-only active-document snapshot

- Run ID: `BB05-UI-SNAPSHOT-TRIGGER-20260927-R01` (evidence folder)
- Build ID: `BB20-UI-SNAPSHOT-TRIGGER-20260927-R01` (`AssistantWeb/dist/index.html` meta `bluebrick-build-id`)
- Source commit at build: `14d871b` (HEAD, "Record STATE 2 CAD vertical-slice acceptance (R06)")
- Capability: `solidworks.get_active_document_snapshot` — read-only, `READ_ONLY_ANALYST`
- Result: **PASS for the browser/static UI contract. Live SOLIDWORKS-hosted UI click NOT_VERIFIED.**

## What changed

One new allowlisted browser→host message, one host handler, one React action. No new
callback, no new route, no new tool, no model call.

| File | Change |
| --- | --- |
| `AssistantWeb/src/bridge/blueBrickWebViewBridge.ts` | 11th browser→host message `captureActiveDocumentSnapshot` (was 10) + `PayloadFor` arm + helper |
| `AssistantPanel.cs` | one `else if` branch in `HandleWebViewMessageAsync` + `CaptureActiveDocumentSnapshotAsync()` calling the existing `AgentPanelClient.ExecuteToolAsync` and returning via the existing `bbAppendToolResult` |
| `AssistantWeb/src/App.tsx` | toolbar item `active-document-snapshot` (label `Snapshot`, `hostRequired`), `handleActiveDocumentSnapshot` with 30 s honest-timeout, `onAppendToolResult` derives the context, card rendered in the thread, reset clears it |
| `AssistantWeb/src/activeDocumentContext.ts` | accepts the real tool name and label, maps `empty`/`partial` statuses, reads `items[0].Title` and `metadata.runtime`, reads `mutation_count` |
| `AssistantWeb/src/ActiveDocumentContextCard.tsx` | one added `Runtime` metric |
| `AssistantWeb/scripts/narrow-smoke.mjs` | frozen browser→host contract widened 10 → 11 (mandatory; the gate would otherwise fail) |
| `AssistantWeb/scripts/test-active-document-context-card.mjs` | live-payload cases for `ok` / `empty` / `partial` plus the host-source contract |
| `AssistantWeb/scripts/verify-active-document-snapshot-button.mjs` | new browser click contract |
| `AssistantWeb/package.json` | `verify:active-document-snapshot` script |

Design constraint honoured: **no 18th `bb*` callback.** `bbAppendToolResult` is reused, so the
live identity readback still reports `bbCallbackCount: 17`.

## Verified in this session (2026-09-28)

| Check | Result |
| --- | --- |
| `npm run typecheck` | PASS (`tsc --noEmit`, no output) |
| `node scripts/test-active-document-context-card.mjs` | PASS — 10 cases incl. `live snapshot result`, `empty status`, `partial status` |
| `npm run verify:active-document-snapshot` | PASS — 5 checks, file-scheme only, no SOLIDWORKS, no listener, no network, no provider |
| `npm run verify:replay` | PASS |
| `npm run verify:ui-activation` | PASS |
| `npm run verify:transport` | PASS |
| `npm run test:vira-lab` | PASS |
| `npm run test:execution-board` | PASS |
| `node scripts/template-flow-verify.mjs` | PASS |
| `npm run verify:output-lab` | PASS (source↔`bin/Lab` dist parity) |

Browser-click evidence from `verify:active-document-snapshot`:

- exactly one posted message, `type: captureActiveDocumentSnapshot`, `documentNonce` attached;
- no `sendMessage` and no non-`file:` request, so no model and no egress;
- rendered card text contains `Part`, `33.5.0`, `0 mutation actions`, `Read-only context`;
- host-offline path surfaces `Active document snapshot unavailable: host is offline.`

## Package / machine state

- `AssistantWeb/dist` and `bin/Lab/AssistantWeb/dist` agree: `index.html 50AFB5C6…`,
  `assistant-web.js 252993F1…`, `assistant-index.css DC6EDCD8…` (unchanged).
- `bin/Lab/BlueBrick.Lab.dll` = `9287B2E6…` (contains the `AssistantPanel.cs` change).
- Deployed `C:\BlueBrickLab` is still `BB04-STATE2-READONLY-20260927-R05`
  (`2A90967F…`, deployed 2026-09-27 12:01Z) because the R01 live launch failed and the
  controller auto-rolled-back. Nothing from this run is deployed.

## NOT verified / blocked

1. **Live SOLIDWORKS-hosted click.** The R01 live launch exited before its main window and rolled
   back. A retry is still outstanding, and as of this record the machine is not free: a
   **user-owned SOLIDWORKS PID 29816** is running `80233885.SLDASM` (started 2026-09-28 09:17),
   port `17178` is LISTEN, and `C:\BlueBrickLab\owned-solidworks-process.json` does not exist.
   That process is user-owned; it was not attached to, reloaded, or terminated. The launch guard
   refusing here is correct behaviour, not a product failure.
2. **Host-side receipt round-trip from the real WebView.** The browser test stubs the host
   callback with the recorded R06 payload. The real `WEBMESSAGE_PARSED` /
   `bbAppendToolResult` round-trip inside SOLIDWORKS is unproven for this run.
3. **Packet-demo `Shift+Enter` and normal-mode host stub** remain NOT_VERIFIED for the separate
   `packet-demo-packaged-lab` sprint-01 contract (independent reevaluation-02: CONTRACT BLOCK /
   RUBRIC BLOCK). No acceptance transfers from the earlier reviewer's ACCEPT document.

## Safety boundary observed

No CAD, PDM, Epicor, database, registry, Production, or provider mutation. No commit, no push.
The SOLIDWORKS Licensing Service was left in its original `Stopped`/Manual state.
