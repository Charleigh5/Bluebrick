# BlueBrick IT Review Package Manifest

Package: `BlueBrick_IT_Review_2026-06-11.zip`

Source root: `C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick`

Branch at packaging: `bluebrick-assistant-slice1-foundation`

HEAD at packaging: `ced14be`

## Primary Entry Point

Open `docs/review/bluebrick-it-review-2026-06-11.html` first. It is a standalone HTML review dossier for the lead architect.

## Included Source Areas

- `Agent/`
- `AssistantWeb/`
- `BlueBrick.Relay/`
- `BlueBrick.Relay.Tests/`
- `BlueBrick.UI.Tests/`
- `config/`
- `DataFiles/`
- `DocGenerator/`
- `docs/`
- `Forms/`
- `Resources/`
- `Services/`
- `Simulation/`
- `Vault/`
- `lib/`
- `Properties/`
- root source and project files including `BlueBrick.sln`, `BlueBrick.csproj`, `Directory.Build.props`, `App.config`, `AppIdentity.cs`, `AssistantPanel.cs`, `AssistantPanel.Designer.cs`, `Cls*.cs`, `Frm*.cs`, `swaddin.cs`, `eventhandling.cs`, `packages.config`, resource files, icons, and installer script source.

## Review Evidence Files

- `docs/review/git-status-at-packaging.txt`
- `docs/review/phase2-branch-delta-main-to-head.patch`
- `docs/review/current-working-tree-delta.patch`
- `docs/review/verification-summary.md`
- `docs/review/local-test-harness/README.md`

## Deliberately Excluded

- `.git/`
- `.env` and `.env.*`
- secret, key, certificate, and token files
- `bin/`, `obj/`, `packages/`, `TestResults/`
- `.vs/`, `.vscode/`
- `tmp_*.ps1`, `tmp_*.bat`, `check_regroot.ps1`
- `CUserscweirrelay_test_output.txt`
- `IntegrationTest/obj/`

## Local Test Harness Quarantine

The untracked `IntegrationTest` source files are included only under `docs/review/local-test-harness/`. They are not presented as production source because the harness reads live registry and environment state and temporarily clears/restores values during one scenario.

## Review Notes

The package intentionally separates committed Phase 2 assistant work from current uncommitted WIP:

- Committed branch delta: inspect `phase2-branch-delta-main-to-head.patch`.
- Current uncommitted WIP: inspect `current-working-tree-delta.patch`.
- Local scratch scripts remain out of the package.
