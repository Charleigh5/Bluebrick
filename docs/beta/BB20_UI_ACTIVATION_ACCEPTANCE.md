# BB20 UI Activation Acceptance

Current run: `BB20-UI-20260902-111609-R01`

Current decision: `BLOCKED_EXTERNAL`

The authoritative React frontend builds and packages correctly. Source, `bin\Lab`, and `C:\BlueBrickLab` contain byte-identical frontend artifacts. The live run stopped before WebView activation because SOLIDWORKS 2025 did not discover/load the HKCU-only Lab add-in registration.

First failure: `F09_WRONG_REGISTRATION`.

The distinct Lab machine discovery key is absent:

`HKLM\SOFTWARE\SolidWorks\Addins\{251d6df2-3e7b-42ef-b7fc-175e1fdcb4c5}`

The governed one-time elevated registration reached UAC but was canceled. No HKLM write occurred. After that exact Lab registration is approved, start a new R02 run and re-check DLL load, WebView resources, React bootstrap, native fallback disappearance, bridge port `17179`, and the visible UI.
