<#
.SYNOPSIS
    Safely stages the BlueBrick Lab add-in alongside Production.

.DESCRIPTION
    SAFETY-FIRST (NON-DESTRUCTIVE BY DEFAULT):
      - Dry-run by default: add -Execute to actually perform the copy.
      - Never modifies C:\BlueBrickLab without a timestamped Lab-only backup first.
      - Production paths and registry identities are not touched by Lab mode.
      - Verifies hash + version after copy.
      - Prints an exact rollback command set at the end.

    Supported mode:
      "Lab deploy" - copies bin\Lab\BlueBrick.Lab.dll
        into C:\BlueBrickLab\BlueBrick.Lab.dll and stages appsettings.lab.json.
        Registration is a separate explicit PerUser Lab action.

      Production replacement is intentionally not a mode of this script.

.PARAMETER Mode
    'Lab' (the only permitted target in this acceptance sprint).

.PARAMETER Execute
    Performs the actions. Without it, only a dry-run report is printed.

.PARAMETER BackupRoot
    Where Lab backups are written. Default: C:\BlueBrickLab\backups\<timestamp>

.EXAMPLE
    # Dry-run (default, safe):
    .\tools\deploy-production.ps1

.EXAMPLE
    # Actually deploy the Lab build alongside production:
    .\tools\deploy-production.ps1 -Mode Lab -Execute

#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('Lab')]
    [string]$Mode = 'Lab',
    [switch]$Execute,
    [string]$BackupRoot = ''
)

$ErrorActionPreference = 'Stop'
$repo       = Split-Path $PSScriptRoot -Parent
$labRuntime = 'C:\BlueBrickLab'

function Write-Step($m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Write-Ok($m)   { Write-Host "    OK  $m" -ForegroundColor Green }
function Write-Warn($m) { Write-Host "    WARN $m" -ForegroundColor Yellow }

# --- Resolve Lab-only source and target ----------------------------------
$src = Join-Path $repo 'bin\Lab\BlueBrick.Lab.dll'
$srcConfig = Join-Path $repo 'config\appsettings.lab.json'
$dst = Join-Path $labRuntime 'BlueBrick.Lab.dll'
$dstConfig = Join-Path $labRuntime 'config\appsettings.lab.json'
$label = 'Lab (isolated coexistence)'

if (-not (Test-Path $src)) { throw "Source not found: $src. Build the $Mode configuration first." }
if (-not (Test-Path $srcConfig)) { throw "Lab config not found: $srcConfig." }
$src = (Resolve-Path $src).Path
$srcHash = (Get-FileHash $src -Algorithm SHA256).Hash
$srcVer  = (Get-Item $src).VersionInfo.FileVersion

# --- Backup root ---------------------------------------------------------
if (-not $BackupRoot) { $BackupRoot = "$labRuntime\backups\$(Get-Date -Format 'yyyyMMdd-HHmmss')" }

Write-Step "Mode: $label"
Write-Ok "Source : $src  [$srcVer]  SHA256=$($srcHash.Substring(0,16))..."
Write-Ok "Target : $dst"
Write-Ok "Backup (planned) : $BackupRoot"

# Pure preview: no disk/registry side effects at all.
if (-not $Execute) {
    Write-Host "`n[DRY RUN] No changes made. Would:" -ForegroundColor Yellow
    Write-Host "  1. Create $BackupRoot" -ForegroundColor Yellow
    if (Test-Path $dst) { Write-Host "  2. Backup $dst -> $BackupRoot\BlueBrick.Lab.dll.orig" -ForegroundColor Yellow }
    if (Test-Path $dstConfig) { Write-Host "  3. Backup $dstConfig -> $BackupRoot\appsettings.lab.json.orig" -ForegroundColor Yellow }
    Write-Host "  4. Copy $src -> $dst and $srcConfig -> $dstConfig (hash-verified)" -ForegroundColor Yellow
    Write-Host "  5. Register Lab separately with tools\register-lab-addin.ps1 -Mode PerUser -StageRoot $labRuntime" -ForegroundColor Yellow
    Write-Host "Re-run with -Execute to apply." -ForegroundColor Yellow
    exit 0
}

New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null

# --- Pre-checks ----------------------------------------------------------
if (Test-Path $dst) {
    $labHash = (Get-FileHash $dst -Algorithm SHA256).Hash
    $labVer  = (Get-Item $dst).VersionInfo.FileVersion
    Write-Ok "Existing Lab DLL present: $dst  [$labVer]  SHA256=$($labHash.Substring(0,16))..."
} else {
    Write-Warn "No existing Lab DLL at $dst (first-time Lab deploy)."
}

if (Test-Path $dst) {
    Write-Warn "Lab target $dst already exists. A Lab-only backup will be made, then overwritten."
}

# --- Backups (DLL + registry) --------------------------------------------
Write-Step "Creating backups"
if (Test-Path $dst) {
    Copy-Item $dst (Join-Path $BackupRoot 'BlueBrick.Lab.dll.orig') -Force
    Write-Ok "Lab DLL backup: $(Join-Path $BackupRoot 'BlueBrick.Lab.dll.orig')"
}
if (Test-Path $dstConfig) {
    Copy-Item $dstConfig (Join-Path $BackupRoot 'appsettings.lab.json.orig') -Force
    Write-Ok "Lab config backup: $(Join-Path $BackupRoot 'appsettings.lab.json.orig')"
}

# --- Perform copy --------------------------------------------------------
Write-Step "Copying $dst"
$targetDir = Split-Path $dst -Parent
$configDir = Split-Path $dstConfig -Parent
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
New-Item -ItemType Directory -Path $configDir -Force | Out-Null
$PSCmdlet.ShouldProcess($dst, "Copy-Item $src") | Out-Null
Copy-Item $src $dst -Force
$newHash = (Get-FileHash $dst -Algorithm SHA256).Hash
$newVer  = (Get-Item $dst).VersionInfo.FileVersion
if ($newHash -ne $srcHash) { throw "Hash mismatch after copy! Aborting. Source=$srcHash Target=$newHash" }
Write-Ok "Copy verified: SHA256=$($newHash.Substring(0,16))...  [$newVer]"

$srcConfigHash = (Get-FileHash $srcConfig -Algorithm SHA256).Hash
$PSCmdlet.ShouldProcess($dstConfig, "Copy-Item $srcConfig") | Out-Null
Copy-Item $srcConfig $dstConfig -Force
$newConfigHash = (Get-FileHash $dstConfig -Algorithm SHA256).Hash
if ($newConfigHash -ne $srcConfigHash) { throw "Lab config hash mismatch after copy!" }
Write-Ok "Lab config verified: SHA256=$($newConfigHash.Substring(0,16))..."

Write-Ok "Lab registration remains a separate PerUser action; Production registry was not touched."

Write-Host "`n=== DEPLOY COMPLETE ===" -ForegroundColor Green
Write-Host "Backups: $BackupRoot" -ForegroundColor Green
Write-Host "`n=== LAB ROLLBACK ===" -ForegroundColor Yellow
Write-Host "  Copy-Item '$BackupRoot\BlueBrick.Lab.dll.orig' '$dst' -Force" -ForegroundColor Yellow
Write-Host "  Copy-Item '$BackupRoot\appsettings.lab.json.orig' '$dstConfig' -Force" -ForegroundColor Yellow
Write-Host "  Re-run tools\register-lab-addin.ps1 -Mode PerUser -StageRoot '$labRuntime' -BackupRoot '$BackupRoot\registry'" -ForegroundColor Yellow
Write-Host "  Production rollback paths are intentionally absent." -ForegroundColor Yellow
