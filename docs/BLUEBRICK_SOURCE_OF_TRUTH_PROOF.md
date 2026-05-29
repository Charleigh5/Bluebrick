# BlueBrick Source-Of-Truth Proof

Last captured: 2026-05-29 (updated after P0 baseline commit)

## Purpose

This document captures the P0 source-of-truth evidence required before expanding the BlueBrick assistant implementation. It proves which workspace Codex inspected and records the current repository/WIP state without reading secrets or exercising SolidWorks, PDM, lab reset, or local file mutation endpoints.

## Workspace Evidence

| Check | Result | Evidence |
|---|---|---|
| Current directory | `C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick` | `Get-Location` |
| Git branch | `main` | `git log --oneline --decorate -n 5` showed `1904fc9 (HEAD -> main) Add files via upload` |
| Recent commits inspected | `1904fc9`, `581f0a4`, `7fec6c0`, `fb6dd88` | `git log --oneline --decorate -n 5` |
| Active WIP state | Dirty, with tracked modifications and many untracked assistant/relay/test/config/docs files | `git status --short` |

## Tracked Changes Observed

The checkout contains tracked modifications in the existing BlueBrick add-in and UI files:

```text
M App.config
M BlueBrick.csproj
M BlueBrick.sln
M ClsLucky.cs
D ClsSalesForce.cs
M ClsSettings.cs
M CollapsePanel.cs
M DataFiles/settings.xml
M DocGenerator/ClsGenerators.cs
M Forms/FrmOpts.Designer.cs
M Forms/FrmOpts.cs
M Forms/FrmPane.Designer.cs
M Forms/FrmPane.cs
M Forms/FrmSfAuth.cs
M Properties/AssemblyInfo.cs
M Resource1.Designer.cs
M Resource1.resx
M swaddin.cs
```

## Untracked WIP Observed

The assistant/relay/test/config/docs implementation appears to be local WIP and is not yet tracked in Git:

```text
Agent/
AssistantPanel.cs
AssistantPanel.Designer.cs
BlueBrick.Relay/
BlueBrick.Relay.Tests/
BlueBrick.UI.Tests/
config/
docs/
Vault/
Resources/icon_Assistant*.png
Resources/icon_*.jpg
Resources/img-1779417852167.jpg
Directory.Build.props
AppIdentity.cs
FrmAgentWindow.cs
FrmAssistantWindow.cs
Forms/FrmAgent.cs
register_addin.bat
```

An untracked `.env` file exists. It was not opened or printed.

## `git ls-files` Result

`git ls-files Agent BlueBrick.Relay config docs BlueBrick.UI.Tests BlueBrick.Relay.Tests Forms AssistantPanel.cs AssistantPanel.Designer.cs` only returned tracked `Forms/*` files. This means the current assistant, relay, config, docs, and test-project work must be treated as untracked local WIP until explicitly added or otherwise baselined.

## Gate Status

| Gate Item | Status | Notes |
|---|---|---|
| Active workspace identified | PASS | The inspected path is `C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick`. |
| Branch and commit identified | PASS | Branch is `main`; HEAD is `ee99e55` (P0 baseline). |
| WIP state captured | PASS | All WIP now tracked and committed. |
| Assistant/relay/docs/tests tracked or intentionally baselined | PASS | Commit `ee99e55` adds 150 files including all Agent/, Vault/, Relay, tests, config, and docs. |
| Secrets protected | PASS | `.env` excluded via `.gitignore`; no secrets in committed files. |
| No SolidWorks/PDM/lab mutation endpoints exercised | PASS | This was a read-only evidence capture. |

## Baseline Commit

**Commit**: `ee99e55` on `main`
**Date**: 2026-05-29
**Summary**: P0 gate fixes and assistant WIP baseline (150 files, 22,400 insertions)

### What the baseline includes:

- All 39 Agent/ service files
- AssistantPanel.cs + FrmAssistantWindow.cs (with bug fixes and security hardening)
- BlueBrick.Relay/ (ASP.NET Core 8.0 relay server)
- BlueBrick.Relay.Tests/ + BlueBrick.UI.Tests/ (45+ test methods)
- Vault/ (local + PDM workspace implementations)
- config/appsettings.json + appsettings.lab.json (with P0.4 model profile fields)
- docs/ (architecture, security, ADR, route manifest, specs, plan, source-of-truth proof)
- Resources/ (assistant UI icons)
- .gitignore (excludes bin/, obj/, .env, temp files)

### Bug fixes in baseline:

| Bug | Fix | Files |
|-----|-----|-------|
| 1: PostStreamingAsync hangs | 90s read timeout via Task.WhenAny | AgentPanelClient.cs |
| 2: bbGetTranscript double-encode | Remove JSON.stringify wrapper | AssistantPanel.cs, FrmAssistantWindow.cs |
| 3: SSE line splitting breaks across buffers | Line buffer accumulator | AgentPanelClient.cs |
| 4: message.Text extraction fails | Case-insensitive multi-fallback | AssistantPanel.cs, FrmAssistantWindow.cs |
| 5: Double-serialize anti-pattern | Remove SerializeObject on already-JSON | AssistantPanel.cs, FrmAssistantWindow.cs, RelayTunnelClient.cs |
| 6: LogErrorAsync race condition | Lock around File.AppendAllText | AssistantPanel.cs, FrmAssistantWindow.cs |

### Security hardening in baseline:

- FrmAssistantWindow: DevTools off, host objects blocked, web messages disabled, navigation allowlist, popup blocker, isolated user data, init guard
- RelayTunnelClient: Payload changed from string to JToken to prevent double-escaping
- AssistantToolService: capture_screenshot dispatch wired with graceful fallback
- Production config: P0.4 model profile fields + Relay section with safe defaults

### Body-size limit (P0.6):

- AgentHttpServer: MaxRequestBodyBytes = 1,048,576 (1 MB)
- Content-Length header checked before routing; returns 413 if exceeded
- ReadBody uses chunked read with size guard as defense-in-depth against absent/spoofed Content-Length
