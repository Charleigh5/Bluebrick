# BlueBrick Lab Runtime Acceptance Matrix

Run: `20260901-163846`

| Gate | Proof required | Current state |
|---|---|---|
| T0 | Static source/policy/contract checks | `PASS_CURRENT` |
| T1 | Focused behavioral tests | `PASS_CURRENT` (UI 267 total / 266 passed / 1 skip; Relay 4/4) |
| T2 | VS MSBuild Release and Lab builds | `PASS_CURRENT` |
| T3 | Release/Lab package output and hash checks | `PASS_CURRENT` |
| T4 | Lab-only deploy/register with backup manifest | `PASS_WITH_WARNINGS` |
| T5 | Lab SOLIDWORKS host/process/bridge observation | `BLOCKED_NO_LAB_BRIDGE` |
| T6 | SolidWorks read-only snapshot/selection/feature-tree evidence | `BLOCKED_T5` |
| T7 | Disposable reversible CAD mutation, only if trusted approval exists | `BLOCKED_BY_APPROVAL_LIFECYCLE` |
| T8 | PDM read-only evidence, only if already authenticated and secure | `NOT_RUN; DEFAULT_DISABLED` |
| T9 | Close/reopen and automatic Lab rollback/relaunch | `NOT_RUN` |

No row is promoted by static evidence alone. Each live row requires timestamp, environment, process/registration/config evidence, trace or receipt identifiers, and a declared result. Production and customer-system rows are outside this matrix.

## Current evidence

- Run `20260901-163846`; canonical branch `bluebrick-assistant-slice1-foundation`; HEAD `cb813c45e6ac3de217765c972ab41784beeb67aa`.
- T4 deployed only to `C:\BlueBrickLab`, registered only under the Lab per-user identity, and created a Lab backup manifest under `C:\BlueBrickLab\backups\20260901-172436`. Mandatory Lab validator checks passed. The validator emitted one non-fatal COM probe cleanup warning; this is not treated as behavioral acceptance.
- Lab DLL: `C:\BlueBrickLab\BlueBrick.Lab.dll`, SHA-256 `32B5271C35D89DB55C5C566B6B905B11C43555B86E21DFBDB606033C5F97D9FE`. Production DLL remained unchanged at SHA-256 `C6C38DF37B5853D8D97D09D0FA7236735A4B6F0933CE7D4923E9C5D269CC10AE`.
- Two responding SOLIDWORKS processes were observed, but no fresh Lab telemetry was produced and no Lab bridge listener was observed on `127.0.0.1:17179`; `127.0.0.1:17178` returned the expected auth-gated response. This proves neither Lab add-in load nor T5 bridge connectivity.
- T6-T9 remain unexecuted. Do not claim `LAB_RUNTIME_CONNECTED`, `LAB_USABLE`, or `LAB_RUNTIME_ACCEPTED` until a user-owned Lab discovery/load action produces fresh Lab bridge/telemetry evidence.
