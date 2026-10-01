# BB20-UI-SNAPSHOT-TRIGGER-20260927-R01 — live SOLIDWORKS UI-trigger run (route C, no rebuild)

- Run ID: `BB20-UI-SNAPSHOT-TRIGGER-20260927-R01`
- Backups taken: `BB20-UI-SNAPSHOT-TRIGGER-20260927-R01-LIVE-R02`, `-LIVE-R03`
- Branch/HEAD: `bluebrick-assistant-slice1-foundation` @ `057b95c`
- **Result: the React `Snapshot` control was NOT reached. Live UI leg NOT_VERIFIED.**
  What this run did prove is recorded below; nothing here is extrapolated.

## Machine preparation

User-owned SOLIDWORKS PID `16304` (started 2026-09-28 20:34, no active document) was exited
through the repo's guarded helper only: `tools/lab-addin-control.vbs probe` confirmed
`ActiveDocIsNothing=True`, then `exit` called `sw.ExitApp`. Exit 0; the process ended within 60s
with no force, no `Stop-Process`, no `CloseDoc`, no save. This was user-authorised route C.

`SolidWorks Licensing Service` was started for the run (Manual, LocalSystem, no startup-type
change) and is back to **Stopped / Manual**, its original state.

## Run 1 — owned PID 3024: host loaded, then exited on its own

| Check | Observed |
| --- | --- |
| Lab direct load without HKCU discovery | PASS — `SOLIDWORKS Lab direct load verified pid=3024 bridge=17179` |
| Bridge | PASS — listener on 17179 |
| `reactMounted` / `documentUrl` | `true` / `https://bluebrick-ui.invalid/index.html` |
| `bbCallbacks` | exactly **17** |
| `frontendBuildId` | `BB20-UI-SNAPSHOT-TRIGGER-20260927-R01` |
| `frontendEnvironment` | `LAB` |
| telemetry `errors` / `resourceErrors` / `unhandledRejections` | all empty |
| `result` | `BOOTSTRAP_RECEIPT_COMPLETE` |

This confirms the **pushed** `057b95c` bundle loads and mounts in the real SOLIDWORKS WebView.

At 13:13:26 local the host began `DisconnectFromSW` and completed it. I did not call unload,
`ExitApp`, or any teardown before that point; the only interactions were read-only COM probes.

### Teardown ordering re-verified live on the currently pushed code

```
DisconnectFromSW start
  Cleanup begin/complete (development sandbox)
  Cleanup begin/complete (assistant preview)      <-- preview closed first
  Cleanup begin/complete (command manager)
  Cleanup begin/complete (event handlers)
  Cleanup begin/complete (audit composition)
  Cleanup begin/complete (agent server)           <-- server stopped after preview
  Cleanup begin/complete (agent overlay)
  Cleanup begin/complete (task pane)
  Cleanup begin/complete (COM release)            <-- COM released last
DisconnectFromSW success
```

This re-confirms the ordering guarantee from `c877aa4` on the current pushed code.

**Cause of the SOLIDWORKS exit is NOT established.** The Windows Application event log holds no
SLDWORKS crash record in the surrounding window (the only WER entries are an unrelated
`OpenAI.Codex` MoAppHang and Microsoft Store update failures). The shutdown was clean and
add-in-aware, not a crash.

## Run 2 — owned PID 15764: the blocker, precisely located

The Lab host loaded identically (direct load verified, bridge 17179). To drive the React control I
enumerated **every top-level window owned by PID 15764** through UI Automation:

| Top-level window | Class | Descendants | Snapshot control |
| --- | --- | --- | --- |
| `SOLIDWORKS Professional 2025 SP5.0` | `Afx:0000000140000000:…` | 219 | not found |
| `BlueBrick Lab Assistant Preview` | `WindowsForms10.Window.8.app…` | 34 | not found |

No window, pane, or element named `Snapshot` exists in that process. No React marker string
(`BlueBrick 2.0`, `Active Document`, `Read-only context`) appears anywhere in either tree.

**The only WebView exposed to UI Automation is the `FrmAssistantWindow` inline
`NavigateToString` fallback shell** — its controls read `Chat`, `Send`, `New Session`,
`Capture Window`, `Attach Image/PDF`, `Reindex Vault`, `Reset Vault`, `Open Working`,
`Open in ChatGPT`, `Test Assistant`, `Use Mock`. That is *not* `AssistantWeb`. Treating it as the
React surface is the documented false-positive, so it was not used.

Therefore the React task-pane surface (`FrmPane` → `AssistantPanel` → WebView2) is not reachable
or operable from this session, which matches the earlier finding that the SOLIDWORKS window cannot
be foregrounded into the captured desktop session. React **loads**; its controls cannot be
**operated** here.

## Recorded identity skew — this run is not identity-clean

| Source | Value |
| --- | --- |
| `runtime-manifest.json` `gitCommit` | `057b95cdeea6aa90b3303b5eed7f89e9a3c30d53` (HEAD) |
| WebView readback `frontendSourceCommit` | `14d871bb02371f870e0201fb96b75b9998ec4202` (parent of HEAD) |

`057b95c` adds exactly the source that produced this bundle and nothing else, so the package is
functionally coherent, but the two identities differ. This is the same class of skew that correctly
failed identity during the R02 acceptance run, and it is recorded rather than reasoned away. Route
B (rebuild under a fresh RunId) remains the way to get a coherent identity.

## Non-mutation

| Item | Result |
| --- | --- |
| Disposable part `bb05-uitrigger-part.sldprt` | `D171DAFB4FA34A02AAD2BA3C83BE314097E0E8A8BFE1671D71CBEE2E1234DDC3` before and after — never opened (`OpenDoc` never succeeded) |
| Installed donor `Part.SLDPRT` | `D171DAFB…` unchanged |
| Production DLL `C:\BlueBrick\BlueBrick.dll` | `0F4CF55DA27011598719A42E740B85BF15D3BDAF2F83B8812753ED38F14BE211` unchanged |
| Deployed Lab DLL | `9287B2E60FC77A59BAA0E961A1EB101C7EED292EFBFFEA5BFB72A36A2CCE5E21` (the intended R01 candidate) |
| CAD / PDM / Epicor / database / provider | none |

## Final state

Both owned PIDs (3024, 15764) exited normally through the guarded unload + `ExitApp` path. Zero
`SLDWORKS` processes, zero listeners on 17178/17179, no stale ownership record. Registry backups for
both runs are under `C:\BlueBrickLab\backups\BB20-UI-SNAPSHOT-TRIGGER-20260927-R01-LIVE-R0*`.

## What is required to close the live leg

A session that can actually operate the SOLIDWORKS task pane — an interactive desktop where the
task pane can be opened and its React surface driven. No rebuild is required for that; the blocker
is UI reachability, not the package.
