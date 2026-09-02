# BlueBrick 2.0 Beta Test Matrix

Run IDs: `20260902-091551` → identity absent; `20260902-093418` → env metadata not embedded; `20260902-095413` → corrected static bundle, Lab launch blocked at add-in activation.

| Lane | Main path | Failure path | Regression path | Current result |
|---|---|---|---|---|
| Frontend identity | Build with explicit Lab metadata and inspect bundle | Missing/unknown metadata must remain `UNKNOWN` | Verify all four surfaces mount one marker | Static PASS; live UNKNOWN |
| Frontend transport | 17 callback contract and replay tests | malformed SSE/error state | two-round transcript ordering | PASS static |
| API | host-mediated `/message/stream`, `/assistant/tool` contracts | 400/404/500/SSE error classifications | no direct browser secret/egress | PASS static; live NOT_RUN |
| Database | local vault/relay fixture boundaries | unavailable dependency | isolated temporary relay DB | PASS static; legacy business DB BLOCKED_EXTERNAL |
| PDM | read-only direct PDM path | disabled/not authenticated | no write operations | BLOCKED_EXTERNAL |
| Epicor | bounded parameterized part query | disabled/unavailable connector | legacy direct SQL path not substituted | BLOCKED_EXTERNAL; broader parity DEFERRED |
| SOLIDWORKS | Lab add-in and real document context | no owned live host / no bridge | preserve dirty user session | registration PASS; activation BLOCKED |
| Rollback | exact Lab backup restore | failed deployment auto-rollback | Production hashes unchanged | PASS Lab-only rollback |

## Stop rules

Do not promote from STATE 0 on static tests, screenshots, HTTP health, add-in registration, or assistant chat. PDM/Epicor writes, Production changes, unsaved-user-document mutation, and irreversible CAD changes remain deferred.
