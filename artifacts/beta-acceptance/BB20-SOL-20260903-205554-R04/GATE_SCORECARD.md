# Gate Scorecard

| Gate | Result | Evidence |
|---|---|---|
| G0 baseline | PASS | Canonical repo/branch/SHA, dirty state, DLL/config/frontend hashes, PID/ports, registration, production status and diagnostics captured. |
| G1 defect reproduction | PASS | Representative focused test failed at `UseReactWebView` before repair. |
| G2 config contract | PASS | Explicit binding, type/schema validation, load diagnostics, focused tests. |
| G3 regression lock | PASS | 20 config/generation tests; full suite 275 passed, 1 credential-gated skip. |
| G4 generation guard | PASS | Match/mismatch tests plus deployed `RUNTIME_GENERATION_MATCH`. |
| G5 clean Lab build | PASS WITH WARNING | Rebuild passed; existing compiler warnings and Vite chunk-size warning remain. |
| G6 Lab deployment | PASS | Built/deployed DLL, config, index, CSS and JS hashes match; production hashes unchanged. |
| G7A owned process | PASS | Separate responding PID `63828`, started 2026-09-03 20:57:29 EDT. |
| G7B loaded Lab DLL | FAIL | Zero BlueBrick modules in PID `63828`. |
| G7C-G7M | BLOCKED | No Lab bridge, WebView, DOM, screenshot, interaction, or telemetry can exist before add-in load. |

## Weighted score

| Domain | Earned / Weight |
|---|---:|
| Configuration contract | 15 / 15 |
| Regression coverage | 10 / 10 |
| Runtime generation identity | 15 / 15 |
| Clean Lab build | 10 / 10 |
| Deployment integrity | 10 / 10 |
| Effective runtime config | 0 / 10 |
| WebView/navigation | 0 / 10 |
| React DOM/mount | 0 / 10 |
| Bridge/interaction | 0 / 5 |
| Visual + telemetry | 0 / 5 |
| **Total** | **60 / 100** |

Critical runtime gates override the score. Promotion is not accepted.

