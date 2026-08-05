# BlueBrick Lab + Relay Local Validation Runbook

## Summary
Use a two-process local validation loop:

1. Run the hosted relay from Visual Studio as the only debuggable startup project.
2. Build `BlueBrick Lab` in `Lab|Any CPU`, load it through SOLIDWORKS, and let the add-in connect outward to the relay.

This repo uses the correct split already:
- `BlueBrick` is a COM add-in library loaded by SOLIDWORKS.
- `BlueBrick.Relay` is the executable web process that should be started directly.

Grounding:
- MCP Streamable HTTP expects one `/mcp` endpoint, client JSON-RPC over `POST`, and optional `GET` SSE support.
- MCP/OpenAI auth expects protected-resource metadata and `401` challenges for unauthenticated requests.
- Visual Studio multi-startup support is appropriate for the relay side, but the add-in still has to be loaded by SOLIDWORKS rather than launched directly.

## Exact Visual Studio Setup
### Solution and startup shape
Open [BlueBrick.sln](/C:/Users/cweir/Documents/GitHub/VIRA%20GITHUB/Bluebrick/BlueBrick.sln) and restore packages for:
- [BlueBrick.csproj](/C:/Users/cweir/Documents/GitHub/VIRA%20GITHUB/Bluebrick/BlueBrick.csproj)
- [BlueBrick.Relay/BlueBrick.Relay.csproj](/C:/Users/cweir/Documents/GitHub/VIRA%20GITHUB/Bluebrick/BlueBrick.Relay/BlueBrick.Relay.csproj)
- [BlueBrick.UI.Tests/BlueBrick.UI.Tests.csproj](/C:/Users/cweir/Documents/GitHub/VIRA%20GITHUB/Bluebrick/BlueBrick.UI.Tests/BlueBrick.UI.Tests.csproj)
- [BlueBrick.Relay.Tests/BlueBrick.Relay.Tests.csproj](/C:/Users/cweir/Documents/GitHub/VIRA%20GITHUB/Bluebrick/BlueBrick.Relay.Tests/BlueBrick.Relay.Tests.csproj)

### Build configurations
- `BlueBrick`: `Lab|Any CPU`
- `BlueBrick.UI.Tests`: `Debug|Any CPU`
- `BlueBrick.Relay`: `Debug|Any CPU`
- `BlueBrick.Relay.Tests`: `Debug|Any CPU`

### Startup projects
Configure startup projects like this:
- `BlueBrick.Relay` -> `Start`
- `BlueBrick` -> `None`
- `BlueBrick.UI.Tests` -> `None`
- `BlueBrick.Relay.Tests` -> `None`

Reason:
- `BlueBrick.Relay` is the only directly runnable executable in the repo.
- `BlueBrick` must be built, registered, and then loaded by SOLIDWORKS.

## Exact Config Values
### BlueBrick Lab config
Set these values in [config/appsettings.lab.json](/C:/Users/cweir/Documents/GitHub/VIRA%20GITHUB/Bluebrick/config/appsettings.lab.json):

```json
{
  "Agent": {
    "BridgePort": 17179,
    "OverlayColor": "#D9FF5A"
  },
  "Vault": {
    "Root": "C:\\Users\\cweir\\Documents\\BlueBrick Lab Vault",
    "SourceRoot": "C:\\Users\\cweir\\Documents\\BlueBrick Lab Vault\\source",
    "GeneratedRoot": "C:\\Users\\cweir\\Documents\\BlueBrick Lab Vault\\generated",
    "ThumbsRoot": "C:\\Users\\cweir\\Documents\\BlueBrick Lab Vault\\thumbs",
    "MetadataRoot": "C:\\Users\\cweir\\Documents\\BlueBrick Lab Vault\\db",
    "LogRoot": "C:\\Users\\cweir\\Documents\\BlueBrick Lab Vault\\logs",
    "SampleSeedRoot": "C:\\Users\\cweir\\Documents\\BlueBrick Lab Samples"
  },
  "Assistant": {
    "ApiBaseUrl": "https://api.openai.com/v1",
    "Model": "gpt-4.1-mini",
    "Mode": "real",
    "SystemPrompt": "You are the BlueBrick Lab assistant. Help users troubleshoot SolidWorks workflows, drawings, generated outputs, and the BlueBrick interface using text and screenshots. Be concise, practical, and grounded in what is visible.",
    "Detail": "low",
    "EnableUploads": true,
    "MaxImageDimension": 1600,
    "JpegQuality": 75,
    "ConnectionTestPrompt": "Reply with the word READY and one short sentence confirming the BlueBrick Lab assistant connection is working.",
    "RequireExplicitUploadConsent": true,
    "MaxHistory": 20
  },
  "Relay": {
    "Enabled": true,
    "BaseUrl": "https://localhost:7085",
    "ChatWorkspaceUrl": "https://chatgpt.com/",
    "DeviceId": "cweir-bluebrick-lab-01",
    "DeviceName": "BlueBrick Lab on CWEIR-WS",
    "RegistrationToken": "bluebrick-relay-dev-token",
    "HandoffPath": "chatgpt/handoff",
    "HeartbeatIntervalSeconds": 30
  }
}
```

### BlueBrick Lab environment variables
Set these before launching SOLIDWORKS:
- `OPENAI_API_KEY=<your real platform API key>`

If you do not want live assistant calls yet:
- set `"Mode": "mock"` in the lab config instead

### Relay config
Set these values in [BlueBrick.Relay/appsettings.json](/C:/Users/cweir/Documents/GitHub/VIRA%20GITHUB/Bluebrick/BlueBrick.Relay/appsettings.json):

```json
{
  "Relay": {
    "BaseUrl": "https://localhost:7085",
    "ChatWorkspaceUrl": "https://chatgpt.com/",
    "HandoffPath": "chatgpt/handoff",
    "ProtectedResourcePath": "/.well-known/oauth-protected-resource",
    "SqlitePath": "data/relay.db",
    "ToolTimeoutSeconds": 20,
    "HeartbeatStaleSeconds": 90,
    "AllowedScopes": [ "bluebrick.preview" ],
    "RegistrationToken": "bluebrick-relay-dev-token"
  },
  "OAuth": {
    "Authority": "https://YOUR-DEV-IDP.example.com",
    "Audience": "bluebrick-relay",
    "RequireHttpsMetadata": true
  }
}
```

### OAuth requirement for the manual loop
For the first local manual loop, use a real dev IdP or dev tenant that exposes:
- OpenID discovery
- JWKS
- authorization code flow
- PKCE `S256`
- dynamic client registration if you intend to wire actual ChatGPT linking later

Do not leave:
- `Authority = https://auth.example.com`
- `RegistrationToken = change-me`

## Exact Operator Run Order
### Stage 1: Build and start relay
1. Build the solution.
2. Start only `BlueBrick.Relay`.
3. Confirm it is listening on `https://localhost:7085`.

### Stage 2: Load BlueBrick Lab
1. Build `BlueBrick` using `Lab|Any CPU`.
2. Ensure the add-in is registered and visible in SOLIDWORKS.
3. Start SOLIDWORKS manually.
4. Load `BlueBrick Lab`.
5. Confirm the assistant window opens automatically.
6. Confirm the assistant status panel shows:
- `Mode: REAL` or `MOCK`
- local vault path
- relay URL `https://localhost:7085`
- relay state eventually changing to connected after the tunnel starts

### Stage 3: Create a preview session
1. In the assistant window, click `New Session`.
2. Then click `Open in ChatGPT`.
3. This should create a preview session locally and hit the relay handoff route.

### Stage 4: Manual HTTP validation before ChatGPT
Run the request file at [docs/BlueBrick.Relay.Validation.http](/C:/Users/cweir/Documents/GitHub/VIRA%20GITHUB/Bluebrick/docs/BlueBrick.Relay.Validation.http) in Visual Studio, Rider, or VS Code REST Client.

## WebSocket Tunnel Validation
This cannot be validated with plain HTTP alone. Use Postman WebSocket, `wscat`, or another WS client.

Connect to:

```text
wss://localhost:7085/ws/agent?deviceId=cweir-bluebrick-lab-01
```

Headers:

```text
X-Relay-Token: bluebrick-relay-dev-token
```

After connect, send:

```json
{
  "kind": "register",
  "correlationId": "",
  "deviceId": "cweir-bluebrick-lab-01",
  "payload": "{\"deviceId\":\"cweir-bluebrick-lab-01\",\"deviceName\":\"BlueBrick Lab on CWEIR-WS\",\"product\":\"BlueBrick Lab\",\"sessions\":[\"<SESSION_ID>\"]}"
}
```

Expected:
- socket remains open
- relay now has a live tunnel for that device
- subsequent routed MCP tool calls no longer return `offline`

## Manual Success Criteria By Stage
### Stage A: BlueBrick local readiness
Pass when:
- `BlueBrick Lab` loads in SOLIDWORKS
- assistant window opens
- `/assistant/status` succeeds
- `/chatgpt/session/create` succeeds

### Stage B: Relay readiness
Pass when:
- `/health` succeeds
- protected-resource metadata returns valid JSON
- register/heartbeat succeed with the configured relay token
- handoff URL creates or updates a route

### Stage C: Tunnel readiness
Pass when:
- websocket stays connected
- registration payload is accepted
- relay can map session -> device

### Stage D: Routed tool readiness
Pass when:
- with no tunnel: relay tool call returns structured `offline`
- with tunnel: relay tool call returns a real action result
- blocked tools return `denied`, not `500`

## Exact Core Manual Test Sequence
Run this exact order:
1. `GET /health`
2. `GET /.well-known/oauth-protected-resource`
3. `POST /devices/register`
4. start SOLIDWORKS + load `BlueBrick Lab`
5. `GET /assistant/status`
6. `POST /chatgpt/session/create`
7. `GET /chatgpt/handoff?...`
8. open websocket tunnel and send `register`
9. manually invoke an MCP-style `tools/call` only after OAuth is ready
10. verify one read tool and one safe write tool:
- read: `get_preview_status`
- write-safe: `capture_preview_screenshot` or `run_local_review`

## Failure Review Checklist
### If relay never connects
Check:
- `Relay.BaseUrl` matches exactly in both configs
- `RegistrationToken` matches exactly in both configs
- TLS certificate trust for `https://localhost:7085`
- BlueBrick Lab assistant status shows the same relay URL you expect

### If `/mcp` always returns `401`
That is correct until a valid OAuth token is attached.
Check:
- `Authority`
- `Audience`
- JWKS/discovery availability
- token `aud`
- required scope `bluebrick.preview`

### If tool calls return `offline`
Check:
- websocket is actually connected
- session route exists for the same `sessionId`
- `deviceId` in handoff matches `deviceId` used on the socket
- the local agent is still alive and has not restarted

### If BlueBrick local calls fail
Check:
- `%APPDATA%\VIRA\.agent_token`
- bridge port `17179`
- assistant window status panel
- local vault paths exist and are writable

## Core Tests To Run First
Run only these tests first:
- [BlueBrick.UI.Tests/LabWorkspaceTests.cs](/C:/Users/cweir/Documents/GitHub/VIRA%20GITHUB/Bluebrick/BlueBrick.UI.Tests/LabWorkspaceTests.cs)
- [BlueBrick.Relay.Tests/RelayCoreTests.cs](/C:/Users/cweir/Documents/GitHub/VIRA%20GITHUB/Bluebrick/BlueBrick.Relay.Tests/RelayCoreTests.cs)

Focus on:
- session creation
- handoff URL generation
- policy denial for disabled hosted write actions
- relay route persistence
- offline tunnel behavior
- conservative tool catalog exposure

Do not spend time yet on:
- full UI automation
- live ChatGPT OAuth linking
- production deployment
- broad regression sweeps

## Assumptions And Defaults
- `BlueBrick` is loaded by SOLIDWORKS, not started directly from Visual Studio.
- `BlueBrick.Relay` is the only startup project for local debug.
- local relay URL is `https://localhost:7085`
- local BlueBrick agent URL is `http://127.0.0.1:17179`
- `bluebrick.preview` is the initial required OAuth scope
- the first manual validation target is:
  - relay up
  - BlueBrick Lab connected
  - preview session created
  - handoff route working
  - one read path and one safe write path validated

Because the current shell environment is broken, this runbook is based on static repo inspection and official docs, not a completed local execution in this session.
