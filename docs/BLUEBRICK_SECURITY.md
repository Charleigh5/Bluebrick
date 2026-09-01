# BlueBrick Security

## Threat Model

BlueBrick operates as an in-process SolidWorks COM add-in that exposes a localhost HTTP bridge and optional remote relay. The primary threats are:

1. **Local privilege escalation** — malicious code on the same machine hitting the bridge
2. **Model prompt injection** — adversarial content in documents or model responses triggering unintended tool calls
3. **Relay impersonation** — unauthorized devices connecting to the relay
4. **WebView2 content escape** — navigation or script injection in the chat UI

## Defense Layers

### Layer 1: WebView2 Hardening (P0.5)

Both `AssistantPanel` and `FrmAssistantWindow` apply the same security settings:

| Setting | Value | Purpose |
|---------|-------|---------|
| `AreDevToolsEnabled` | `false` | Prevents DevTools access to DOM/token inspection |
| `AreHostObjectsAllowed` | `false` | Blocks .NET host object injection into JS context |
| `IsWebMessageEnabled` | `true` | Required for the React bridge; every received message is checked against the current privileged document URI and a host-issued per-session nonce |
| `AreDefaultContextMenusEnabled` | `false` | Removes right-click inspect/debug menus |
| `IsScriptEnabled` | `true` | Required for chat UI functionality |

**Navigation allowlist** (`AssistantWebViewSecurity.IsNavigationAllowed`):
- `https://bluebrick-ui.invalid/index.html` — the only privileged React document; its source must remain the current document before a host message is accepted
- `about:blank` and `data:` URIs — allowed only for fallback/display navigation; they are never privileged message sources
- `https://chatgpt.com` / `https://chat.openai.com` — allowed for display/handoff navigation only; they are never privileged message sources
- All other absolute URIs — **blocked** (default-deny)

The React bridge adds the non-exported `__blueBrickDocumentNonce` to each object message. The host compares that nonce, the WebView2 `Source`, and the current privileged document URI before dispatching a message. A navigation resets trust. This is defense in depth; it does not replace server authentication or tool policy.

**Popup blocker**: `NewWindowRequested` handler sets `e.Handled = true` to prevent new window/tab opens.

**Isolated user data folder**: WebView2 uses an identity-specific folder under `%LOCALAPPDATA%\VIRA-Digital-Engineer\webview2` or `%LOCALAPPDATA%\VIRA-Digital-Engineer-Lab\webview2`. Production and Lab therefore do not share cookies, cache, or WebView profile state.

**Init guard**: `_initLock`, `_initialized`, and `_initFailed` flags prevent double-initialization and provide graceful failure if WebView2 is unavailable.

### Layer 2: Bridge Authentication (P0.6)

The `AgentHttpServer` on `localhost:{BridgePort}` requires:

- **JWT-style auth token** (`X-Agent-Auth` header) — auto-generated on startup, stored in memory
- Token is **never persisted to disk** and **never logged**
- Every request to the bridge must include the token or receives 401

**Token scanner**: `AssistantWebViewSecurity.ContainsSensitiveTokenText()` detects if token identifiers (`X-Agent-Auth`, `.agent_token`, `OPENAI_API_KEY`, `NVIDIA_API_KEY`, `AssistantApiKey`) appear in HTML content — would block rendering if detected.

### Layer 3: Tool Policy (P0.7)

`AssistantToolPolicy` enforces a default-deny policy on tool execution:

| Route Pattern | Verdict | Code |
|---------------|---------|------|
| `/sw/*` | **BLOCKED** | `blocked_cad_route` |
| `/pdm/search`, `/pdm/get_props` | **BLOCKED** | `blocked_native_pdm_route` |
| `/pdm/*` (other) | **BLOCKED** | `blocked_pdm_mutation_route` |
| `/lab/vault/reset` | **BLOCKED** | `blocked_destructive_lab_route` |
| Tool aliases containing `sw/`, `pdm/`, `lab/vault/reset` | **BLOCKED** | `blocked_route_alias` |
| Unknown non-GET routes from assistant tool source | **BLOCKED** | `unknown_mutation_route` |
| `/assistant/*` | **ALLOWED** | `assistant_route` |
| `/agent/telemetry/*`, `/agent/selfcheck` | **ALLOWED** | `read_only_agent_route` |
| Unprotected GET routes | **ALLOWED** | `unprotected_route` |

**Every tool invocation** produces an `AssistantToolExecutionReceipt` with:
- Receipt ID, trace ID, tool name
- Policy decision (allowed/denied), policy code
- Risk level, result status
- Input summary (query length, parameter presence — no raw query text)
- Timestamp, authorization status

Receipts are persisted to JSONL files via `AssistantToolAuditLog` and queryable via `/assistant/tool/audit`.

`/assistant/capabilities` exposes the server-owned capability catalog, including capability ID, environment, read-only/mutation flags, credential and filesystem access, approval policy, and execution boundary. Client JSON cannot deserialize `AssistantToolRequest.Authorization`; a direct in-process forged approval is rejected as well. A future approved mutating capability must be issued by a trusted native lifecycle and bound to request ID, arguments, session, environment, expiry, and one-time consumption. No such approval lifecycle is exposed by the current Lab UI.

### Layer 4: Relay Security

- **JWT authentication** on `/mcp` endpoint — validates registration token before accepting tool calls
- **Device registration** — devices must present a valid registration token
- **WebSocket tunnel** — only registered devices can connect to `/ws/agent`
- **Heartbeat** — stale tunnels are cleaned up based on configurable interval
- **Inbound execution gate** — an inbound relay invocation is accepted only on a connected, token-configured channel, then re-evaluated by preview policy and the native execution boundary; relay payloads cannot provide confirmation or choose the actor identity

### Layer 5: Model Provider Isolation

- API keys stored as **environment variables only** — never in config files
- `SecretRef: "runtime-only"` in model profiles indicates keys come from environment
- Each model profile declares capabilities (`SupportsVision`, `SupportsStreaming`, `SupportsTools`, `SupportsJsonMode`) — the UI honors these limits
- Models that don't support vision skip screenshot analysis automatically

## Screenshot Safety

- Screenshots captured via `AssistantImageTools.CaptureActiveWindowAsync()`
- Optional redaction applied before sending to model
- Screenshots stored locally in assistant history directory
- `RetentionPolicy` field controls automatic cleanup

## Error Classification

`AssistantErrorClassifier` maps exceptions to user-facing error categories:

| Category | Code | Meaning |
|----------|------|---------|
| `request_canceled` | User-initiated cancel | Normal flow |
| `request_timeout` | CancellationToken fired without user cancel | Network/provider issue |
| `bridge_unavailable` | HttpRequestException | Local bridge down |
| `auth_failed` | 401/403 response | Invalid credentials |
| `provider_error` | Model returned error JSON | Rate limit, quota, model error |
| `json_parse_error` | Response not valid JSON | Provider returned non-JSON |

## Configuration Security

- `appsettings.json` contains **no secrets** — only structural config and model profile metadata
- API keys come from **environment variables** (`NVIDIA_API_KEY`, `OPENAI_API_KEY`)
- Production relay is **disabled by default** (`Relay.Enabled: false`)
- Lab config enables relay for development but with empty credentials by default

## Current P0 Disposition and Gaps

- Body-size limit on bridge requests is enforced before route dispatch (`MaxRequestBodyBytes`)
- No TLS between bridge and relay (relay-to-provider uses HTTPS)
- No rate limiting on bridge endpoints
- Trusted native confirmation lifecycle for mutating CAD/local actions is not available; those actions remain denied or confirmation-blocked
- PDM assistant search is disabled by default and never calls `LoginAuto`; read-only use requires an already authenticated session plus explicit server-side permission, and native `/pdm/*` routes remain execution-boundary blocked
- No Content Security Policy headers on bridge responses

The remaining items are declared gaps, not promotion claims. Static evidence can prove the policy seams and Lab deployment isolation; it cannot prove WebView2, SolidWorks, provider, relay, or PDM runtime behavior.
