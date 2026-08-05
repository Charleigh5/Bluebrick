# BlueBrick Architecture

## Overview

BlueBrick is a SolidWorks task-pane copilot that provides AI-assisted workflows inside the CAD environment. It is implemented as a .NET Framework 4.8 COM add-in with a WebView2-based chat UI, a local HTTP bridge, and an optional ASP.NET Core 8.0 relay server for remote/ChatGPT integration.

## Runtime Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│  SolidWorks Process (.NET Framework 4.8)                           │
│                                                                     │
│  ┌──────────────┐    ┌───────────────────┐    ┌─────────────────┐  │
│  │ swaddin.cs   │───▶│ AgentHttpServer   │◀──▶│ ISldWorks COM   │  │
│  │ (COM entry)  │    │ (localhost:35001)  │    │ API             │  │
│  └──────────────┘    └────────┬──────────┘    └─────────────────┘  │
│                               │                                     │
│  ┌──────────────────┐        │    ┌─────────────────────────────┐  │
│  │ AssistantPanel   │◀───────┘    │ Agent/ (39 service files)   │  │
│  │ (WebView2 chat)  │             │ - OpenAiAssistantService    │  │
│  │ or               │             │ - AssistantToolService      │  │
│  │ FrmAssistantWindow│            │ - AssistantToolPolicy       │  │
│  └──────────────────┘             │ - AgentPanelClient          │  │
│                                    │ - VaultWorkspaceFactory     │  │
│                                    │ - RelayTunnelClient         │  │
│                                    └─────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────┘
        │ localhost HTTP          │ WebSocket (optional)
        ▼                        ▼
┌──────────────────┐    ┌─────────────────────────────────────────────┐
│  Model Provider   │    │  BlueBrick.Relay (ASP.NET Core 8.0)       │
│  (NVIDIA / OpenAI │    │  - JWT authentication                      │
│   / AionUI)       │    │  - /mcp tool invocation endpoint           │
│                   │    │  - /ws/agent WebSocket tunnel               │
│                   │    │  - Device registration + heartbeat          │
│                   │    │  - SQLite repository for tunnel state       │
└──────────────────┘    └─────────────────────────────────────────────┘
```

## Key Components

### COM Add-In (`swaddin.cs` / `ClsMain.cs`)

- Registers as a SolidWorks add-in via `ISwAddin` interface
- `AppIdentity.cs` provides compile-time switching between production (`BLUEBRICK`) and lab (`BLUEBRICK_LAB`) builds
- Production uses GUID `{a0a6a8e5-...}`, lab uses `{b1b7c9f6-...}`
- Creates the `AgentHttpServer` and the assistant UI panel

### AssistantPanel (`AssistantPanel.cs`)

- `UserControl` hosted in the SolidWorks task pane
- WebView2 control renders the chat shell HTML
- Security-hardened: DevTools off, host objects blocked, web messages disabled, navigation allowlist, popup blocker
- Communicates with `AgentHttpServer` via `AgentPanelClient` (localhost HTTP)

### FrmAssistantWindow (`FrmAssistantWindow.cs`)

- Standalone `Form` for lab/preview mode
- Same WebView2 chat shell as AssistantPanel
- Now hardened with same security measures as AssistantPanel (P0.5 fix)

### AgentHttpServer (`Agent/AgentHttpServer.cs`)

- Local HTTP listener on `127.0.0.1:{BridgePort}` (production: 35001, lab: 17179)
- 39+ routes covering assistant, vault, PDM, generation, telemetry, and relay
- JWT auth via `X-Agent-Auth` header (auto-generated on startup)
- Delegates to `AssistantToolService`, `OpenAiAssistantService`, vault workspaces, and relay tunnel

### AssistantToolService (`Agent/AssistantToolService.cs`)

- Tool dispatch: `search_local_vault`, `search_pdm`, `search_epicor`, `capture_screenshot`
- Policy gate via `AssistantToolPolicy` — blocks CAD, PDM, and destructive routes
- Every invocation produces an `AssistantToolExecutionReceipt` recorded in `AssistantToolAuditLog`

### OpenAiAssistantService (`Agent/OpenAiAssistantService.cs`)

- Model runtime: streams chat completions from NVIDIA, OpenAI, or AionUI broker
- Screenshot capture and analysis
- Model profile support (vision, streaming, tools, JSON mode capabilities)

### RelayTunnelClient (`Agent/RelayTunnelClient.cs`)

- WebSocket client connecting to the relay server
- Registers device, receives tool invocations, sends results
- Handoff URL generation for ChatGPT integration

### BlueBrick.Relay (`BlueBrick.Relay/`)

- ASP.NET Core 8.0 web server
- JWT authentication on `/mcp` endpoint
- WebSocket tunnel at `/ws/agent` for device-to-relay communication
- SQLite persistence for device registry
- MCP tool catalog and routing

### Vault Layer (`Vault/`)

- `LocalVaultWorkspace` — file-based indexing of generated artifacts
- `PdmVaultWorkspace` — read-only PDM search via EPDM interop
- `VaultWorkspaceFactory` — selects workspace based on configuration

## Data Flow

### Chat Message (streaming)

1. User types in WebView2 → `bbSend()` JS → `AgentPanelClient.PostStreamingAsync("/assistant/stream", ...)`
2. `AgentHttpServer` → `OpenAiAssistantService.SendStreamingAsync()` → model provider HTTPS
3. SSE chunks flow back through the bridge → `AgentPanelClient` line buffer → WebView2 `bbAppendChunk()`
4. Final response assembled in `fullResponse` StringBuilder

### Tool Invocation

1. Model returns `tool_call` → `AgentHttpServer` parses tool name + arguments
2. `AssistantToolService.ExecuteAsync()` → policy gate → catalog lookup → dispatch
3. Result + receipt returned to caller, receipt persisted to JSONL audit log

### ChatGPT Handoff (via relay)

1. ChatGPT sends tool call → relay `/mcp` → JWT validated
2. Relay looks up device tunnel → sends invocation via WebSocket
3. `RelayTunnelClient` receives → `HandleRelayInvocationAsync()` → `AssistantToolService.ExecuteAsync()`
4. Result sent back through WebSocket → relay → ChatGPT

## Configuration

- `config/appsettings.json` — production settings (bridge port 35001)
- `config/appsettings.lab.json` — lab settings (bridge port 17179, relay enabled)
- Model profiles define provider, capabilities, and key environment variables
- `AppIdentity.cs` compile-time constants control GUIDs, ports, and registry paths

## Build Modes

| Mode | Constant | Bridge Port | Add-in GUID | Relay |
|------|----------|-------------|-------------|-------|
| Production | (default) | 35001 | `{a0a6a8e5-...}` | Disabled by default |
| Lab | `LAB_BUILD` | 17179 | `{b1b7c9f6-...}` | Enabled by default |

## Testing

- `BlueBrick.UI.Tests` — 45+ test methods covering vault, tools, policy, security, and error classification
- `BlueBrick.Relay.Tests` — 3 test methods for relay contracts
- Tests use MSTest framework, run via Visual Studio test runner
