# BlueBrick Project OS

## Canonical source

C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick

This is the current canonical BlueBrick source checkout.

## Linked worktrees

C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick-phase2

C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick-phase2-pre-optionA

Linked worktrees are development workspaces, not independent source repositories.

## Runtime targets

Production:
C:\BlueBrick

Lab:
C:\BlueBrickLab

Runtime targets are deployment outputs only. They must never become source-of-truth development directories.

## Recovery

Recovery packages:
C:\VIRA-Recovery\BlueBrick\<RUN_ID>

A recovery package must exist before destructive cleanup or worktree retirement.

## Source authority

1. live canonical source and runtime evidence
2. Git history and worktree state
3. tests
4. governed project receipts
5. remote repository
6. assumptions

## Git rules

- Preserve unrelated dirty work.
- Never use broad `git add .` or `git add -A`.
- Never run `git clean` against an unreviewed dirty worktree.
- Never force-remove a dirty worktree.
- Never reset to origin to solve local drift.
- Do not use stash as durable preservation.
- Changes sharing files with older WIP require patch/hunk-level preservation.

## Worktree rule

One active purpose per worktree.

Before removing a worktree:
1. clean or deliberately preserved;
2. branch backed up;
3. recovery receipt exists;
4. normal `git worktree remove` succeeds without `--force`.

## Build/runtime rule

Source is built from the canonical repository or an explicitly assigned worktree.

C:\BlueBrick and C:\BlueBrickLab must never be directly edited as source.

## Deployment rule

Deployment must be:
1. explicit;
2. target-specific;
3. dry-run-first;
4. hash-receipted;
5. rollback-capable.

No deployment while a blocking P0 remains open.

## Safety boundary

Ordinary development may not mutate:
- PDM;
- SOLIDWORKS production documents;
- customer systems;
- registry/deployed runtime state

unless the active execution packet explicitly authorizes that action.

## Required run start

Before implementation:
1. verify repo root;
2. verify branch;
3. inspect dirty state;
4. inspect worktrees;
5. compare origin;
6. read current task ledger/incident record;
7. establish file ownership.

## Promotion

Static implementation is not proof of runtime acceptance.

Promotion requires current build/test/runtime evidence and zero blocking P0 findings.
