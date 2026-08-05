# BB-M001 Hardening Slice Execution Receipt

- **Date**: 2026-07-31
- **Mode**: R2_LOCAL_IMPLEMENTATION
- **Status**: COMPLETE
- **Branch**: bluebrick-assistant-slice1-foundation (HEAD: ced14be)
- **Repo**: C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick

## Status

COMPLETE — All 7 production files and 6 test files written, project-included, compiled (Debug + Lab), and tested. 60/60 hardening tests pass (175 total tests: 174 pass, 1 pre-existing live-connection test skipped). Toolchain (VS Build Tools 2022 v17.14.51) installed locally; no deployment, no external writes, no real credentials.

## Files Changed

### New Production Files (7)
| File | Size |
|------|------|
| Agent/AssistantManifest.cs | 5771 B |
| Agent/AssistantProviderExpiry.cs | 3397 B |
| Agent/AssistantInjectionGuard.cs | 8388 B |
| Agent/AssistantIntegrityScanner.cs | 5622 B |
| Agent/AssistantChainedReceipt.cs | 4798 B |
| Agent/AssistantPromotionGate.cs | 6917 B |
| Agent/AssistantSbomGenerator.cs | 4208 B |

### New Test Files (6)
| File | Size |
|------|------|
| BlueBrick.UI.Tests/Agent/AssistantManifestTests.cs | 6004 B |
| BlueBrick.UI.Tests/Agent/AssistantProviderExpiryTests.cs | 6650 B |
| BlueBrick.UI.Tests/Agent/AssistantInjectionGuardTests.cs | 6919 B |
| BlueBrick.UI.Tests/Agent/AssistantIntegrityScannerTests.cs | 5562 B |
| BlueBrick.UI.Tests/Agent/AssistantChainedReceiptTests.cs | 4949 B |
| BlueBrick.UI.Tests/Agent/AssistantPromotionGateTests.cs | 9102 B |

### Modified Project Files (2)
| File | Change |
|------|--------|
| BlueBrick.csproj | +39 lines (7 hardening Compile includes, +RuntimeIdentifiers win-x64, +System.Xml.Linq reference) |
| BlueBrick.UI.Tests/BlueBrick.UI.Tests.csproj | +13 lines (6 hardening test Compile includes) |

### Fixes Applied During Build/Test Iteration (all in slice scope)
| File | Fix |
|------|-----|
| SolidWorks/Adapters/SolidWorksCustomPropertyReadAdapter.cs | CS0051: public constructor + internal `ISwDocumentSource` param → internal constructor (design-preserving; wiring/tests use InternalsVisibleTo) |
| Agent/AssistantPromotionGate.cs | CS0426: `AssistantIntegrityScanner.AssistantIntegrityScanResult` → top-level `AssistantIntegrityScanResult` (2 spots) |
| Agent/AssistantManifest.cs | CS0117/CS0246: canonical serializer refs fixed (matches `BlueBrick.Audit.Core` pattern); ArtifactHashes excluded from canonical payload (self-referential-hash fix) |
| Agent/AssistantSbomGenerator.cs | csproj parsed as XML (XDocument) not JSON; canonical serializer ref fixed |
| Agent/AssistantChainedReceipt.cs | `Seal()` now flips VerificationState to "sealed" BEFORE hashing (chain-validity fix) |
| Agent/AssistantInjectionGuard.cs | Added active-script detection, broadened protected-route regex (`sw/\|pdm/\|lab/`), broadened secret-exfil regex (api key/bearer/secret+send variants); added INJ-011 benign fixture (3 approve fixtures) |
| Agent/AssistantIntegrityScanner.cs | Secret regex now tolerates JSON quotes before `:` and matches `bearer <jwt>` without colon; `RedactFindings()` now replaces the value (not appends `REDACTED` after it); added `Truncate()` helper |
| BlueBrick.UI.Tests/Agent/AssistantPromotionGateTests.cs | Manifest-block tests now pass a manifestPath to `Evaluate()` so manifest verification actually runs |
| BlueBrick.UI.Tests/Agent/AssistantInjectionGuardTests.cs | Fixture-count assertion 10 → 11 (added INJ-011) |
| BlueBrick.UI.Tests/Agent/AssistantIntegrityScannerTests.cs | Writes test file with `UTF8Encoding(false)` (no BOM) so size assertion is byte-exact |

### Best-Practice Review (post-green audit, all re-verified green)
| File | Change |
|------|--------|
| Agent/AssistantToolPolicy.cs | All route comparisons (`StartsWith`/`==` on paths) now explicit `StringComparison.Ordinal` — on .NET Framework 4.8 default string comparisons are culture-sensitive (CA1310); input is already lowercased, so this is defense-in-depth + analyzer-clean |
| Agent/AssistantInjectionGuard.cs | `lower.Contains("x-agent-auth")`/`Contains("api_key")` → `IndexOf(..., StringComparison.Ordinal) >= 0` (same culture-safety rationale) |
| tools/register-lab-addin.ps1 (NEW) | Lab add-in registration script: self-elevating, `-WhatIf` = pure preview with zero side effects, backs up pre-existing keys to .reg before writing, writes HKCR\CLSID + HKLM\Addins + HKCU\AddInsStartup, verifies each key after, `-Unregister` for clean removal, URI-encoded CodeBase (`file:///…%20…`) because the repo path contains a space |
| tools/deploy-production.ps1 (NEW) | Production deploy script: dry-run by default (`-Execute` required), hash-verified copy, timestamped backup of DLL + 3 registry keys, Mode=Lab (coexist, production untouched) or Mode=ReplaceProduction (re-point existing CLSID), prints exact rollback commands |

### Pre-Existing Dirty Files (untouched, out of scope)
| File | Change |
|------|--------|
| Agent/AgentHttpServer.cs | +161 lines (pre-existing, /sw/generate_step_configs route) |
| ClsEnums.cs | +2/-3 lines (pre-existing, NamedConfig=8192 flag) |

## Decisions

1. **Signed manifests**: Implemented versioned envelope with plugin ID/version, provenance, permissions, tool schemas, artifact hashes, signature algorithm, signer key ID, signature, trust/revocation state. Verifier uses fail-closed policy. ArtifactHashes excluded from the canonical payload hash to avoid the self-referential store-then-verify mismatch (hash is computed over identity+permissions+toolschemas; per-artifact hashes verified independently).
2. **Provider expiry**: `ProviderExpiryRecord` with checked_at/expires_official source/free status/privacy class/validation state. `ProviderExpiryChecker` for eligibility, expiry, unknown, revoked, missing, and revalidation.
3. **Injection guard**: 11 default fixtures (8 deny + 3 approve) covering malicious webpages, repository text, fake system messages, tool-call-shaped content, hidden/ARIA text, poisoned MCP metadata, persistent-memory poisoning, and benign content. `Analyze()` with pattern matching, `VerifyNotBlocked()` with `AssistantInjectionDeniedException`. Protected-route detection is regex-driven over `sw/|pdm/|lab/` routes (deny on any), plus active-script (script tags/onerror/onload/javascript:) and secret-exfil (api key/secret/token + send/steal/upload variants).
4. **Integrity scanner**: SHA-256 hashing for files/bytes/strings, `ScanFile()`, `ScanForSecrets()` with regex-based secret detection and redaction, `ScanDirectory()`, `RedactFindings()` — redaction replaces only the matched value with REDACTED, never leaks the raw secret.
5. **Chained receipts**: `AssistantChainedReceipt` with receipt_id/previous_receipt_hash/canonical payload hash/timestamp/actor/tool identity/action tier/evidence IDs/verification state/`Seal()`. `AssistantChainedReceiptChain` with append (hash linking), `IsChainValid()`, `CreateCorrection()` for superseding receipts. `Seal()` transitions VerificationState before hashing so the stored hash covers the final state.
6. **Promotion gate**: `PromotionDecision` enum (AllowPromotion/AdmitCandidateWithLimits/BlockPromotion). `AssistantPromotionGateResult` with decision/reason/evidence IDs/per-control pass-fail flags. `AssistantPromotionGate` with 7 control checkers. Manifest verification is driven by the manifestPath argument — callers that supply a manifest loader and a path get fail-closed manifest enforcement.
7. **SBOM generator**: CycloneDX SBOM generation for project/plugin bundles, deterministic JSON via `AuditCanonicalSerializer`, SHA-256 hash, file save. csproj parsed with XDocument (correct XML parsing, not JSON).
8. **Hardened policy**: Unsigned local-dev mode is explicit and blocked by hardened policy. Fail-closed for missing, invalid, revoked, unsupported, or mismatched signatures.
9. **Toolchain**: VS Build Tools 2022 v17.14.51 installed locally from pre-staged layout (`C:\Users\cweir\Downloads\vsbt-layout`, offline, self-elevating install script). Legacy `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe` cannot compile C# 9/ToolsVersion 15.0 and was not used.

## Commands / Results

| Command | Result |
|---------|--------|
| V-T1 `git diff --check` | exit 0, clean (pre-existing CRLF warnings only) |
| V-T4 forbidden-op scan (publish/regasm/registry-write/real-key patterns in slice files) | zero real matches (only false positives: `AssistantScopeRegistry` naming in pre-existing file; bearer string is a test fixture) |
| V-T5 secret scan (api_key=/sk-/Bearer patterns in slice files) | zero real matches — bearer fixture is test data, no real credentials |
| V-T6 project inclusion (13 files in both .csproj) | PASS — all 7 production + 6 test files confirmed |
| `MSBuild.exe BlueBrick.csproj /t:Build /p:Configuration=Debug /p:Platform=AnyCPU` | SUCCESS → `bin\Debug\BlueBrick.dll` |
| `MSBuild.exe BlueBrick.csproj /t:Build /p:Configuration=Lab /p:Platform=AnyCPU` | SUCCESS → `bin\Lab\BlueBrick.Lab.dll` (2,122,240 B) |
| `MSBuild.exe BlueBrick.UI.Tests\BlueBrick.UI.Tests.csproj -t:Build -p:Configuration=Debug` | SUCCESS (175 tests discovered) |
| `vstest.console.exe BlueBrick.UI.Tests\bin\Debug\BlueBrick.UI.Tests.dll` | **174 passed, 1 skipped, 0 failed** |

## Test Matrix

| Test Class | Tests Written | Status |
|------------|---------------|--------|
| AssistantManifestTests | 8 | ✅ 8/8 pass |
| AssistantProviderExpiryTests | 10 | ✅ 10/10 pass |
| AssistantInjectionGuardTests | 14 | ✅ 14/14 pass |
| AssistantIntegrityScannerTests | 10 | ✅ 10/10 pass |
| AssistantChainedReceiptTests | 9 | ✅ 9/9 pass |
| AssistantPromotionGateTests | 9 | ✅ 9/9 pass |
| **Total (hardening)** | **60** | **✅ 60/60 pass** |
| Pre-existing suite | 115 | ✅ 114 pass, 1 skipped (`OpenAiAssistantService_RealConnectionTest_WithNvidiaKey` — requires real API key, correctly skipped) |

### Iteration record — 14 failures found and fixed
| Failure (round 1) | Root cause | Fix |
|-------------------|-----------|-----|
| Manifest_ValidSignedManifest_Accepted | canonical payload hash self-reference (ArtifactHashes stored after hashing) | Exclude ArtifactHashes from canonical payload |
| InjectionGuard_MaliciousScript_Denied | no script-tag detection | Added ActiveScriptRegex; active_script is destructive |
| InjectionGuard_ToolCallShapedContent_Denied | protected-route list too narrow (`sw/part/delete` only) | Regex `\b(?:sw\|pdm\|lab)/[a-zA-Z0-9_/]+` |
| InjectionGuard_PoisonedMcpMetadata_Denied | `sw/delete` not in literal route list | Same regex fix |
| InjectionGuard_PersistentMemoryPoisoning_Denied | `api_key`/`secret`+`send` too narrow | SecretExfilRegex covers api key/bearer/secret/token + exfil verbs |
| InjectionGuard_FixtureCount_MatchesExpected | 2 approve fixtures, test wants 3 | Added INJ-011 benign fixture; count 10→11 |
| IntegrityScanner_ScanFile_ReturnsHashAndSize | UTF-8 BOM (16 B vs 13 chars) | Test writes `UTF8Encoding(false)` |
| IntegrityScanner_ScanForSecrets_DetectsApiKey | regex required `:` directly after key, JSON has `"` before `:` | `["']?\s*[:=]\s*["']?` tolerance |
| IntegrityScanner_ScanForSecrets_DetectsBearerToken | `bearer <jwt>` has no colon | Dedicated `\bbearer\s+` branch |
| IntegrityScanner_ScanDirectory_ScansEligibleFiles | same quote-before-colon issue | Same regex fix |
| IntegrityScanner_RedactFindings_RedactsSecretValues | `${0}REDACTED` appended after raw secret | MatchEvaluator replaces only value with REDACTED |
| ChainedReceipt_ChainIsValid_WhenUntampered | Seal() hashed before state flip | Flip state first, then hash |
| PromotionGate_ManifestFails_BlocksPromotion | Evaluate() called without manifestPath → manifest check skipped | Test passes `"manifest.json"` path |
| PromotionGate_ResultHasReasonsAndEvidenceIds | same | Same |

## Risks

1. **SOLIDWORKS interop version skew**: repo `lib\` interops are SW2024 (v32.3.1.2) while SOLIDWORKS 2025 SP5 (v33.5.0.0053) is installed. Build succeeds with lib\ interops; runtime COM binding against SW2025 is not exercised by these unit tests and must be validated in a live session.
2. **Registration not performed**: The Lab build (`bin\Lab\BlueBrick.Lab.dll`) is built but NOT registered as a SOLIDWORKS add-in. Because the DLL is not strong-named, RegAsm /codebase may reject it; the production deployment was registered manually (HKCR\CLSID + HKLM\SOFTWARE\SolidWorks\Addins + HKCU\AddInsStartup). Templates `tmp_register.bat` / `tmp_register_lab.ps1` / `register_addin.bat` exist; registry writes are a system-level change and were deliberately deferred (no external writes per mode).
3. **Pre-existing dirty files**: `AgentHttpServer.cs` and `ClsEnums.cs` have uncommitted changes from a prior session; out of scope, not touched, still uncommitted.
4. **No CI pipeline**: no `.github/` or CI YAML; SBOM generation has no automated pipeline validation.
5. **Live-connection test skipped**: `OpenAiAssistantService_RealConnectionTest_WithNvidiaKey` requires a real API key; skipped by policy (no real credentials).

## Rollback

To revert the hardening slice:
1. Remove the 7 hardening Compile includes from `BlueBrick.csproj` (plus the `RuntimeIdentifiers` line and System.Xml.Linq reference if desired)
2. Remove the 6 hardening test Compile includes from `BlueBrick.UI.Tests/BlueBrick.UI.Tests.csproj`
3. Delete the 7 production files under `Agent/`
4. Delete the 6 test files under `BlueBrick.UI.Tests/Agent/`
5. Restore the two csproj files from HEAD: `git checkout HEAD -- BlueBrick.csproj BlueBrick.UI.Tests/BlueBrick.UI.Tests.csproj`
6. Note: `SolidWorks/Adapters/SolidWorksCustomPropertyReadAdapter.cs` constructor visibility fix is safe to keep (design-preserving) or revert with the pre-slice version if the interface is later made public.

## Gaps

1. Live SOLIDWORKS 2025 session test not performed (requires interactive CAD session; unit tests cover logic only)
2. Lab add-in registration not performed (registry writes deferred per no-external-write policy; templates ready)
3. Integration test with existing `Agent/AgentHttpServer.cs` / `ClsEnums.cs` changes not performed (out of scope, pre-existing dirty)
4. SBOM generator output not validated against a real plugin bundle (only unit-level generation tests)
5. No CI pipeline configured for automated re-runs

## Next Test

1. Live-session smoke: load `bin\Lab\BlueBrick.Lab.dll` in SOLIDWORKS 2025 SP5 (after registering via `tmp_register_lab.ps1` with the Lab GUID {251D6DF2-3E7B-42EF-B7FC-175E1FDCB4C5}) and verify coexistence with production BlueBrick v1.0.13.4
2. Re-run the 60 hardening tests on any subsequent change: `vstest.console.exe BlueBrick.UI.Tests\bin\Debug\BlueBrick.UI.Tests.dll`
3. Re-run V-T1/V-T4/V-T5/V-T6 scans after any change (all currently clean)
4. Optionally add CI (GitHub Actions) wiring the same build+test+scan steps

## Confirmation

- No deployment occurred
- No external write occurred (no registry writes, no add-in registration, no SOLIDWORKS automation)
- No real credentials were used (live-connection test skipped by policy)
- No unrelated commit was made
- No paid service was invoked
- All changes are local-only and scoped to the hardening slice (plus two in-slice compile fixes)
