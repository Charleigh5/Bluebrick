# BlueBrick Source-Of-Truth Proof

Last captured: 2026-05-28

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
| Branch and commit identified | PASS | Branch is `main`; HEAD is `1904fc9`. |
| WIP state captured | PASS | Dirty tracked and untracked state captured above. |
| Assistant/relay/docs/tests tracked or intentionally baselined | PARTIAL | These are currently untracked WIP. They can be valid working state, but not a durable baseline yet. |
| Secrets protected | PASS | `.env` was observed but not opened. |
| No SolidWorks/PDM/lab mutation endpoints exercised | PASS | This was a read-only evidence capture. |

## Required Next Action

Before claiming a durable baseline, choose one of these:

1. Add the intended assistant/relay/config/docs/test WIP to a reviewed Git baseline.
2. Keep it untracked but write an explicit local-WIP handoff listing the required files and why they are intentionally untracked.
3. Move experimental or generated files out of the project root before baseline.

Until one of those happens, implementation claims should say: "verified in current local WIP, not yet baselined in Git."
