# BlueBrick Session State — 2026-05-30

## Branch & Build
- Branch: `main`
- Latest commit: `c72814c` (test additions for mode hardening)
- Uncommitted changes in: `Agent/OpenAiAssistantService.cs`, `Agent/AgentConfig.cs`
- Uncommitted changes: RegistryCache, TrimHistory O(n) fix, GetCachedProfiles, HasAgentToken cache, MaxTotalAttachmentBytes, double-norm fix

## Deployed State
- `C:\BlueBrick\BlueBrick.dll` hash: `579AD30A5461D9E8B200E1288C7AAD37CB3052F492160C04935416141A2FF3F9` (matches bin\Debug)
- Deployment verified 2026-05-30, SolidWorks launched, add-in loaded clean
- Bridge on 17178 active, auth-gating (403) correctly
- Startup log: no errors, clean ConnectToSW success

## Completed Work
### Slice A: Mode Resolution Hardening (COMMITTED — b8a29d7, c72814c)
- Fixed ResolveMode real-mode-without-key bug
- Added 4 tests (3 unit + 1 integration)
- All 58 UI tests pass, all 3 relay tests pass

### Performance Optimization (LOCAL/UNCOMMITTED, DEPLOYED)
- H4: TrimHistory O(n) — RemoveRange instead of RemoveAt loop
- H6: RegistryCache — 5s TTL on all Registry.GetValue calls
- M2: GetCachedProfiles — model profiles computed once, cached
- M10: HasAgentToken cache — 5s TTL on token file check
- H3: MaxTotalAttachmentBytes — 10MB default, per-message cap in BuildChatRequestBody
- Bug fix: double-normalization in GetModelProfiles

## Not Yet Started
- Streaming/cancellation contract tests (§9 remaining from spec)
- Bridge helper refactors per PLAN_P0_GATES_AND_BUG_FIXES.md
- Log writer extraction, double-serialize scan
- Cross-file performance fixes (H1/H2 deadlock risks, H7/H8 auth token caching, M3-M9)
- Full AI interface and engineering assistant implementation

## Architecture Notes
- Main add-in: .NET Framework 4.8 (old-style csproj, packages.config)
- Relay: .NET 8.0
- Debug build = production GUID/path/registry ({C56E0AFF-0BD3-4364-90CB-1A581046CD7D})
- Lab build = separate GUID ({251d6df2-3e7b-42ef-b7fc-175e1fdcb4c5}), LAB_BUILD define
- COM CodeBase: C:\BlueBrick\BlueBrick.DLL
- Bridge port: 17178 (production) / 17179 (lab)
- Registry: HKCU\SOFTWARE\ViraInsight\BlueBrick\Settings
- Current registry: AssistantMode=real, AssistantApiKey=nvapi-... present
- AppIdentity has InternalsVisibleTo("BlueBrick.UI.Tests")
- Debug builds do NOT define LAB_BUILD

## Key Files
- Agent/OpenAiAssistantService.cs — main service (modified, uncommitted)
- Agent/AgentConfig.cs — config classes (modified, uncommitted)
- Agent/IAssistantService.cs — service interface
- Agent/AssistantModels.cs — model/message/response contracts
- Agent/AgentPanelClient.cs — streaming client with idle timeout + line buffer
- Agent/AgentHttpServer.cs — bridge server with auth + body-size limit
- AppIdentity.cs — registry root, bridge port, IsLabBuild
- swaddin.cs — SolidWorks add-in entry point
- config/appsettings.json — production config, 3 model profiles
- docs/SPEC_P0_GATES_AND_BUG_FIXES.md
- docs/PLAN_P0_GATES_AND_BUG_FIXES.md

## Verification Commands
dotnet build BlueBrick.sln -c Debug
dotnet test BlueBrick.UI.Tests\bin\Debug\BlueBrick.UI.Tests.dll --test-adapter-path packages\MSTest.TestAdapter.3.1.1\build\net462 --logger "console;verbosity=detailed" --filter "FullyQualifiedName~OpenAiAssistantService"
dotnet test BlueBrick.UI.Tests\bin\Debug\BlueBrick.UI.Tests.dll --test-adapter-path packages\MSTest.TestAdapter.3.1.1\build\net462 --logger "console;verbosity=detailed"
dotnet test BlueBrick.Relay.Tests\BlueBrick.Relay.Tests.csproj -c Debug --logger "console;verbosity=detailed"

## Rules
- Only touch Agent/OpenAiAssistantService.cs and BlueBrick.UI.Tests/LabWorkspaceTests.cs unless compilation requires otherwise
- No code comments in source files
- Do not reopen P0 gates unless new failing test proves regression
- P0_POST_CLOSURE_REGRESSION_HARDENING scope
