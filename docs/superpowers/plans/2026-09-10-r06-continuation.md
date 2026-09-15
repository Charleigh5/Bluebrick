# R06 Provider and Publication Implementation Plan

> For agentic workers: use subagent-driven-development, with a fresh implementer followed by spec review and code-quality review. Primary agent owns integration and Git publication.

Goal: add OpenRouter provider-only, finish and publish reviewed canonical BlueBrick source, preserve divergent work, and establish an honest R06 LAB acceptance boundary.

Architecture: SharedAI remains a static catalog plus native deterministic routers. NVIDIA and Gemini remain the only model records and routes. OpenRouter is a third provider record, with no model or automatic inference until explicitly selected by the user.

Tech stack: .NET Framework 4.8, Visual Studio MSBuild/VSTest, TypeScript, existing Node/AJV, Windows Computer Use @oai/sky.

Spec: current user request and prior R06 SharedAI specification; user chose provider-only on 2026-09-10.

## Constraints

Preserve frozen R04 and separate R05 inference from R06 routing. No production deployment, secrets in Git/evidence, PDM login automation or forced worktree cleanup. User explicitly authorized commit/push. Existing source is dirty; primary agent preserves and publishes only reviewed paths. New source changes use an isolated temporary worktree; no concurrent coupled writers. User explicitly selected finish and merge all older subsystems into R06. Preserve unfinished status until each subsystem passes its gates.

## Task 1: Preserve source and diagnose native input

- [x] Inspect canonical root/branch/status, linked trees, GitHub remote and authentication.
- [x] Create fresh all-refs bundle, separate index/worktree patches and hash-verified non-secret-path copies in C:\VIRA-Recovery\BlueBrick\20260910-R06-continuation.
- [x] Initialize supported @oai/sky; list_windows returns PDM login but no SOLIDWORKS. Process check returns no SLDWORKS. Do not touch authentication.
- [x] Open Windows environment editor; user owns key entry. Presence is distinct from visibility and successful authentication.

## Task 2: OpenRouter provider-only

Files: SharedAI/catalog/providers.json; SharedAI/generated/csharp/SharedCatalog.cs; SharedAI/generated/typescript/sharedAI.ts; SharedAI/schema/build.cjs and regenerated provider/model/inference schemas where relevant; SharedAI/tests/typescript.test.cjs; BlueBrick.UI.Tests/Agent/SharedAiTests.cs; SharedAI/README.md.

Interface: existing Provider {id,protocol,baseUrl,credentialBinding}. Add {id:openrouter, protocol:openai-chat-completions, baseUrl:https://openrouter.ai/api/v1, credentialBinding:OPENROUTER_API_KEY}.

- [x] Add a failing regression that expects exactly three supported providers but exactly two model records, unchanged routes, and rejects unapproved OpenRouter endpoint or key binding.
- [x] Extend C#/TS/schema allowlists without permitting arbitrary providers, duplicate identities, credentials or invented models. Keep model-provider identity rules for NVIDIA/Gemini.
- [x] Run isolated strict TS compile/tests and schema validation; run focused C# tests after integration with existing Visual Studio tooling.
- [x] Spec reviewer verifies provider-only scope and unchanged routing; quality reviewer verifies validation parity and regressions. Fix findings before integration.

## Task 3: Finish canonical source and publish

- [ ] Review the complete existing canonical diff and new untracked source, not generated build binaries or credential files.
- [ ] Run relevant existing frontend, legacy test, relay and source/runtime-identity checks; fix concrete failures with scoped implementer tasks and ordered reviews.
- [ ] Secret-scan the exact proposed staged text paths without echoing matched values. Explicitly stage only reviewed source/docs/evidence paths.
- [ ] Commit coherent changes and push the existing canonical feature branch to the observed Charleigh5/Bluebrick origin; verify remote SHA. No merge to production/default branch implied.

## Task 4: Retire divergent work safely

- [ ] Reconcile phase2/pre-optionA source and evidence inventories, unique commits and current incomplete feature receipts.
- [ ] Finish and verify each unique phase2/pre-optionA subsystem in dependency order, reconcile overlapping code with canonical safety/SharedAI contracts, then merge reviewed source. Do not substitute archiving for requested completion.
- [ ] Remove linked worktrees only after full preservation, reviewed path disposition and successful ordinary non-force removal. Keep any unresolved tree with an exact blocker rather than discard data.

## Task 5: Remaining R06 acceptance

- [ ] Recheck AionUI integration conflicts and prepare a bounded read-only reconciliation seam.
- [ ] Prepare deployable LAB candidate with catalog/config/runtime hashes and rollback; request exact LAB approval only when candidate is reviewable and permitted prerequisites are satisfied.
- [ ] Test providers separately, then controlled provider-only fallback, vision, tool continuation, streaming and structured output, preserving per-boundary receipts.
- [ ] Use returned native SOLIDWORKS window and observed controls for a real UI action; stop on PDM authentication/modal or unsupported input. Do not patch BlueBrick to compensate for tool failures.
- [ ] Independent final acceptance review. Enterprise readiness requires observed applicable runtime gates; static builds alone do not establish it.

## Expanded integration order (user approved finishing all subsystems)

1. Finish OpenRouter provider-only reviews; fix schema/model association parity before integration.
2. Fix R06 catalog deployment/rollback/hash contract and observed frontend build identity; review before any LAB packaging.
3. Integrate Vira.Next.Contracts, Vira.Next.Engine and tests: policy/router/fake host, comparisons, packet/CAD/offline resolver, hardware binding. Use synthetic fixtures; no live network/CAD.
4. Integrate PacketWorker core/tests, then legacy peer hosts and bounded generator facade. Repair SolidWorksHost x64-to-AnyCPU ProjectReference mapping without retargeting the add-in. Build peers, do not execute CAD/PDM hosts without exact approval.
5. Reconcile connector/context/dispatcher and document-generator cancellation into canonical Agent/UI/project files. Keep SharedAI, consent, ingress and mutation boundaries intact.
6. Integrate execution-board relay route/contracts and only differing frontend deltas. Most older feature frontend modules already exist byte-identically; no wholesale overwrite.
7. Reconcile pre-optionA residual intent/test hunks and record each as integrated, equivalent or intentionally rejected with reason. Do not discard differences based on age.
8. Run integrated release checks and spec/quality reviews; explicitly stage and publish reviewed non-sensitive source. Retire extra worktrees only after merged results and verified preservation.
9. Complete separately approved live LAB/provider/AionUI acceptance. Production readiness remains unverified until all applicable runtime gates pass.

Observed canonical baseline on continuation: legacy 305 passed / 1 skipped; relay 4 passed; frontend typecheck, transport, activation and file-scheme responsive rendering passed. No SOLIDWORKS process was running; @oai/sky initialized/listed windows successfully but only PDM login appeared for the CAD environment. Environment editor launch requested; user owns secret entry.
