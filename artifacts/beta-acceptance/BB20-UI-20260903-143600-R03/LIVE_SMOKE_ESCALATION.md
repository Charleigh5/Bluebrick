# BlueBrick R03 Live Smoke Escalation

MODEL_ESCALATION_REQUIRED
SOURCE_MODEL: LUNA
TARGET_MODEL: SOL
TARGET_REASONING: HIGH

TASK_ID: BB20-UI-20260903-143600-R03
USER_INTENT: Determine the first proven runtime divergence behind fallback-shell and obtain React/WebView2 acceptance without production, CAD, PDM, Epicor, database, or registry mutation.
DESIRED_OUTCOME: A corrected, independently verifiable React preview path, or a narrow blocker that tells the next run exactly which seam and approval remain.
LAST_PROVEN_STATE: Current SOLIDWORKS PID 62920 is responding; registered Production CodeBase is C:\BlueBrick\BlueBrick.dll; production frontend triplet is hash-parity with current source/deployed R02 output; authenticated 17178 bridge responds; WebView core is ready but current diagnostics show fallback about:blank.
FIRST_UNRESOLVED_TRANSITION: Effective AgentConfig from the registered production assembly -> React selection and trusted React URI navigation.
EXPECTED: C:\BlueBrick\config\appsettings.json Assistant.UseReactWebView=true and current intended BlueBrick runtime generation should produce the React branch.
OBSERVED: Exact deployed DLL reflection returns UseReactWebView=false, EnableUploads=false, RequireExplicitUploadConsent=false, and AgentBridgePort=17178 while its config file contains true/true/true and 35001. Current live status is fallback-shell. Diagnostics show coreReady=true, about:blank, inline fallback content, rootChildCount=-1, and no React identity marker.
CONTRADICTION: The registered production artifact is an older assembly generation than current source/build and its non-public JSON configuration contract loses the Assistant settings. The current source still lacks explicit binding for Assistant and nested internal properties. The frontend files can match perfectly while the host intentionally selects fallback.
EVIDENCE: R03 FINAL_RECEIPT.md; C:\BlueBrick\BlueBrick.dll SHA-256 C6C38DF37B5853D8D97D09D0FA7236735A4B6F0933CE7D4923E9C5D269CC10AE; current bin\Release DLL SHA-256 2C8A40067C446D14FE47B6ADADC6C9C6DA4862CF4F66F7F8FFCC07F397A75A7B; source config SHA-256 FBAB1714B17CCA824E293167C4D6394951B5FC896626503EC13DA970FCFC9C2B; deployed config SHA-256 2BC0B3E19169ADCC6C59376956B2F5E3B30034235C507DB5EB5CB0E3F93A9091; authenticated status; current WebViewDiagnostics JSON; startup log; registry CodeBase.
CLOSED_INVESTIGATIONS: Current-process stale-state hypothesis as sole cause; port-role misunderstanding; WebView2 core initialization as first failure; frontend production source/deployed hash parity.
FILES_CHANGED: Only this R03 receipt pair was added. No source, DLL, config, registry, CAD, PDM, Epicor, or database file was changed.
COMMANDS_RUN: Read-only Git/process/port/hash/registry/config/log/diagnostic checks; authenticated read-only localhost GETs; isolated reflection of current/deployed assemblies. No build, deployment, restart, attach, or mutation command was run.
ROLLBACK_STATE: R02 frontend backup remains available at C:\BlueBrick\backups\BB20-UI-20260902-165104-R02; manifests and parity were verified previously; restore was not executed.
PERMISSION_BOUNDARY: Do not replace C:\BlueBrick\BlueBrick.dll or production config, change registration, close/restart the current SOLIDWORKS session, or cross CAD/PDM boundaries. The current window has an unsaved-document indicator.
QUESTION_FOR_SOL: Select the smallest safe source seam for explicit AgentConfig binding, require a disk-values-distinct regression test, and define a Lab-only build/registration/runtime proof path that avoids the active unsaved Production session.
RECOMMENDED_NEXT_PROBE: Inspect the Sol-reviewed configuration contract and test it against both source and deployed config shapes; build a distinct Lab artifact; do not deploy or restart until artifact identity, registration ownership, rollback, and unsaved-CAD safety are explicit.

FINAL_STATUS: ROUTING_ESCALATION_REQUIRED
REACT_BOOTSTRAP: NOT_PROVEN
VISUAL_ACCEPTANCE: NOT_PROVEN
PRODUCTION_MUTATION: NONE
CAD_MUTATION: NONE
PDM_MUTATION: NONE
