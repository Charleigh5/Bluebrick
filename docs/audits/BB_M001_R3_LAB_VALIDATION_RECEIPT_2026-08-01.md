# BB-M001 R3 LIVE LAB VALIDATION — Execution Receipt (BLOCKED)

- **Date**: 2026-08-01 02:33 (session start 2026-07-31)
- **Mode**: R3_LIVE_LAB_VALIDATION
- **Status**: `BLOCKED_WITH_EXACT_CAUSE`
- **Branch**: bluebrick-assistant-slice1-foundation (HEAD: ced14be)
- **Operator context**: agent shell, user `CWeir`, SessionId 1, interactive, **non-elevated**

## Authorized action attempted (not completed)
```
.\tools\register-lab-addin.ps1 -Confirm
```
4 elevation attempts, all failed to obtain admin rights (see Evidence).

## Exact cause (blocking)

1. **Lab registration requires elevation** — SOLIDWORKS discovers add-ins only via
   `HKLM\SOFTWARE\SolidWorks\Addins\{GUID}` (+ `HKCR\CLSID\{GUID}` COM binding).
   There is **no per-user (HKCU) equivalent** for add-in discovery; the pre-existing
   `HKCU\...\AddInsStartup\{251d6df2-...}=1` key is inert without the HKLM entry.
2. **`CWeir` is NOT in the Administrators group** — only the built-in `Administrator`
   account has admin rights (verified via `net localgroup Administrators`).
3. UAC policy: `EnableLUA=1`, `ConsentPromptBehaviorAdmin=5`, `PromptOnSecureDesktop=1`
   → the secure-desktop prompt requests **Administrator credentials** (not mere consent).
4. Every `Start-Process -Verb RunAs` attempt returned "The operation was canceled by the
   user" (credential prompt not satisfiable from this session; no admin password available;
   console idle ~7 min at 02:33, no interactive approver).
5. No alternative elevation path exists in-scope: no `psexec`, no stored-credential
   scheduled task, `runas` requires the same admin password.

This is an evidence-backed blocker per R2/R3 policy: stop, do not bypass, do not weaken.

## Evidence captured

| # | Check | Result |
|---|-------|--------|
| E1 | Production DLL hash (before) | `EE72B6532D2B8709A43A10E60092175CA9C6116591A14E8D7903F33C67F9581E` |
| E2 | Production HKCR CLSID (before) | `CodeBase=file:///C:/BlueBrick/BlueBrick.DLL`, Assembly=`BlueBrick 1.0.13.4`, Class=`BlueBrick.SwAddin`, RuntimeVersion=v4.0.30319, ThreadingModel=Both |
| E3 | Production HKLM Addins (before) | Title=BlueBrick, Description=ViraInsight SW add-in, Icon Path=C:\BlueBrick\BlueBrick.png, (Default)=0 |
| E4 | Production HKCU AddInsStartup (before) | `{C56E0AFF-...}` = 1 |
| E5 | Lab HKLM Addins before | **absent** (correct baseline) |
| E6 | Lab HKCU AddInsStartup before | `{251d6df2-...}` = 1 (leftover inert key, pre-existing) |
| E7 | SOLIDWORKS running | Yes — PID 41072, `SLDWORKS.exe` v33.5.0.0053 (**2025 SP5**), started 07-31 08:09 |
| E8 | Live SW module evidence | **No BlueBrick modules loaded** in running session (production NOT loaded; 754 modules enumerated, technique validated — .NET modules visible) |
| E9 | SW COM add-in enumeration | `SwApp.AddIns.Count = 0` — no add-ins loaded in current session (incl. production) |
| E10 | Event log (08:05–08:20, .NET/App errors) | no BlueBrick errors; no binding failures logged |
| E11 | SW journal `swxJRNL.swj` | no add-in load records |
| E12 | Elevation attempt 1 (`register-lab-addin.ps1 -Confirm`) | `Start-Process` NRE — prompt not approved |
| E13 | Elevation attempt 2 (cmd.exe RunAs) | 120 s hang — prompt unapproved |
| E14 | Elevation attempt 3 (marker test) | `Start-Process returned null` |
| E15 | Elevation attempt 4 (directed, 240 s) | `The operation was canceled by the user` — no output log |
| E16 | CWeir group membership | **Not in Administrators** (S-1-5-32-544 absent); `net localgroup Administrators` → only `Administrator` |
| E17 | HKCU\Software\Classes writable w/o elevation | Yes (tested) — but **insufficient**: SW add-in discovery is HKLM-only |
| E18 | FAIL-CLOSED verification | Lab HKLM Addins: **absent** · Lab HKCR CLSID: **absent** · Prod DLL hash: **unchanged** |
| E19 | `deploy-production.ps1 -Mode ReplaceProduction` dry run | **Zero writes** — backup dirs 2→2, plan printed, prod hash unchanged `EE72B653…` |
| E20 | Lab build artifact | `bin\Lab\BlueBrick.Lab.dll` v1.0.13.4 present (2,122,752 B) |
| E21 | Debug build artifact (deploy source) | `bin\Debug\BlueBrick.dll` v1.0.13.4 present (2,123,264 B), hash `B1DDEDF4…` |

## What did NOT happen (fail-closed confirmation)
- ❌ Lab add-in NOT registered (HKLM + HKCR absent after attempts)
- ❌ Production DLL NOT touched (hash identical before/after)
- ❌ Production registry NOT modified (no elevated process ever succeeded)
- ❌ No deployment, no external writes, no real credentials used
- ❌ `deploy-production.ps1 -Execute` NOT run (and remains unauthorized)

## Pre-existing finding surfaced by R3 (not caused by this session)
Production BlueBrick is registered but **not loaded** in the running SW 2025 SP5 session
(no modules, AddIns count 0, no errors logged). Possible causes to investigate:
HKLM Addins (Default)=0 (only HKCU=1 enables per-user), SW2024 interops (lib\) vs SW2025
installed, or add-in disabled in the Add-Ins manager. **This predates R3 and is out of
scope to fix**, but is documented for R4 planning — production "loads normally" was not
demonstrable from the live session either.

## Next authorized move (unblocks R3)
Run the registration **from an elevated shell with Administrator credentials** (the
built-in `Administrator` account or any admin), then re-run the R3 validation steps:
```
# Elevated PowerShell (run as Administrator):
cd C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick
.\tools\register-lab-addin.ps1            # registration only (no -Confirm needed when elevated)
# then re-run live validation: launch SW 2025 SP5, check Add-Ins manager for
# 'BlueBrick' + 'BlueBrick Lab', load Lab, verify bin\Lab\BlueBrick.Lab.dll module,
# run load/unload/restart/startup/security checks, then:
.\tools\register-lab-addin.ps1 -Unregister
# verify rollback + production unchanged
```
Alternative (if admin password is available): `Start-Process -Verb RunAs` will present
the credential prompt — approve it interactively.

## Rollback status
- Nothing to roll back — registration never succeeded; system state is byte-for-byte
  identical to pre-R3 baseline (E1–E4 unchanged, E18 confirms).

## Final promotion decision
**NOT READY** — `R3_LAB_VALIDATION_PARTIAL` would require a successful live load, which
cannot proceed until registration elevation is unblocked. Status returned:
`BLOCKED_WITH_EXACT_CAUSE`.

---

## Addendum 2026-08-04 — R3 re-attempt + Codex regression handling

### Codex regression (closed, no residual state)
- `Agent/AssistantWebViewSecurity.cs` (Codex, 8/3 00:29) added an http(s) allow rule for
  `127.0.0.1`/`localhost`, breaking the committed test
  `AssistantWebViewSecurity_Blocks_Unexpected_Navigation` (`LabWorkspaceTests.cs:1055`,
  asserts `http://127.0.0.1:17177/assistant/status` **blocked** — the agent's own HTTP
  server). **Reverted** (`git restore`), Debug+Lab rebuilt, suite back to
  **174 passed / 1 skipped / 0 failed**. Production DLL untouched.
- Lab DLL rebuilt without the rule: `bin\Lab\BlueBrick.Lab.dll` v1.0.13.4,
  SHA256 `9A0965BF8071F82CB5BE592AFDE82038B4447CA0D3817368C61FE59B6E69DE99`.

### Re-attempt: elevation (attempts 5–6, total 6/6 failed)
- Ran `.\tools\register-lab-addin.ps1 -Confirm` twice (operator present at console).
  Failure mode changed from "The operation was canceled by the user" to
  `Start-Process` terminating error `Object reference not set to an instance of an
  object` — consent UI cannot complete from this agent session.
- Session diagnostics: `SESSIONNAME=Console`, `SessionId=1`, `quser` shows user ACTIVE
  (interactive desktop exists), Explorer in Session 1. Root cause stands: `CWeir` has no
  admin token and no credential path from the agent harness; **the single manual step is
  still required**.
- Local Administrators group **changed since 8/1**: now contains `Administrator`, `NEW`,
  `VIRA\virad`, `virainsight`, `VIRA\Domain Admins` (CWeir still absent) — an admin
  credential is now plausibly available to the operator.

### New tool: post-registration validator (non-elevated, read-only)
- `tools/validate-lab-live.ps1` — verifies discovery key, activation key, per-user load,
  Lab DLL version/integrity, CodeBase match, assembly/type resolution, COM activation
  probe; exit 0 = all green. Parse-clean on PS 5.1 + 7.

### Current validator output (registration still absent)
```
[FAIL] Discovery key missing: HKLM\SOFTWARE\SolidWorks\Addins\{251d6df2-...}
[FAIL] Activation key missing: HKCR\CLSID\{251d6df2-...}\InprocServer32
[PASS] Per-user load enabled: HKCU\...\AddInsStartup\{251d6df2-...} = 1
[PASS] Lab DLL v1.0.13.4  SHA256=9A0965BF8071F82CB5BE592AFDE82038B4447CA0D3817368C61FE59B6E69DE99
[INFO] Production DLL untouched: SHA256=EE72B6532D2B8709A43A10E60092175CA9C6116591A14E8D7903F33C67F9581E
[PASS] Assembly loaded: BlueBrick.Lab v1.0.13.4; type BlueBrick.SwAddin resolved
[WARN] COM activation probe failed (non-fatal): 80040154 REGDB_E_CLASSNOTREG (expected — not registered)
[FAIL] 2 failure(s); registration incomplete
```

### Updated unblock path (single manual step — operator)
From an **elevated PowerShell** (right-click → Run as administrator, any admin account):
```
cd C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick
.\tools\register-lab-addin.ps1
```
Then, from any shell (elevation not needed):
```
.\tools\validate-lab-live.ps1        # expect [PASS] on all mandatory checks, COM probe PASS
```
Then launch SOLIDWORKS 2025 SP5, open Tools → Add-Ins, enable **BlueBrick Lab**, and
confirm `BlueBrick.Lab.dll` loads; run load/unload/restart/startup checks, then
`.\tools\register-lab-addin.ps1 -Unregister` and re-run the validator to confirm rollback.
Status on completion: `R3_LAB_VALIDATION_PARTIAL` (or `R4_PRODUCTION_REPLACEMENT_READY`
if the full live-load checklist passes).
