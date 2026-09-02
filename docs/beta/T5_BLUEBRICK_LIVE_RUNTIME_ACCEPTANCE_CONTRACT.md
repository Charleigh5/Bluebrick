# BlueBrick 2.0 Live Runtime Acceptance Contract

Run mode: `LIVE_BETA_ACCEPTANCE`, `LAB_ONLY`, `LEGACY_PARITY_FIRST`, `RUNTIME_EVIDENCE_REQUIRED`.

## Acceptance boundary

The current React bundle is the intended BlueBrick 2.0 frontend only when the source entrypoint, build identity, packaged asset triplet, Lab DLL/config identity, loaded WebView URI, and observed host/backend agree. Static source, build, registration, or HTTP evidence cannot promote a live behavior row to `PASS`.

The existing Production root (`C:\BlueBrick`) and the existing SOLIDWORKS session containing an unsaved assembly are protected. Lab-only registration, deployment, and rollback are allowed; Production writes, PDM writes, Epicor business-data writes, irreversible CAD changes, credential changes, and forced termination of the user session are not allowed.

## Evidence ladder

| State | Meaning | Evidence required |
|---|---|---|
| STATE 0 | `NOT_LIVE` | Static/source/package evidence only |
| STATE 1 | `LIVE_IDENTITY_PROVEN` | Correct Lab DLL/config and React bundle observed inside a live SOLIDWORKS host |
| STATE 2 | `UI_BETA_READY` | State 1 plus real SOLIDWORKS context and one original capability vertical slice |
| STATE 3 | `INTEGRATION_BETA_READY` | Runtime-proven or genuinely external-blocked database, PDM, Epicor, and core legacy integration |
| STATE 4 | `BLUEBRICK_V2_BETA_READY` | All critical gates, failure paths, rollback, and Production isolation pass with no P0/P1 regressions |

## Required row fields

Every acceptance row records the capability, expected behavior, source/runtime paths, frontend/backend build identities, dependency, test input class, method, evidence, result, failure code, repair commit, run ID, and notes. `PROVEN_STATIC`, `UNKNOWN`, `BLOCKED_EXTERNAL`, `NOT_INDUCED_SAFETY_BOUNDARY`, and `DEFERRED_REQUIRES_APPROVAL` are not runtime `PASS`.

## Current run ceiling

Run `20260902-095413` embedded and packaged the requested Lab identity, passed the static ladder, staged and registered the Lab build, and requested a separate SOLIDWORKS process. The new process became responsive but did not load `BlueBrick.Lab.dll`, did not open port `17179`, and produced no fresh WebView telemetry. The run therefore remains `STATE 0 NOT_LIVE`; the failed Lab deployment was rolled back from its exact backup. The original dirty SOLIDWORKS process was not terminated or attached.
