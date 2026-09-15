# Lab discovery diagnosis — 2026-09-08

Status: BLOCKED_ENVIRONMENT. Diagnostic defect repaired; Lab loading and React interaction remain unverified.

## Evidence

- Source: canonical Bluebrick checkout, branch bluebrick-assistant-slice1-foundation. Existing dirty work preserved.
- Prior continuation: ../BB20-SOL-20260903-205554-R04/AGENT_CURRENT_STATE.md.
- September 3 Lab process no longer exists. One current SLDWORKS process (43360, started September 7) is responsive; read-only module enumeration found no BlueBrick. Its ownership/document state is unknown; it was not attached to or modified.
- Lab HKCU COM binding resolves to C:\BlueBrickLab\BlueBrick.Lab.dll. HKCU discovery exists; startup default is 1. Lab HKLM discovery key is absent. No listener found on 17179.
- Lab DLL SHA256 remains 97B8EDEF6F6C7E09871CC573B9C75866671AA8F4DCD02C059CD5B21AB2EEA3EE; Lab config remains 1ECCB27D42CE641D1DFDBA2A4B923A42F37DBEC2AA8806B79A210C7A1AF434A4, matching prior receipt.
- Registration script documents version-dependent HKCU discovery. Launcher always selects PerUser. Missing HKLM discovery is a plausible loading cause, not confirmed root cause.

## Narrow change and verification

tools/validate-lab-live.ps1 now emits WARN for HKCU-only discovery, preventing ALL CHECKS GREEN from masking unverified discovery.

Executed once using Windows PowerShell with -LabDllPath C:\BlueBrickLab\BlueBrick.Lab.dll -SkipComProbe: assembly/type resolution passed, binding and startup checks passed, discovery warning and warning summary appeared. Exit 0 retains existing nonfatal warning behavior. PowerShell parser and git diff --check passed. No builds or broad suites repeated.

## Concrete remaining action

Preview verified with tools/register-lab-addin.ps1 -Mode All -LabDllPath C:\BlueBrickLab\BlueBrick.Lab.dll -BackupRoot C:\BlueBrickLab\backups\BB-LAB-20260908-discovery\registry -WhatIf. It would write Lab-only HKCR COM binding, HKLM discovery, and HKCU startup keys. No registration executed.

Before live execution, obtain explicit approval for this Lab COM registration and an isolated Lab smoke. Applicable skill C:\Users\cweir\.codex\skills\bluebrick-repo-editing-gate\SKILL.md says: “Do not launch SOLIDWORKS, register/unregister COM add-ins ... without explicit approval.” The delegated authorization allowed narrow reversible repair only after isolation and authorization were verified; current SOLIDWORKS ownership is not established. Native computer surfaces are unavailable in this executor, so no UI interaction was possible.

After authorized registration, use an owned isolated Lab session to verify module load, Lab listener, then one React interaction. Stop if isolation cannot be maintained. Do not infer acceptance from registration or static checks.

## Instruction impact

## Authorized continuation

User subsequently explicitly approved Lab registration/launch and closing/reloading SOLIDWORKS for UI testing. No additional permission for those actions is needed. The executor is not elevated. A single Windows RunAs request for the previewed All-mode registration was dispatched and remains pending in terminal session 5502; do not duplicate it. HKLM discovery remained absent at the last check, so registration is not yet verified.

Native computer APIs are disabled in the supplied CUA capability. Read-only COM document-save inspection was attempted after restart authorization: late-bound document enumeration failed with a COM type error, and the typed interop retry failed conversion. Any zero counts printed after the first exception are invalid and were rejected. Current document/save state remains UNKNOWN; no close, kill, save, or CAD mutation occurred. Next dependency is Windows elevation response and a safe saved/closed existing session before an owned Lab launch.

Capability check: installed computer-use/26.901.51231/skills/computer-use/SKILL.md and docs/guidance.md require node_repl with @oai/sky. No node_repl tool is exposed, and supplied mcp__cua_repl explicitly disables native computer APIs. No native SOLIDWORKS connector was found. No verified configuration switch or time/quota adjustment can be prescribed from this evidence. A desktop task exposing that runtime and returning SOLIDWORKS from app/window listing is the next capability acceptance test.

No governance or policy change. Only existing validator severity corrected. No production writes, CAD mutation, credentials, commits, staging, deployment, memory updates, or external sync. Authorized read-only COM inspection attempts failed as recorded above. This local receipt is the linked Chronicle for the bounded change.
