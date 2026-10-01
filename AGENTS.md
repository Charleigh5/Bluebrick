# BlueBrick source checkout instructions

This is the canonical BlueBrick source checkout.

- Canonical physical checkout: C:/Users/cweir/Documents/GitHub/VIRA GITHUB/Bluebrick
- Current project hub: C:/Users/cweir/Documents/ChatGPT/Bluebrick AI Assistant
- Hub access alias: C:/Users/cweir/Documents/ChatGPT/Bluebrick AI Assistant/Bluebrick
- The hub alias and this checkout resolve to the same Git working tree. Do not treat them as separate repositories or copy the repository again.
- The hub has its own empty, unborn .git directory. Run Git commands in this checkout or through its hub alias, never from the hub root.

On 2026-09-29 a low-disruption consolidation was performed. Windows would not rename the open hub folder, so the source checkout stayed here and a junction was created in the hub. Relevant agent instructions, the folder map, and a consolidation receipt are in the hub root. Two relevant Traycer evidence folders were copied to hub/project-records/traycer; the originals remain authoritative.

Preserve the dirty worktree state present at consolidation. At that time branch bluebrick-assistant-slice1-foundation was at 057b95cdeea6aa90b3303b5eed7f89e9a3c30d53 with seven modified tracked files and one pre-existing untracked checklist. No runtime or linked worktree was changed.

Runtime locations stay separate:
- Lab: C:/BlueBrickLab
- Production: C:/BlueBrick
- Recovery: C:/VIRA-Recovery/BlueBrick

Do not move or merge runtime output into source. Do not perform SOLIDWORKS, PDM, COM registration, deployment, or external mutations without their separately required approval.
