# BlueBrick Lab Approval-Lifecycle Smoke Checklist (`solidworks.set_custom_property`)

> LIVE DISCIPLINE: every step below marked **[live]** runs ONLY on the coordinator's explicit go message for a pre-announced RunId. No live SOLIDWORKS contact, no Lab/package-regenerating build, and no live smoke execution without both. Until performed, installed-interop admission, native owner/modal UI, persistence, rollback, and live acceptance remain `NOT_VERIFIED`. Record the RunId here before starting: `RunId: __________`.

Ticket: `artifacts/tickets/approval-lifecycle-tests` (epic `55ce171d-262f-4963-9447-0600bfb14ba4`). Contract: Sprint 05 v1 (SHA `7C7E5DA8…`). This checklist is the hand-run half of ticket §2; the Debug suite is the other half.

## 0. Prerequisites (Debug-verified first)

- [ ] Debug gates green on this checkout: `BlueBrick.UI.Tests` (full), `BlueBrick.Relay.Tests`, `Vira.Next.Engine.Tests`; `git diff --check` clean.
- [ ] Lab build registered and loaded by SOLIDWORKS (see `BlueBrick.Lab.Relay.Validation.Runbook.md` for the two-process loop).
- [ ] Lab config has `Assistant.Mutations.Enabled=true` (`config/appsettings.lab.json`).
- [ ] Test root exists and is writable; it contains a clean throwaway `smoke.sldprt` with an existing editable literal-text document-level property `Description = smoke-old`, and NO same-name override in any configuration.
- [ ] Checkpoint/restore path pre-verified in Debug (restart-matrix + restore rows green).

## 1. Reader proofs through installed interop [live]

- [ ] PASS / FAIL — concrete reader reports native text type for `Description` (not inferred).
- [ ] PASS / FAIL — literal value reads `smoke-old`, raw == resolved, status editable, dirty=false, read-only=false.
- [ ] PASS / FAIL — dirty/read-only query failures would deny (concrete lever: briefly toggle the scratch file read-only and watch the reader report it; restore writability immediately afterwards).
- [ ] PASS / FAIL — absent property and a non-text property deny (probe on a scratch copy if needed; never on the seed).

## 2. Happy path [live]

- [ ] PASS / FAIL — model proposes the literal update; a FRESH native dialog opens naming the active test file, showing typed old→new literal, stating it will save.
- [ ] PASS / FAIL — Approve → post-checkpoint start decision observed; `consumed` ledger event; checkpoint hash equals pre-write baseline.
- [ ] PASS / FAIL — ordinary save result; retained-document clean check before close; persisted tool-audit/Traces receipt with `Mode=HUMAN_APPROVED_MUTATION`, correct approval ID, `MutationCount=1`.
- [ ] PASS / FAIL — same file reopened from disk shows the new literal; journal reaches `evidence_complete`.

## 3. Negative paths [live]

- [ ] PASS / FAIL — Deny: no change, `denied` receipt, ledger `denied` entry.
- [ ] PASS / FAIL — Timeout: auto-deny at `ApprovalTimeoutSeconds`, `expired` ledger entry, no write.
- [ ] PASS / FAIL — Unsafe target (outside-root path): denies pre-dialog, no prompt, no write.
- [ ] PASS / FAIL — Sequential: two admitted requests on one issuer open two fresh dialogs and both apply; deny→new-request and timeout→new-request apply on fresh prompts.
- [ ] PASS / FAIL — Config off (`Enabled=false`): fail-closed `disabled`, no prompt, no write; restore `Enabled=true` afterwards.

## 4. Owner/modal + Production posture [live]

- [ ] PASS / FAIL — actual SOLIDWORKS owner/modal behavior observed and recorded (separate from the accepted centered/topmost substitute).
- [ ] PASS / FAIL — Production-build posture unchanged (no Lab-only affordance leaks; `HandleChatGptConfirm` still hard-403 spot-check).

## 5. Failure injection, controlled and recoverable [live]

Run only with the scratch file recoverable via checkpoint/restore; stop on any unrecoverable state and escalate.

- [ ] PASS / FAIL — save/final-evidence failure path exercised per checklist direction (concrete lever: rename the audit root mid-run to force the evidence-incomplete path, then restore the name before proceeding); `saved_verified / evidence_incomplete` + target block observed where applicable.
- [ ] PASS / FAIL — directed manual restore (or reconciliation) performed and verified: original value/hash (restore) or accepted saved value + completed evidence (reconciliation).

## 6. Evidence + sign-off

Record per step: host/listener log excerpt, SOLIDWORKS version, UI observation (dialog text/screenshot reference), file hashes (before/after), execution-journal stages, receipt ID. Attach the per-step PASS/FAIL table to the round evidence. Sign-off requires every box above checked PASS (or an explicit coordinator waiver per box).
