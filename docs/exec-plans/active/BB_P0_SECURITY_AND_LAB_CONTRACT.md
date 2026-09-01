# BlueBrick P0 Security and Lab Contract

Run: `20260901-163846`

## Security flow

1. WebView2 receives a custom identity-specific user-data folder.
2. Navigation is allowlisted. Only `https://bluebrick-ui.invalid/index.html` (or its root document) is privileged.
3. The host injects a per-host nonce before document scripts run. React includes that nonce in bridge object messages.
4. `AssistantPanel` checks WebView2 `Source`, the current WebView URI, and the nonce before dispatch. Navigation and fallback reset trust.
5. The localhost bridge requires the in-memory `X-Agent-Auth` token and applies the protected-route ingress gate.
6. `/assistant/capabilities` is server-owned metadata. `/assistant/tool` ignores client JSON authorization claims, resolves a server catalog descriptor, applies capability policy, and emits a receipt.
7. CAD/PDM routes and protected local side effects pass through an independent execution-boundary policy. No trusted native approval lifecycle is exposed in this slice.
8. Relay invocation requires a connected token-configured channel, uses a server actor identity, and is re-evaluated by preview policy and the execution boundary.
9. PDM assistant search is disabled by default. If explicitly enabled by server configuration, it requires an already authenticated PDM session; it never calls `LoginAuto`.

## Lab isolation contract

Lab deployment targets are fixed to `C:\BlueBrickLab` and `HKCU\SOFTWARE\ViraInsight\BlueBrickLab\Settings`, with Lab port `17179`, Lab telemetry, and Lab WebView data. Production remains `C:\BlueBrick`, its distinct registry identity, port `17178`, telemetry, and WebView data.

The Lab orchestrator only accepts `Target=Lab`. It backs up Lab DLL/config and Lab registry state before copying or registration. Its rollback manifest must identify the exact Lab paths, `productionMutation=false`, and a backup root under `C:\BlueBrickLab`; rollback imports only recognized Lab registry backup filenames. Production paths are validation data, never rollback targets.

The current Lab deployment/register gate passed its mandatory checks on `2026-09-01` and left the Production DLL untouched. The follow-on host gate is not accepted: no fresh Lab telemetry or `127.0.0.1:17179` listener was observed after launch. Two responding SOLIDWORKS processes alone do not prove that the Lab add-in was discovered or loaded. One non-fatal COM probe cleanup warning remains recorded by the validator.

## Truth ceiling

Static/build evidence can establish source contracts, policy behavior, package hashes, script syntax, and rollback target isolation. It cannot establish a live WebView2 message, SolidWorks COM connection, provider response, relay peer, PDM session, or successful Lab registration until those are observed in the Lab runtime and recorded with trace/time/process/path evidence.

Current deliberate gaps: trusted native confirmation for mutating actions, PDM live read-only acceptance, external review writes, and Production promotion.
