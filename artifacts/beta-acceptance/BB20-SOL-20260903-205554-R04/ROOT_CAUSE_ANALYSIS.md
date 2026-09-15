# Root Cause Analysis

Framework: `BB20-SOL-CONFIG-LAB-ACCEPTANCE-v1.0`  
Run: `BB20-SOL-20260903-205554-R04`

## Confirmed source/runtime cause

Newtonsoft.Json was deserializing internal configuration properties. `Agent.BridgePort` was explicitly annotated, while the top-level `Assistant` property and its critical members were not. The pre-repair representative test therefore preserved `BridgePort=35001` but observed `UseReactWebView=false`, matching the exact deployed-assembly reflection result and production `fallback-shell` behavior.

The repair adds deliberate mappings for the top-level configuration sections and Assistant fields, schema/type validation, and a non-secret configuration load receipt. Exact post-repair Lab-assembly reflection proved:

- `BridgePort=17179`
- `UseReactWebView=true`
- `EnableUploads=true`
- `RequireExplicitUploadConsent=true`
- `ConfigurationLoadStatus=CONFIG_PRESENT_VALID`
- `ConfigSchemaVersion=1`

## Generation seam

The Lab host now requires a deployment manifest whose target/schema/build IDs and SHA-256 values match the loaded DLL, effective config, and exact frontend triplet. A mismatch fails before React navigation as `RUNTIME_GENERATION_MISMATCH`. React bootstrap additionally requires `data-bluebrick-app-mounted=true` and records frontend build/source identity.

## First remaining live failure

`SOLIDWORKS Lab process -> Lab add-in discovery/load` failed. A distinct responding process (`PID 63828`) started, HKCU Lab registration and CodeBase validation passed, but module enumeration found zero BlueBrick modules, port `17179` never listened, and no Lab WebView diagnostics were created. Native Computer Use returned no targetable apps, so the Add-Ins dialog could not be inspected safely. No SendKeys, ad hoc COM attachment, HKLM/UAC, or production mutation was attempted.

