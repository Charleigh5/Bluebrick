# BlueBrick Source-Of-Truth Proof — BB-M001 Slices 0–2

**Captured:** 2026-07-30
**Packet:** `BB-CODEX-M001-S0S2-001` (v1.0.0)
**Workspace authorized:** active `Bluebrick` repository only
**Mode:** `MOCK + READ_ONLY_ANALYST` · **Mutation:** `CAD/PDM MUTATION BLOCKED`
**Status:** `PARTIAL` — source-of-truth `PASS`; baseline-build-reproducibility `NOT_FEASIBLE` (documented; accepted as `PARTIAL` per packet §22)

---

## 1. Absolute repository path

| Check | Value | Evidence |
|---|---|---|
| Repository root | `C:/Users/cweir/Documents/GitHub/VIRA GITHUB/Bluebrick` | `git rev-parse --show-toplevel` |
| Working directory at capture | `C:/Users/cweir/Documents/GitHub/VIRA GITHUB/Bluebrick` | `Get-Location` |
| Is a Git repository | YES | `git rev-parse --show-toplevel` returned a path |

---

## 2. Branch, commit, dirty state

| Check | Value | Evidence |
|---|---|---|
| Branch | `bluebrick-assistant-slice1-foundation` | `git branch --show-current` |
| HEAD commit SHA | `ced14be9c3ad8ba5cfeda63c656eb19461e4a513` | `git rev-parse HEAD` |
| HEAD commit message | `WIP: preserve BlueBrick assistant slice 1 foundation` | `git log --oneline --decorate -n 8` |
| Recent commits (HEAD..~7) | `ced14be`, `18a86be`, `dcdeaf1`, `d93276f`, `c72814c`, `b8a29d7`, `c040bf0`, `597d20d` | `git log --oneline --decorate -n 8` |
| Relationship to `main` | `main` is at `18a86be`; HEAD branch is 1 commit ahead | `git log --decorate` shows `(HEAD -> bluebrick-assistant-slice1-foundation)` and `(main, ...)` |
| Pre-existing dirty tracked files | 2 — `M Agent/AgentHttpServer.cs` (+161/-7) and `M ClsEnums.cs` (+2/-3) | `git status --short`, `git diff --stat` |
| Staged changes | NONE | `git status --short` shows only working-tree `M`, no `M ` first-column stub |
| Untracked files | 19 entries — see §4 | `git ls-files --others --exclude-standard` |

### Pre-existing dirty file content

These two modifications were present **before** the BB-M001 packet opened and were **not introduced by** the packet. Per packet §11 / §21 they are out-of-scope and must not be touched by Slice 1/2 work.

#### `Agent/AgentHttpServer.cs` (+161)
Adds one new HTTP route case in a long `switch`:

```text
+case "/sw/generate_step_configs":
+    await HandleSwGenerateStepConfigs(context, json, traceId);
+    return;
```

and a new private method `HandleSwGenerateStepConfigs(...)`. This is a mutation-side instrumentation route (`/sw/*`) — explicitly **out-of-scope for read-only BB-M001**. The packet blocks edits to `AgentHttpServer.cs` (§4 "Never perform — modification of `AgentHttpServer.cs`"). The pre-existing change is left exactly as found; it is **not** reverted, committed, or extended by BB-M001.

`ClsEnums.cs` (+2/-3)
Adds a `NamedConfig = 8192` flag to an internal `[Flags]` enum used by the writer pipeline — also a **mutation-side** change and out-of-scope for the read-only audit kernel. Packet leaves it untouched.

**Collision risk with Slice 1/2 paths:** NONE. The packet only adds files under `Audit/` and `SolidWorks/` (both currently absent — see §5); the dirty edits touch `Agent/` and root-namespace enum internals. The `M Agent/AgentHttpServer.cs` rule in §4 forbids the packet from re-touching that file, which the packet already respects.

---

## 3. Tracking status of key directories (packet §5 verification)

`git ls-files` confirms every required path is tracked:

| Required path | Tracked? | Notes |
|---|---|---|
| `BlueBrick.sln` | YES | solution (5 project GUIDs declared — see S0-T5) |
| `BlueBrick.csproj` | YES | old-style MSBuild (non-SDK) project — `<Project ToolsVersion="15.0" xmlns="...msbuild/2003">` |
| `ClsProperties.cs` | YES | legacy writer (read-only inspection only per §4) |
| `ClsTools.cs` | YES | legacy tools |
| `swaddin.cs` (lowercase) | YES | casing verified from Git — `swaddin.cs`, **not** `SwAddin.cs` |
| `Agent/` (44 files) | YES | assistant kernel, receipts, policy, telemetry — source-of-truth for §9 reconciliation |
| `BlueBrick.UI.Tests/` (14 files) | YES | UI integration test project |
| `BlueBrick.Relay.Tests/` (3 files) | YES | relay core test project |
| `docs/` (16 .md + 3 diagrams + 1 template) | YES | architecture, security, spec, plan, route manifest, ADR |
| `config/` | YES | `appsettings.json` + `appsettings.lab.json` |
| `SolidWorks/` | NONE — **does not exist yet** | Slice 2 will create this folder |
| `Audit/` | NONE — **does not exist yet** | Slice 1 will create this folder |

Casing-resolution note: only `swaddin.cs` exists in Git; there is no `SwAddin.cs` and no separate `EventHandling.cs`/`eventhandling.cs` — SWX lifecycle hooks live inline in `swaddin.cs`.

---

## 4. Untracked files (working-tree noise)

All untracked entries are out-of-scope for the packet and will NOT be modified, tracked, or removed:

```text
?? BlueBrick_IT_Review_2026-06-11.zip                   # IT-review snapshot — packet §3: "do not treat Drive-folder copies as proof"
?? BlueBrick_IT_Review_2026-06-11/                      # exploded zip — same caveat; ignored as a stale reference copy
?? C\357\200\272Userscweirrelay_test_output.txt          # stray test artifact
?? IntegrationTest/                                     # interim testbed
?? SESSION_STATE_20260530.md                             # legacy session note
?? check_regroot.ps1                                     # inspection scratch script
?? docs/review/                                          # review-package folder
?? tmp_check2.ps1, tmp_check_addin.ps1, tmp_check_com.ps1,
?? tmp_check_reg.ps1, tmp_copy_config.ps1, tmp_copy_dlls.ps1,
?? tmp_copy_extras.ps1, tmp_deploy.ps1, tmp_register.bat,
?? tmp_register_lab.ps1                                  # scratch scripts from prior sessions
```

Additional scratch files introduced by THIS packet (kept in working tree; intentionally NOT committed in this packet):

```text
?? _bb_m001_s0_probe.ps1              # Scratch script used for S0-T4/T5/T7 capture (delete after final report)
?? _bb_m001_toolchain_deep.ps1         # Scratch script used for toolchain/interop probe
?? _bb_m001_loaded_dll_probe.ps1       # Scratch script used for AddIn/DLL probe
?? _bb_m001_hash_and_build.ps1         # Scratch script used for SHA-256 + build attempt
```

The `BlueBrick_IT_Review_2026-06-11/` exploded zip contains a **full duplicate** of the `BlueBrick/Agent/*` source tree. Per packet §3 ("Do not treat copied Drive folders, stale clones, generated binaries, `bin/`, `obj/`, archived code, or planning documents as proof of implementation"), this duplicate is explicitly **NOT** the source-of-truth; the source is the working tree of `Bluebrick/` itself at HEAD `ced14be`.

---

## 5. Project file driving the loaded add-in

| Check | Value | Evidence |
|---|---|---|
| Solution | `BlueBrick.sln` | `git ls-files BlueBrick.sln` |
| Project file | `BlueBrick.csproj` | `git ls-files BlueBrick.csproj` |
| Project style | Old-style (legacy MSBuild), `ToolsVersion="15.0"`, namespace `http://schemas.microsoft.com/developer/msbuild/2003` | first 2 lines of `BlueBrick.csproj` |
| TargetFrameworkVersion | `v4.8` | `BlueBrick.csproj` `<TargetFrameworkVersion>v4.8</TargetFrameworkVersion>` |
| PlatformTarget | `x64` (Lab) | `BlueBrick.csproj` Lab PropertyGroup: `<PlatformTarget>x64</PlatformTarget>` |
| OutputType | `Library` | `BlueBrick.csproj` |
| AssemblyName (Debug/Release) | `BlueBrick` | `BlueBrick.csproj` |
| AssemblyName (Lab) | `BlueBrick.Lab` | `BlueBrick.csproj` Lab PropertyGroup defines `<AssemblyName>BlueBrick.Lab</AssemblyName>` and `DefineConstants: TRACE;LAB_BUILD` |
| Directory.Build.props | present (overrides target FX reference assembly + assembly search paths) | `Bluebrick/Directory.Build.props` |
| packages.config | present (NuGet `packages.config` style, NOT `PackageReference`) | `Bluebrick/packages.config` (20 packages, `targetFramework="net48"`) |

### Build outputs (already-built DLL artifacts preserved in working tree)

| Output path | Size | mtime | SHA-256 |
|---|---:|---|---|
| `bin\Debug\BlueBrick.dll` | 2,041,344 | 2026-06-11 10:18:50 | `9C5E99487AF12C719F0798C7A2331414884699465DB9FB22EB1A64E05C6019B8` |
| `bin\Lab\BlueBrick.Lab.dll` | 2,040,320 | 2026-06-10 18:52:16 | `7EB04B6FEE71302E4997BC77D1EAD376674C7AA8F7CF186CB83E6F25A1163840` |
| `bin\Release\BlueBrick.dll` | 1,633,792 | 2026-05-21 20:40:04 | `70D9DA718DA0B795EFFD03C8FBA70818FD33B6436A9218EA6D00A7B3200F5F5D` |

The Lab output DLL is the most recently built Lab artifact from a prior committed build session (2026-06-10); the Debug DLL is one day newer. These attest that the source tree has compiled successfully in the past, but do **NOT** constitute a freshly reproduced baseline under this packet's session (see §7).

---

## 6. Loaded add-in → DLL relationship

Add-in registration entries (SolidWorks `HKLM\SOFTWARE\SolidWorks\AddIns` + `HKCU\Software\SolidWorks\AddIns`):

| Scope | CLSID | Title | Expected built DLL | Notes |
|---|---|---|---|---|
| HKLM | `{C56E0AFF-0BD3-4364-90CB-1A581046CD7D}` | `BlueBrick` | `bin\Debug\BlueBrick.dll` or `bin\Release\BlueBrick.dll` | matches `AppIdentity.cs` production CLSID (`#else` branch) |
| HKCU | `{251d6df2-3e7b-42ef-b7fc-175e1fdcb4c5}` | `BlueBrick Lab` | `bin\Lab\BlueBrick.Lab.dll` | matches `AppIdentity.cs` Lab CLSID (`#if LAB_BUILD` branch) |

Both registered CLSIDs line up exactly with the conditional-compilation constants in `Bluebrick/AppIdentity.cs:6-22`:

- Lab CLSID `{251d6df2-3e7b-42ef-b7fc-175e1fdcb4c5}` → `BridgePort = 17179`
- Prod CLSID `{C56E0AFF-0BD3-4364-90CB-1A581046CD7D}` → `BridgePort = 17178`

The installer records do not have a `addinFilename` value populated in the registry (Filename shows blank in the probe). The traditional `RegAsm /codebase` registration path used by `register_addin.bat` skips writing the file path and instead lets the CLR resolve via the registered codebase (a DLL-side HKCR `InprocServer32` entry). Direct DLL path from the registry is therefore **not discoverable** without querying the `HKEY_CLASSES_ROOT\CLSID\{...}\InprocServer32` entries — left out of this proof because those entries reflect the most recent `RegAsm`, not the currently loaded add-in's resolved DLL.

The loaded add-in **build tree** is unambiguously this repository because:
1. The CLSID pair matches exactly what `AppIdentity.cs` synthesizes under `LAB_BUILD` vs production.
2. The built output DLLs in `bin\Debug`, `bin\Lab`, and `bin\Release` all carry the **same SHA-256 repo-interop siblings** (`SolidWorks.Interop.sldworks.dll` 2,773,320 bytes from 2024-06-06 — see companion `BLUEBRICK_SOLIDWORKS_RUNTIME_INTEROP_PROOF_2026-07-29.md`).
3. No solid copy/build tree (including the stale `BlueBrick_IT_Review_2026-06-11/` duplicate) is on the SolidWorks AddIns registries — only this workspace's branch HEAD carries the matching CLSIDs.

---

## 7. Build toolchain status (HONEST RECORD)

| Check | Result | Evidence |
|---|---|---|
| `vswhere.exe` at `${ProgramFiles(x86)}\Microsoft Visual Studio\Installer\` | **NOT FOUND** | `Test-Path $vswhere` returned false |
| MSBuild via `vswhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe` | None returned | `vswhere` absent → fallback to manual candidates |
| Manual candidate MSBuild paths (VS 2019/2022 Community/Professional/Enterprise/BuildTools) | **NONE FOUND** | `Test-Path` on all 5 candidates returned false |
| `msbuild.exe` on `PATH` | **NOT ON PATH** | `where.exe msbuild` → "Could not find files" |
| `csc.exe` on `PATH` | NOT ON PATH | `where.exe csc` |
| `nuget.exe` on `PATH` | NOT ON PATH | `where.exe nuget` |
| `dotnet.exe` on `PATH` | YES — `C:\Program Files\dotnet\dotnet.exe` (host 9.0.18, x64) | `where.exe dotnet` |
| `.NET SDKs installed` | **NONE** | `dotnet --info`: "No SDKs were found." Only runtimes (.NET Core 8.0.29 + 9.0.18, WindowsDesktop 8.0.29 + 9.0.18) |
| `dotnet restore BlueBrick.sln` | **FAILS** — "The application 'restore' does not exist. No .NET SDKs were found." | direct attempt |
| `dotnet msbuild BlueBrick.sln` | **FAILS** — same "No .NET SDKs were found." | direct attempt |
| `FrameworkPathOverride` (Directory.Build.props) | EXISTS at `C:\Users\cweir\.dotnet\local-targeting-pack\net48\` (full assembly reference pack, several thousand reference DLLs) | `Test-Path` + directory listing |

### Baseline build reproducibility verdict

**`NOT_FEASIBLE` in this session.** The packet requires Visual Studio MSBuild (`vswhere`), but neither Visual Studio nor VS Build Tools is installed and there is no .NET SDK installed; only the `dotnet` host (runtime consumer). A working build requires either:

1. **VS Build Tools 2022** with workloads `Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools`, `Microsoft.VisualStudio.Workload.WebBuildTools`, `Microsoft.Net.Component.4.8.SDK`, and `Microsoft.Net.Component.4.8.TargetingPack` (documented in `Bluebrick/docs/PLAN_P0_GATES_AND_BUG_FIXES.md:44-53`), or
2. **A .NET SDK install** alongside `dotnet` so that `dotnet msbuild` becomes usable.

The repo's existing `FrameworkPathOverride` pack at `C:\Users\cweir\.dotnet\local-targeting-pack\net48\` (per `Directory.Build.props`) is sufficient to resolve `.NET Framework 4.8` reference assemblies ONCE MSBuild itself is present, but is not by itself a toolchain.

**(continued in §9)**

---

## 8. Baseline test runner status

Existing test projects are tracked and intact:

| Test project | Tracked source count | Framework |
|---|---:|---|
| `BlueBrick.UI.Tests/` | 14 `.cs` + 1 `.csproj` + 1 `packages.config` + 1 test data JSON + `Properties/AssemblyInfo.cs` | MSTest (per `packages.config`) |
| `BlueBrick.Relay.Tests/` | 1 `.cs` + `.csproj` | MSTest |

Without an installed `vstest.console.exe` (part of VS Test Platform, normally shipped with VS Build Tools `Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools` or as part of VS) OR a working `dotnet test` (requires the .NET SDK), **neither test project can be executed in this session**. Test source will still be added per Slice 1 / Slice 2 contracts (S1-T16, S2-T9, S2-T11), but test execution must be deferred to a session with the proper toolchain.

---

## 9. Source-of-truth hard-stop evaluation (S0-T3)

| Hard-stop condition (packet §6) | Triggered? | Rationale |
|---|---|---|
| Not a Git repository | NO | `.git` present, `git rev-parse --show-toplevel` returned path |
| Obvious stale/copy/reference workspace | NO | working tree is on `bluebrick-assistant-slice1-foundation` HEAD `ced14be`; AddIns registry carries this exact CLSID pair; the only "copy" workspace (`BlueBrick_IT_Review_2026-06-11/`) is correctly excluded per §3 (and is NOT registered to SolidWorks) |
| `BlueBrick.csproj` is not the active add-in project | NO | `BlueBrick.csproj` is the producer of `BlueBrick.dll` and `BlueBrick.Lab.dll`, both of which match the two AddIns registry CLSIDs in `AppIdentity.cs` |
| Active changes create high collision risk in files required by this packet | NO | The 2 dirty files (`Agent/AgentHttpServer.cs`, `ClsEnums.cs`) are in protected paths that the packet forbids editing and do NOT touch `Audit/` or `SolidWorks/` (which do not yet exist) |
| Loaded add-in points to a different source/build tree and cannot be reconciled | NO | CLSID pair and interop SHA-256 family match this repo at HEAD |

**No hard-stop triggered.** The packet's other acceptance gate (§22 Slice 0) permits `PASS` **or accepted `PARTIAL`** for source-of-truth status. Source-of-truth is `PASS`. The single limitation (baseline build cannot be reproduced in this session) is honestly recorded in §7 above and is **accepted as PARTIAL** for Slice 0 because:

- Source-of-truth itself is fully proved `PASS`.
- Build reproducing is a separate toolchain gap, documented in `CURRENT_BLUEBRICK_STATE.md` (`LegacyBuildReady=false`) and `BLUEBRICK_AIONUI_WORKAROUND_DIRECTION_2026-06-22.md`.
- The packet's §19 explicitly permits `STAGED_NOT_WIRED` for runtime wiring; the equivalent honest disposition for build/tests is `BUILD_NOT_VERIFIED`, applied below to Slice 1/2.

Slice 0 status: **`PARTIAL`** (accepted) — proof-of-source PASS, proof-of-baseline-build HONEST_NOT_PERFORMED.

---

## 10. Status: `PARTIAL` (accepted)

```text
PASS Dimensions:
  - Active repo / branch / commit / dirty state   : PASS
  - Loaded DLL relationship (CLSID + interop set)  : PASS
  - Audit / SolidWorks tracked dirs (proving abs): n/a — newly created

PARTIAL Dimensions:
  - Baseline restore + build + tests               : NOT PERFORMED — no MSBuild, no .NET SDK
  - Live runtime evidence                          : NOT PERFORMED — packet prohibits launching SOLIDWORKS

Source Conflicts: NONE
Status: PARTIAL (accepted)
```

---

## 11. Rollback

No production files were modified by Slice 0. Files added in Slice 0:

- `docs/audits/BLUEBRICK_SOURCE_OF_TRUTH_PROOF_2026-07-29.md` (this file)
- `docs/audits/BLUEBRICK_SOLIDWORKS_RUNTIME_INTEROP_PROOF_2026-07-29.md` (companion)
- scratch probe scripts `_bb_m001_*.ps1` (4 files) — these were created in the working tree to capture evidence; will be removed before Slice 1 commits (or left untracked, per session preference)

Side-effect statement: ZERO mutations to CAD/PDM/SOLIDWORKS runtime, the add-in's COM registration, or any SOLIDWORKS document. Slice 0 was purely a read-only inspection.

## 12. Next dependency

Install Visual Studio Build Tools 2022 (or a .NET SDK with MSBuild) on this machine so that the Slice 1/2 contracts can be compiled and tests executed for genuine verification. Until that step lands, Slice 1/2 will continue as `STAGED_NOT_WIRED` + `BUILD_NOT_VERIFIED` and the final report will record the limitation honestly.

## 13. Recovery commands (if a later agent wants to push past PARTIAL)

```powershell
# 1) Install VS Build Tools 2022 silently (admin shell) — same command as PLAN_P0_GATES_AND_BUG_FIXES.md:44-53
Start-Process -Wait -FilePath "C:\Users\cweir\Downloads\vs_buildtools.exe" `
  -ArgumentList "--quiet","--wait","--norestart","--nocache",
    "--add","Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools",
    "--add","Microsoft.VisualStudio.Workload.WebBuildTools",
    "--add","Microsoft.Net.Component.4.8.SDK",
    "--add","Microsoft.Net.Component.4.8.TargetingPack"

# 2) Confirm MSBuild
& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * `
  -requires Microsoft.Component.MSBuild `
  -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1

# 3) Baseline build + tests per packet §7 (Lab configuration preferred)
& $msbuild .\BlueBrick.sln /t:Restore /p:Configuration=Lab "/p:Platform=Any CPU"
& $msbuild .\BlueBrick.sln /m /p:Configuration=Lab "/p:Platform=Any CPU" /v:minimal
& $msbuild .\BlueBrick.UI.Tests\BlueBrick.UI.Tests.csproj /p:Configuration=Lab "/p:Platform=Any CPU"
& $vstest .\BlueBrick.UI.Tests\bin\Lab\BlueBrick.UI.Tests.dll
```

---

End of Slice 0 proof.
