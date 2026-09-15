# BB20-UI-ACTIVATION-001 — R01 Receipt

Promotion decision: `BLOCKED_EXTERNAL`

First broken transition: Lab registration → SOLIDWORKS Lab add-in discovery.

Primary failure: `F09_WRONG_REGISTRATION`.

Evidence:

- Source, `bin\Lab`, and `C:\BlueBrickLab` frontend hashes match exactly.
- Lab config resolves `UseReactWebView=true` and bridge port `17179`.
- Per-user COM activation and Lab startup registration pass.
- SOLIDWORKS PID 49356 reached its normal window but did not load the Lab DLL.
- No `17179` listener or Lab WebView telemetry appeared.
- `HKLM\SOFTWARE\SolidWorks\Addins\{251d6df2-3e7b-42ef-b7fc-175e1fdcb4c5}` is absent.
- The governed one-time elevated Lab registration was attempted but Windows UAC was canceled; no machine registration was written.

Production hashes remained unchanged. Rollback backup: `C:\BlueBrickLab\backups\20260902-112158`.

Next dependency: user approval of the one-time UAC prompt for the distinct Lab GUID registration, followed by a new R02 launch.
