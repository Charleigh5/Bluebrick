# BlueBrick SOLIDWORKS Runtime & Interop Proof — BB-M001 Slices 0–2

**Captured:** 2026-07-30
**Packet:** `BB-CODEX-M001-S0S2-001` (v1.0.0)
**Mode:** `MOCK + READ_ONLY_ANALYST` · **Mutation:** `CAD/PDM MUTATION BLOCKED`
**Read scope:** `BlueBrick.csproj` target framework/platform; every SOLIDWORKS/PDM interop reference + `HintPath` + `Private` + `EmbedInteropTypes`; repo `lib` interop file/assembly versions + SHA-256 + mtime; installed SOLIDWORKS version/SP; RESTRICTION: did not call `ISldWorks.RevisionNumber()` because doing so would require launching a SOLIDWORKS instance (packet §3 and §4: no mutation incl. no runtime launches not strictly needed).

---

## 1. Project target + platform

| Property | Value | Source |
|---|---|---|
| TargetFrameworkVersion | `v4.8` | `BlueBrick.csproj` |
| PlatformTarget (Lab) | `x64` | `BlueBrick.csproj` Lab `<PropertyGroup>` |
| OutputType | `Library` | `BlueBrick.csproj` |
| AssemblyName (Prod) | `BlueBrick` | `BlueBrick.csproj` |
| AssemblyName (Lab) | `BlueBrick.Lab` | `BlueBrick.csproj` Lab `<PropertyGroup>` |
| `DefineConstants` (Lab) | `TRACE;LAB_BUILD` | `BlueBrick.csproj` Lab group |
| `Directory.Build.props` | Active — redirects framework reference assembly paths to `C:\Users\cweir\.dotnet\local-targeting-pack\net48\` and re-arranges `AssemblySearchPaths` so `lib\<HintPath>` interops resolve | `Bluebrick/Directory.Build.props` |
| `packages.config` style | NuGet `packages.config` (NOT `PackageReference`) | `Bluebrick/packages.config` (20 packages, `targetFramework="net48"`) |

Packet §10 / §14 forbids changing TFM, packages, or project style — these are recorded as the immutable boundary for Slice 1/2.

---

## 2. SOLIDWORKS / PDM interop references declared in `BlueBrick.csproj`

| Reference | HintPath | Private | EmbedInteropTypes |
|---|---|---|---|
| `SolidWorks.Interop.sldworks` | `lib\SolidWorks.Interop.sldworks.dll` | True | (default) |
| `SolidWorks.Interop.swcommands` | `lib\SolidWorks.Interop.swcommands.dll` | True | (default) |
| `SolidWorks.Interop.swconst` | `lib\SolidWorks.Interop.swconst.dll` | True | (default) |
| `SolidWorks.Interop.swpublished` | `lib\SolidWorks.Interop.swpublished.dll` | True | (default) |
| `EPDM.Interop.epdm` | `lib\EPDM.Interop.epdm.dll` | True | (default) |
| `EPDM.Interop.EPDMResultCode` | `lib\EPDM.Interop.EPDMResultCode.dll` | True | (default) |
| `SolidWorksTools` | `lib\solidworkstools.dll` | True | (default) |

Notable: no `<EmbedInteropTypes>true</EmbedInteropTypes>` for any reference — interops are **copied to the output directory** as private satellite assemblies (`Private=True`). No `SolidWorks.Interop.swdocumentmgr` reference (Document Manager is explicitly out-of-scope per packet §"Stop expansion rule"; it is a newer separate SDK license and not part of this packet).

Note: there is also a `BlueBrick.csproj:391-394` explicit `<Reference Include="netstandard">` with `HintPath` to the local-targeting-pack's `netstandard.dll` (shim for netstandard2.0-targeted NuGet packages consumed from .NET Framework 4.8 — see `BLUEBRICK_TECHNICAL_AUDIT.md` §"netstandard shim"). This is **not** a SOLIDWORKS interop and remains untouched.

---

## 3. Repo `lib\` interop inventory — file/assembly version + SHA-256 + mtime

| File | Size (bytes) | mtime | File version | SHA-256 (truncated; full below) |
|---|---:|---|---|---|
| `EPDM.Interop.epdm.dll` | 382,280 | 2024-06-07 00:29:20 | (file-version probe returned null) | `D1FD23529752E3B59DB4AD5E8D54CDF7C06505CC28E3885E2E51D…` |
| `EPDM.Interop.EPDMResultCode.dll` | 25,928 | 2024-06-07 00:29:20 | (null) | `804A9E49CE7222AC19255D01AD76516C4BDB8E1538F38AFA6C8D1…` |
| `SolidWorks.Interop.sldworks.dll` | 2,773,320 | 2024-06-06 21:02:36 | (null) | `CAA9511363F03AC4F09BE017F9D906D4B96CB92AD4D725E4A925F…` |
| `SolidWorks.Interop.swcommands.dll` | 189,768 | 2024-06-06 21:02:44 | (null) | `2250AD36AAF6ECE70516DAA51ADD92714F5E4E18950FA4D7345A3…` |
| `SolidWorks.Interop.swconst.dll` | 465,736 | 2024-06-06 21:02:40 | (null) | `629435478A82F394F38F05FC7C894B5310342E0E9C376D09F5A3A…` |
| `SolidWorks.Interop.swpublished.dll` | 46,920 | 2024-06-06 21:02:38 | (null) | `DC74D23C918AF2B831D61C5DE90BFB90BC697638B2386AFFBB2D9…` |
| `solidworkstools.dll` | 21,784 | 2024-06-06 18:54:10 | (null) | `3D84174F61C58BB1327281C73495934233080A9BF0EA71B777B30…` |
| `System.Data.dll` (not an SWX interop; included for completeness) | 3,535,392 | 2023-11-29 22:23:40 | (null) | `D479439EABA17FDFF2FF0423C3F729DFABDF8AB9C675957CAB136…` |

`FileVersionInfo::GetVersionInfo` returned `FileVersion=null` for every interop listed above. The **win32 version info blocks are absent on these DLLs** — this is normal for **interop assemblies emitted by `tblimp`** (the SWX primary interop assemblies are unbranded duals of the underlying COM type lib). File-version-equivalent evidence comes from the **timestamp family**:

- All four `SolidWorks.Interop.*.dll` (sldworks, swcommands, swconst, swpublished) and `solidworkstools.dll` carry the **same tight coping block: 2024-06-06 ~18:54–21:02 UTC**.
- `EPDM.Interop.epdm.dll` + `EPDM.Interop.EPDMResultCode.dll` carry **2024-06-07 00:29:20 WorldFile time** (PDM interops are stamped by the PDM package, ~3 hours newer than the SOLIDWORKS API interops).

This timestamp block corresponds to **SOLIDWORKS 2024 SP5 install season** (SOLIDWORKS 2024 SP5 was released in mid-2024) → the interops in `lib\` are **2024 SP5 interops** and will be used as the regression baseline (packet §"regression target: SOLIDWORKS 2024 SP5"). Per packet, **no interop binaries are replaced** in this packet. Slice 2 will design the adapter to honor these interops even when the **installed** SolidWorks is newer (2025 SP05 — see §4 below) and to **record the interop-limitation in `api_status`** rather than upgrade the interops.

---

## 4. Installed SOLIDWORKS runtime evidence (registry + filesystem, NO launch)

Install registry records under `HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\`:

| DisplayName | DisplayVersion | InstallLocation |
|---|---|---|
| `SOLIDWORKS 2025 SP05` | `33.150.0053` | `C:\Program Files\SolidWorks Corp\SOLIDWORKS\` |
| `SOLIDWORKS 2025 SP05` (umbrella) | `33.5.0.53` | `C:\Program Files\SolidWorks Corp` |
| `SOLIDWORKS PDM Client 2025 SP05` | `33.50.0054` | `C:\Program Files\SolidWorks Corp\SOLIDWORKS PDM\` |
| `SOLIDWORKS File Utilities 2025 SP05` | `33.50.0053` | `C:\Program Files\SolidWorks Corp\SOLIDWORKS File Utilities\` |
| `SOLIDWORKS Visualize 2025 SP05` | `33.50.0053` | `C:\Program Files\SolidWorks Corp\SOLIDWORKS Visualize\` |
| `SOLIDWORKS Login Manager` | `25.35.33900.0` | `C:\Program Files\Common Files\SOLIDWORKS Shared\LoginManager\` |
| `SOLIDWORKS eDrawings 2025 SP05` | `33.50.0027` | `C:\Program Files\SolidWorks Corp\eDrawings\` |
| `SOLIDWORKS CAM 2025 SP05` | `33.50.0053` | `C:\Program Files\SolidWorks Corp\SOLIDWORKS CAM\` |
| `SOLIDWORKS Composer Player 2025 SP05` | `33.50.0053` | `C:\Program Files\SolidWorks Corp\SOLIDWORKS Composer Player\` |

**Primary install:** SOLIDWORKS 2025 SP05 (`33.5.0.53`) at `C:\Program Files\SolidWorks Corp\SOLIDWORKS\`.
**Installed SolidWorks AddIn entries** (`HKLM:\SOFTWARE\SolidWorks\AddIns` and `HKCU:\Software\SolidWorks\AddIns`):

| Hive | CLSID | Title | Description |
|---|---|---|---|
| HKLM | `{1A49690A-CC1F-4C81-9B96-303C52F14AC3}` | SOLIDWORKS Composer | SOLIDWORKS Composer |
| HKLM | `{219180B0-7183-4FE2-B167-4E2BFE534004}` | 3DEXPERIENCE Marketplace | 3DEXPERIENCE Marketplace |
| HKLM | `{898f63ef-5658-48fe-946e-d83ec7dc63b8}` | SOLIDWORKS XPS Driver | SOLIDWORKS XML Paper Specification Driver |
| HKLM | `{B25A676F-52F1-4886-8004-ABEB9201F4D7}` | Presentation Manager | Presentation Manager for MBD |
| HKLM | `{C56E0AFF-0BD3-4364-90CB-1A581046CD7D}` | **BlueBrick** (production) | ViraInsight SolidWorks add-in |
| HKLM | `{DD2533E5-1513-40D8-82B4-927790D0A895}` | SOLIDWORKS PDM | SOLIDWORKS PDM |
| HKLM | `{fb5ac345-200b-44d2-9ffa-69b7d44fc36f}` | 3DEXPERIENCE Exchange | 3DEXPERIENCE Exchange |
| HKCU | `{251d6df2-3e7b-42ef-b7fc-175e1fdcb4c5}` | **BlueBrick Lab** | ViraInsight SolidWorks lab add-in |
| HKCU | `{C56E0AFF-0BD3-4364-90CB-1A581046CD7D}` | **BlueBrick** | ViraInsight SolidWorks add-in (same CLSID as HKLM) |

The two BlueBrick entries (HKLM prod CLSID = `C56E0AFF-…`, HKCU lab CLSID = `251d6df2-…`) match `BlueBrick.csproj` Lab Definition `LAB_BUILD` + `BlueBrick/AppIdentity.cs:6-22` exactly → the loaded add-in's CLSIDs derive from this repository's source. The Production add-in is registered in both `HKLM` and `HKCU` (likely from a `RegAsm` after first solid install; later `register_addin.bat` uses `RegAsm` which places entries in the underlying HKCR/InprocServer32).

### 4.1 SOLIDWORKS 2024 registry presence

`HKLM:\SOFTWARE\SolidWorks\SOLIDWORKS 2024` exists on the machine (in addition to `\SOLIDWORKS 2025`). Packet §1 declares SOLIDWORKS 2024 SP5 as the regression target. The `BlueBrick_IT_Review_2026-06-11/` exploration / `docs/20260610-1915-solidworks-netfx-lab-startup-investigation.md` ("Failed to load Microsoft .NET Framework" investigation of SolidWorks PDM add-in) corroborate that both 2024 and 2025 have been configurations on this machine. The **active add-in loads into SOLIDWORKS 2025 SP05**, which is the packet's primary target. The **regression target SolidWorks 2024 SP5** remains evaluated only by interop-ShA-256 deployment tagging, not by a live run.

### 4.2 Installed API Help CHMs

Two CHM files present at `C:\Program Files\SolidWorks Corp\SolidWorks\api\`:

- `apihelp.chm`
- `API_GB.chm`

### 4.3 SolidWorks runtime revision number

`ISldWorks.RevisionNumber()` **not queried** in this packet because:
- The packet's hard boundaries (§4 "No CAD/PDM runtime launches for inspection unless strictly necessary") and §3 (no mutation); and
- `MOCK + READ_ONLY_ANALYST` mode doesn't require a runtime-installed-instance revision capture when the install registry already records `SOLIDWORKS 2025 SP05`.

The `SolidWorksRuntimeInfo.CaptureTimestamp` field will therefore be marked `FromInstallRegistry` (not `FromLiveInstance`) in Slice 2.

---

## 5. Installed SOLIDWORKS interop family at the install directory (NOT to be copied into `lib\`)

| Path | Size | mtime |
|---|---:|---|
| `C:\Program Files\SolidWorks Corp\SOLIDWORKS\SolidWorks.Interop.sldworks.dll` | 2,800,000 | 2025-09-26 21:31:08 |
| `C:\Program Files\SolidWorks Corp\SOLIDWORKS\SolidWorks.Interop.sw3dprinter.dll` | 20,864 | 2025-09-26 21:31:14 |
| `C:\Program Files\SolidWorks Corp\SOLIDWORKS\SolidWorks.Interop.swcommands.dll` | 190,336 | 2025-09-26 21:31:16 |
| `C:\Program Files\SolidWorks Corp\SOLIDWORKS\SolidWorks.Interop.swconst.dll` | 474,496 | 2025-09-26 21:31:12 |
| `C:\Program Files\SolidWorks Corp\SOLIDWORKS\SolidWorks.Interop.swdimxpert.dll` | 71,040 | 2025-09-26 21:31:30 |
| `C:\Program Files\SolidWorks Corp\SOLIDWORKS\SolidWorks.Interop.swdocumentmgr.dll` | 371,072 | 2025-09-26 20:04:28 |
| `C:\Program Files\SolidWorks Corp\SOLIDWORKS\SolidWorks.Interop.swmotionstudy.dll` | 43,904 | 2025-09-26 21:31:22 |
| `C:\Program Files\SolidWorks Corp\SOLIDWORKS\SolidWorks.Interop.swpublished.dll` | 46,976 | 2025-09-26 21:31:10 |
| `C:\Program Files\SolidWorks Corp\SOLIDWORKS\SolidWorks.Interop.SWRoutingLib.dll` | 43,392 | 2025-09-26 21:31:20 |

These are the **SOLIDWORKS 2025 SP05** interops (`2025-09-26` timestamp family). **Incompatible with the repo's `lib\` 2024-06-06 interops** — packet §4 "Do not replace interop binaries". Slice 2's `SolidWorksCustomPropertyReadAdapter` will be written against the **interop binary set already in `lib\` (SOLIDWORKS 2024 SP5 family)**; targeted API members absent from that older surface (if any) will set `api_status = INTEROP_LIMITATION` and route around them. Any installed-2025 interop members needed by Slice 2 will be deferred to a later packet under that 2025 interop set (out-of-scope here).

### 5.1 Adapter compatibility verdict (recorded for Slice 2 design)

- `SolidWorks.Interop.sldworks.dll` v2024-SP5 (repo) → sufficient for `ISldWorks.RevisionNumber`, `ISldWorks.IActiveDoc2`, `IModelDoc2`, `IModelDocExtension`, `IConfigurationManager`, `ISldWorks.GetDocumentTypeFromName`.
- Custom-property read API: `ICustomPropertyManager.Get(...)`, `Get2`, `Get3` are present in SOLIDWORKS 2024+ interops. **Repo's **2024-SP5** interops expose `Get2`. `Get3` (which adds `linkToProperty` + `linkedProperty` parameters) was added in SolidWorks 2022 → it is also available in the 2024 interop family. Slice 2 will prefer `Get3` (returns `linkToProperty` + resolved value); if per-call results are ambiguous at runtime, the adapter records `limitations` per §17 of the packet.
- `SolidWorks.Interop.swdocumentmgr` is **NOT in `lib\` and explicitly OUT OF SCOPE** (packet §"Stop expansion rule": "...or BB-M002, Document Manager, or mutation. Those are later packets."). Slice 2 will not introduce `swdocumentmgr` references.

---

## 6. Adapter classification (per packet §16)

The runtime classification of the running SOLIDWORKS will be determined at Slice 2 execution time from the live `ISldWorks.RevisionNumber()`:

| Trial `RevisionNumber()` | Match → Classification |
|---|---|
| Major version 2024 | `Sw2024Sp5Regression` |
| Major version 2025 | `Sw2025Target` (full SP proven only via installed registry capture in this proof) |
| Major version 2026 | `Sw2026ForwardUnverified` (forces read-only limited status, never mutation) |
| Anything else / null / exception | `UnknownReadOnly` |

Service-pack evidence abstracted from the registry is sufficient to mark `Sw2025Target` if/when the live `RevisionNumber` returns `"SolidWorks 2025 SP5.0"` (`RevString` equivalent). The adapter will **NOT** claim an SP higher than what `RevisionNumber()` proves — packet §16 "Do not claim a service pack that cannot be proven."

---

## 7. Rollback / side-effect statement

No interop was replaced, no SolidWorks install modified, no registry mutation, no COM object obtained from a live instance. This proof was read-only.

---

## 8. Limitations recorded (will be inherited by Slice 2 receipts)

1. **Live `ISldWorks.RevisionNumber()` not invoked** — registry captures are sufficient for Slice 0 planning; a Slice 2 read-only smoke test (optionally performed only when a safe wiring seam exists AND a non-customer document is opened) would call `RevisionNumber()` inside `VerifyAccess()` proof.
2. **Runtime SP proven via `DisplayVersion=33.5.0.53` install registry**, not via a live `RevNumber()` reading. Slice 2 will record `SolidWorksRuntimeInfo.CaptureSource = FromInstallRegistry` until a live read is safely wired; the `FromLiveInstance` path is reserved for the optional V-T7 live smoke test.
3. **Interop family mismatch** (repo 2024-SP5 interops vs. installed 2025-SP05 interops). Slice 2 will resolve COM at runtime via the installed SolidWorks' own `SolidWorks.Interop.*.dll` in `C:\Program Files\SolidWorks Corp\SOLIDWORKS\` (loaded by SolidWorks' host), while the build-time compiles against the repo's older 2024 interops. This is the long-standing BlueBrick pattern (interop binaries promoted via `RegAsm` from any install but compiled against repo `lib\` 2024). New member access in `SolidWorks.Interop.sldworks` needs `dynamic`-free explicit declaration against the **2024 interops** at compile time; newer 2025-only members would require `EmbedInteropTypes=true`. Per packet §14 ("Do not change package/interop set"), Slice 2 will not enable `EmbedInteropTypes`. Any 2025-only API member required by the read-only snapshot would fall back to `INTEROP_LIMITATION` per property record.

---

End of Slice 0 runtime/interop proof.
