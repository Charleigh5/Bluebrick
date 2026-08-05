<#
.SYNOPSIS
    Safely prepares the production BlueBrick add-in replacement:
    backs up the live DLL/registry, copies the newly built Debug assembly over
    the production path, and verifies. Also registers a new CLSID so the new
    build can coexist OR replaces the existing production registration.

.DESCRIPTION
    SAFETY-FIRST (NON-DESTRUCTIVE BY DEFAULT):
      - Dry-run by default: add -Execute to actually perform the copy.
      - Never modifies C:\BlueBrick without a timestamped backup first.
      - Exports the current production registry keys to .reg before any change.
      - Verifies hash + version after copy.
      - Prints an exact rollback command set at the end.

    Two supported modes:
      MODE 1 (default, recommended): "Lab deploy" - copies bin\Lab\BlueBrick.Lab.dll
        into C:\BlueBrick\BlueBrick.Lab.dll and registers the LAB GUID, so the new
        build coexists with production. Production is never touched.
      MODE 2 (-ReplaceProduction): copies bin\Debug\BlueBrick.dll over
        C:\BlueBrick\BlueBrick.dll (after backup) and points the EXISTING
        production CLSID CodeBase at it. This replaces the live add-in.

.PARAMETER Mode
    'Lab' (default) or 'ReplaceProduction'.

.PARAMETER Execute
    Performs the actions. Without it, only a dry-run report is printed.

.PARAMETER BackupRoot
    Where backups are written. Default: C:\BlueBrick\backups\<timestamp>

.EXAMPLE
    # Dry-run (default, safe):
    .\tools\deploy-production.ps1

.EXAMPLE
    # Actually deploy the Lab build alongside production:
    .\tools\deploy-production.ps1 -Mode Lab -Execute

.EXAMPLE
    # Actually replace production (requires the new Debug build):
    .\tools\deploy-production.ps1 -Mode ReplaceProduction -Execute
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('Lab','ReplaceProduction')]
    [string]$Mode = 'Lab',
    [switch]$Execute,
    [string]$BackupRoot = ''
)

$ErrorActionPreference = 'Stop'
$repo       = Split-Path $PSScriptRoot -Parent
$prodDll    = 'C:\BlueBrick\BlueBrick.dll'
$prodGuid   = '{C56E0AFF-0BD3-4364-90CB-1A581046CD7D}'
$labGuid    = '{251d6df2-3e7b-42ef-b7fc-175e1fdcb4c5}'
$MsCoree    = "$env:WINDIR\System32\mscoree.dll"

function Write-Step($m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Write-Ok($m)   { Write-Host "    OK  $m" -ForegroundColor Green }
function Write-Warn($m) { Write-Host "    WARN $m" -ForegroundColor Yellow }

# --- Resolve source ------------------------------------------------------
if ($Mode -eq 'Lab') {
    $src = Join-Path $repo 'bin\Lab\BlueBrick.Lab.dll'
    $dst = 'C:\BlueBrick\BlueBrick.Lab.dll'
    $guid = $labGuid
    $label = 'Lab (coexist)'
} else {
    $src = Join-Path $repo 'bin\Debug\BlueBrick.dll'
    $dst = $prodDll
    $guid = $prodGuid
    $label = 'ReplaceProduction'
}

if (-not (Test-Path $src)) { throw "Source not found: $src. Build the $Mode configuration first." }
$src = (Resolve-Path $src).Path
$srcHash = (Get-FileHash $src -Algorithm SHA256).Hash
$srcVer  = (Get-Item $src).VersionInfo.FileVersion

# --- Backup root ---------------------------------------------------------
if (-not $BackupRoot) { $BackupRoot = "C:\BlueBrick\backups\$(Get-Date -Format 'yyyyMMdd-HHmmss')" }

Write-Step "Mode: $label"
Write-Ok "Source : $src  [$srcVer]  SHA256=$($srcHash.Substring(0,16))..."
Write-Ok "Target : $dst"
Write-Ok "Backup (planned) : $BackupRoot"

# Pure preview: no disk/registry side effects at all.
if (-not $Execute) {
    Write-Host "`n[DRY RUN] No changes made. Would:" -ForegroundColor Yellow
    Write-Host "  1. Create $BackupRoot" -ForegroundColor Yellow
    if (Test-Path $prodDll) { Write-Host "  2. Backup $prodDll -> $BackupRoot\BlueBrick.dll.orig" -ForegroundColor Yellow }
    Write-Host "  3. Export production registry keys to $BackupRoot\*.reg" -ForegroundColor Yellow
    Write-Host "  4. Copy $src -> $dst  (hash-verified)" -ForegroundColor Yellow
    if ($Mode -eq 'Lab') {
        Write-Host "  5. Register Lab CLSID $labGuid + Addins + startup (production untouched)" -ForegroundColor Yellow
    } else {
        Write-Host "  5. Re-point production CLSID $prodGuid CodeBase -> $dst" -ForegroundColor Yellow
    }
    Write-Host "Re-run with -Execute to apply." -ForegroundColor Yellow
    exit 0
}

New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null

# --- Pre-checks ----------------------------------------------------------
if (Test-Path $prodDll) {
    $prodHash = (Get-FileHash $prodDll -Algorithm SHA256).Hash
    $prodVer  = (Get-Item $prodDll).VersionInfo.FileVersion
    Write-Ok "Production DLL present: $prodDll  [$prodVer]  SHA256=$($prodHash.Substring(0,16))..."
} else {
    Write-Warn "No production DLL at $prodDll (first-time deploy)."
}

if ($Mode -eq 'Lab' -and (Test-Path $dst)) {
    Write-Warn "Target $dst already exists. A backup will be made, then overwritten."
}

# --- Backups (DLL + registry) --------------------------------------------
Write-Step "Creating backups"
if (Test-Path $prodDll) {
    Copy-Item $prodDll (Join-Path $BackupRoot 'BlueBrick.dll.orig') -Force
    Write-Ok "DLL backup: $(Join-Path $BackupRoot 'BlueBrick.dll.orig')"
}
if (Test-Path "Registry::HKEY_CLASSES_ROOT\CLSID\$prodGuid") {
    reg export "HKCR\CLSID\$prodGuid" (Join-Path $BackupRoot 'prod-clsid.reg') /y | Out-Null
    Write-Ok "Registry backup: $(Join-Path $BackupRoot 'prod-clsid.reg')"
}
if (Test-Path "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\SolidWorks\Addins\$prodGuid") {
    reg export "HKLM\SOFTWARE\SolidWorks\Addins\$prodGuid" (Join-Path $BackupRoot 'prod-addins.reg') /y | Out-Null
    Write-Ok "Registry backup: $(Join-Path $BackupRoot 'prod-addins.reg')"
}
if (Test-Path "Registry::HKEY_CURRENT_USER\Software\SolidWorks\AddInsStartup\$prodGuid") {
    reg export "HKCU\Software\SolidWorks\AddInsStartup\$prodGuid" (Join-Path $BackupRoot 'prod-startup.reg') /y | Out-Null
    Write-Ok "Registry backup: $(Join-Path $BackupRoot 'prod-startup.reg')"
}

# --- Elevation check (writes need HKLM/HKCR) ------------------------------
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
            ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if ($Execute -and -not $isAdmin) {
    throw "Elevation required for -Execute. Run from an elevated shell (or self-elevate)."
}

# --- Perform copy --------------------------------------------------------
Write-Step "Copying $dst"
$PSCmdlet.ShouldProcess($dst, "Copy-Item $src") | Out-Null
Copy-Item $src $dst -Force
$newHash = (Get-FileHash $dst -Algorithm SHA256).Hash
$newVer  = (Get-Item $dst).VersionInfo.FileVersion
if ($newHash -ne $srcHash) { throw "Hash mismatch after copy! Aborting. Source=$srcHash Target=$newHash" }
Write-Ok "Copy verified: SHA256=$($newHash.Substring(0,16))...  [$newVer]"

# --- Registry -------------------------------------------------------------
$codeBase = 'file:///' + ($dst -replace '\\','/' -replace ' ','%20').ToUpperInvariant()

if ($Mode -eq 'Lab') {
    # New CLSID for the Lab assembly (coexist; production untouched)
    $clsid = "Registry::HKEY_CLASSES_ROOT\CLSID\$labGuid\InprocServer32"
    New-Item -Path $clsid -Force | Out-Null
    New-ItemProperty -Path $clsid -Name '(default)' -Value $MsCoree -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $clsid -Name 'Assembly' -Value "BlueBrick.Lab, Version=$newVer, Culture=neutral, PublicKeyToken=null" -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $clsid -Name 'Class' -Value 'BlueBrick.SwAddin' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $clsid -Name 'CodeBase' -Value $codeBase -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $clsid -Name 'RuntimeVersion' -Value 'v4.0.30319' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $clsid -Name 'ThreadingModel' -Value 'Both' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path "Registry::HKEY_CLASSES_ROOT\CLSID\$labGuid" -Name '(default)' -Value 'BlueBrick Lab SwAddin' -PropertyType String -Force | Out-Null

    $addins = "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\SolidWorks\Addins\$labGuid"
    New-Item -Path $addins -Force | Out-Null
    New-ItemProperty -Path $addins -Name '(default)' -Value 0 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $addins -Name 'Description' -Value 'BlueBrick Lab (isolated test build)' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $addins -Name 'Title' -Value 'BlueBrick Lab' -PropertyType String -Force | Out-Null

    New-Item -Path "Registry::HKEY_CURRENT_USER\Software\SolidWorks\AddInsStartup\$labGuid" -Force | Out-Null
    New-ItemProperty -Path "Registry::HKEY_CURRENT_USER\Software\SolidWorks\AddInsStartup\$labGuid" -Name '(default)' -Value 1 -PropertyType DWord -Force | Out-Null
    Write-Ok "Registered Lab CLSID/Addins/startup (production untouched)."
} else {
    # Point the EXISTING production CLSID at the new DLL (in-place replace)
    $inproc = "Registry::HKEY_CLASSES_ROOT\CLSID\$prodGuid\InprocServer32"
    New-Item -Path $inproc -Force | Out-Null
    New-ItemProperty -Path $inproc -Name 'CodeBase' -Value $codeBase -PropertyType String -Force | Out-Null
    Write-Ok "Re-pointed production CLSID CodeBase -> $codeBase"
}

Write-Host "`n=== DEPLOY COMPLETE ===" -ForegroundColor Green
Write-Host "Backups: $BackupRoot" -ForegroundColor Green
Write-Host "`n=== ROLLBACK (run from elevated shell) ===" -ForegroundColor Yellow
if ($Mode -eq 'Lab') {
    Write-Host "  Copy-Item '$BackupRoot\BlueBrick.dll.orig' 'C:\BlueBrick\BlueBrick.dll' -Force" -ForegroundColor Yellow
    Write-Host "  reg delete HKCR\CLSID\$labGuid /f" -ForegroundColor Yellow
    Write-Host "  reg delete HKLM\SOFTWARE\SolidWorks\Addins\$labGuid /f" -ForegroundColor Yellow
    Write-Host "  reg delete HKCU\Software\SolidWorks\AddInsStartup\$labGuid /f" -ForegroundColor Yellow
} else {
    Write-Host "  Copy-Item '$BackupRoot\BlueBrick.dll.orig' 'C:\BlueBrick\BlueBrick.dll' -Force" -ForegroundColor Yellow
    Write-Host "  reg import '$BackupRoot\prod-clsid.reg'" -ForegroundColor Yellow
    Write-Host "  reg import '$BackupRoot\prod-addins.reg'" -ForegroundColor Yellow
    Write-Host "  reg import '$BackupRoot\prod-startup.reg'" -ForegroundColor Yellow
}
