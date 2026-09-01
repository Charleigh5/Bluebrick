<#
.SYNOPSIS
    Registers the BlueBrick Lab add-in (BlueBrick.Lab.dll) as a SOLIDWORKS add-in
    that coexists with the production BlueBrick deployment.

.DESCRIPTION
    Because BlueBrick.Lab.dll is NOT strong-named, RegAsm /codebase refuses it,
    so registration is done by writing registry keys directly (verified against
    the live C:\BlueBrick production registration).

    Two modes:

      -Mode All      (default)  No-per-user-discovery assumption. Writes:
                           1. HKCR\CLSID\{251d6df2-...}\InprocServer32  (COM binding)
                           2. HKLM\SOFTWARE\SolidWorks\Addins\{...}     (add-in metadata)
                           3. HKCU\Software\SolidWorks\AddInsStartup\{...} (load at startup)
                         Requires elevation (self-elevates).

      -Mode PerUser            Per-user, NO elevation required. Writes:
                           1. HKCU\Software\Classes\CLSID\{...}\InprocServer32  (per-user COM)
                           2. HKCU\SOFTWARE\SolidWorks\Addins\{...}             (per-user metadata)
                           3. HKCU\Software\SolidWorks\AddInsStartup\{...}      (load at startup)
                         COM activation works reliably for the current user.
                         SOLIDWORKS 2025 SP5 discovery via HKCU...\SolidWorks\Addins
                         is version-dependent and is TESTED in the lab. If SW ignores
                         HKCU discovery, you need only ONE-TIME elevated registration of
                         the HKLM discovery key to that same Lab GUID (-Mode All), after
                         which every Lab DLL re-build/re-register stays admin-free via
                         -Mode PerUser.

.SAFETY
    NON-DESTRUCTIVE by default (-WhatIf honoured). Never touches production keys
    (Lab GUID is distinct). Backs up any pre-existing Lab keys to .reg files under
    $BackupRoot before writing, verifies every key after writing. -Unregister removes.

.PARAMETER LabDllPath
    Path to the built Lab assembly. Defaults to sibling bin\Lab output.

.PARAMETER IconPath
    Optional icon path for the Addins Title/Icon entries.

.PARAMETER Unregister
    Removes the Lab keys for the selected mode after exporting a backup. Does not
    delete files.

.PARAMETER Mode
    All (default, HKCR+HKLM+HKCU, elevated) or PerUser (HKCU only, no elevation).

.PARAMETER BackupRoot
    Where .reg backups are written. Default: $env:TEMP\bluebrick-lab-reg-backups

.EXAMPLE
    .\tools\register-lab-addin.ps1 -WhatIf
    .\tools\register-lab-addin.ps1 -Mode PerUser -Confirm   # no admin, HKCU only
    .\tools\register-lab-addin.ps1 -Confirm                 # All mode, self-elevates
    .\tools\register-lab-addin.ps1 -Mode PerUser -Unregister
#>
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [string]$LabDllPath = (Join-Path $PSScriptRoot '..\bin\Lab\BlueBrick.Lab.dll'),
    [string]$IconPath = '',
    [switch]$Unregister,
    [ValidateSet('All','PerUser')]
    [string]$Mode = 'All',
    <# When set, stages the Lab build into this space-free folder (e.g. C:\BlueBrickLab)
       and registers CodeBase against it. Recommended: COM/Fusion resolves space-free
       CodeBases far more reliably than paths like "VIRA GITHUB". Admin-free. #>
    [string]$StageRoot = '',
    [string]$BackupRoot = (Join-Path $env:TEMP 'bluebrick-lab-reg-backups')
)

$ErrorActionPreference = 'Stop'
$LabGuid       = '{251d6df2-3e7b-42ef-b7fc-175e1fdcb4c5}'
$LabClass      = 'BlueBrick.SwAddin'
$ExpectedVer   = '1.0.13.4'
$MsCoree       = "$env:WINDIR\System32\mscoree.dll"

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "    OK  $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "    WARN $msg" -ForegroundColor Yellow }

# --- Path selection by mode ---------------------------------------------
$isPerUser = ($Mode -eq 'PerUser')
if ($isPerUser) {
    $clsidPs    = "Registry::HKEY_CURRENT_USER\Software\Classes\CLSID\$LabGuid"
    $clsidReg   = "HKCU\Software\Classes\CLSID\$LabGuid"
    $addinsPs   = "Registry::HKEY_CURRENT_USER\Software\SolidWorks\Addins\$LabGuid"
    $addinsReg  = "HKCU\SOFTWARE\SolidWorks\Addins\$LabGuid"
    $modeName   = 'PerUser (HKCU only, no elevation)'
} else {
    $clsidPs    = "Registry::HKEY_CLASSES_ROOT\CLSID\$LabGuid"
    $clsidReg   = "HKCR\CLSID\$LabGuid"
    $addinsPs   = "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\SolidWorks\Addins\$LabGuid"
    $addinsReg  = "HKLM\SOFTWARE\SolidWorks\Addins\$LabGuid"
    $modeName   = 'All (HKCR+HKLM+HKCU, elevated)'
}
$startupPs  = "Registry::HKEY_CURRENT_USER\Software\SolidWorks\AddInsStartup\$LabGuid"
$startupReg = "HKCU\Software\SolidWorks\AddInsStartup\$LabGuid"
$inprocPs   = "$clsidPs\InprocServer32"

Write-Step "Mode: $modeName"

# --- Elevation (skipped entirely for PerUser) ---------------------------
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
            ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin -and -not $WhatIfPreference -and -not $isPerUser) {
    # Only All-mode self-elevates; WhatIf/-Confirm previews stay in-process.
    $script = "`"$PSCommandPath`""
    foreach ($k in $MyInvocation.BoundParameters.Keys) {
        if ($k -notin 'WhatIf','Confirm') {
            $v = $MyInvocation.BoundParameters[$k]
            $script += " -$k `"$v`""
        }
    }
    Write-Step "Self-elevating: $script"
    Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass',$script) -Wait
    exit $LASTEXITCODE
}

# --- Validate inputs -----------------------------------------------------
if (-not $Unregister -and -not (Test-Path -LiteralPath $LabDllPath)) { throw "Lab DLL not found: $LabDllPath. Build Lab config first." }
if (Test-Path -LiteralPath $LabDllPath) {
    $LabDllPath = (Resolve-Path -LiteralPath $LabDllPath).Path
} elseif ($Unregister) {
    # Unregister only needs the registry identity. The staged DLL may already
    # have been removed by a first-time Lab rollback.
    $LabDllPath = [IO.Path]::GetFullPath($LabDllPath)
}

# --- Optional staging to a space-free path -------------------------------
if ($StageRoot -and -not $Unregister) {
    $srcDir = Split-Path $LabDllPath -Parent
    Write-Step "Staging Lab build -> $StageRoot (space-free, admin-free)"
    New-Item -ItemType Directory -Path $StageRoot -Force | Out-Null
    Copy-Item (Join-Path $srcDir '*') $StageRoot -Recurse -Force
    $LabDllPath = Join-Path $StageRoot (Split-Path $LabDllPath -Leaf)
}
$ver = if (Test-Path -LiteralPath $LabDllPath) { (Get-Item $LabDllPath).VersionInfo.FileVersion } else { 'not-present (unregister)' }
if ($Unregister) {
    Write-Step "Lab registry identity: $LabGuid  [$ver]"
} else {
    if ($ver -ne $ExpectedVer) {
        Write-Warn "Lab DLL version is $ver (expected $ExpectedVer) - proceeding anyway."
    }
    Write-Step "Lab assembly: $LabDllPath  [$ver]"
}

$codeBase = 'file:///' + ($LabDllPath -replace '\\','/' -replace ' ','%20').ToUpperInvariant()
Write-Step "CodeBase (URI-encoded): $codeBase"

# Pure preview: no disk/registry side effects at all under -WhatIf.
if ($WhatIfPreference) {
    Write-Host "`n[PREVIEW] Would write ($Mode):" -ForegroundColor Yellow
    Write-Host "  $clsidReg\InprocServer32  (mscoree.dll, BlueBrick.Lab $ExpectedVer, Class=$LabClass)" -ForegroundColor Yellow
    Write-Host "  $addinsReg  (Title=BlueBrick Lab)" -ForegroundColor Yellow
    Write-Host "  $startupReg  (=1)" -ForegroundColor Yellow
    Write-Host "  Backups -> $BackupRoot  (only on real run)" -ForegroundColor Yellow
    Write-Host "No changes made." -ForegroundColor Yellow
    exit 0
}

# --- Backup any existing Lab keys (mode-selected locations) --------------
New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$bkClsid   = Join-Path $BackupRoot "clsid-$Mode-$stamp.reg"
$bkAddins  = Join-Path $BackupRoot "addins-$Mode-$stamp.reg"
$bkStartup = Join-Path $BackupRoot "startup-$stamp.reg"
if (Test-Path $clsidPs) {  reg export $clsidReg  $bkClsid   /y | Out-Null; Write-Ok "Backed up CLSID -> $bkClsid" }
if (Test-Path $addinsPs) { reg export $addinsReg $bkAddins  /y | Out-Null; Write-Ok "Backed up Addins -> $bkAddins" }
if (Test-Path $startupPs){ reg export $startupReg $bkStartup /y | Out-Null; Write-Ok "Backed up startup -> $bkStartup" }

if ($Unregister) {
    Write-Step "Unregistering Lab add-in $LabGuid ($Mode)"
    foreach ($key in @($clsidPs, $addinsPs, $startupPs)) {
        if (Test-Path $key) {
            if ($PSCmdlet.ShouldProcess($key, 'Remove-Item -Recurse')) {
                Remove-Item $key -Recurse -Force
                Write-Ok "Removed $key"
            }
        } else {
            Write-Warn "Not present: $key"
        }
    }
    Write-Step "Done. Backups in $BackupRoot"
    exit 0
}

# --- REGISTER ------------------------------------------------------------
# 1. COM binding (per-user CLSID in PerUser mode; ThreadingModel=Both,
#    RuntimeVersion=v4.0.30319, mscoree.dll, Class=BlueBrick.SwAddin)
if ($PSCmdlet.ShouldProcess($inprocPs, 'Create COM CLSID registration')) {
    New-Item -Path $clsidPs -Force | Out-Null
    New-Item -Path $inprocPs -Force | Out-Null
    New-ItemProperty -Path $inprocPs -Name '(default)'       -Value $MsCoree -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $inprocPs -Name 'Assembly'        -Value "BlueBrick.Lab, Version=$ExpectedVer, Culture=neutral, PublicKeyToken=null" -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $inprocPs -Name 'Class'           -Value $LabClass -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $inprocPs -Name 'CodeBase'        -Value $codeBase -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $inprocPs -Name 'RuntimeVersion'  -Value 'v4.0.30319' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $inprocPs -Name 'ThreadingModel'  -Value 'Both' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $clsidPs -Name '(default)'        -Value 'BlueBrick Lab SwAddin' -PropertyType String -Force | Out-Null
    Write-Ok "CLSID registration written ($clsidReg)"
}

# 2. Addins metadata
if ($PSCmdlet.ShouldProcess($addinsPs, 'Create Addins entry')) {
    New-Item -Path $addinsPs -Force | Out-Null
    New-ItemProperty -Path $addinsPs -Name '(default)'   -Value 0 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $addinsPs -Name 'Description' -Value 'BlueBrick Lab (isolated test build) - do not use for production' -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $addinsPs -Name 'Title'       -Value 'BlueBrick Lab' -PropertyType String -Force | Out-Null
    if ($IconPath -and (Test-Path $IconPath)) {
        New-ItemProperty -Path $addinsPs -Name 'Icon Path' -Value $IconPath -PropertyType String -Force | Out-Null
    }
    Write-Ok "Addins entry written ($addinsReg)"
}

# 3. Load-at-startup (HKCU)
if ($PSCmdlet.ShouldProcess($startupPs, 'Enable load-at-startup')) {
    New-Item -Path $startupPs -Force | Out-Null
    New-ItemProperty -Path $startupPs -Name '(default)' -Value 1 -PropertyType DWord -Force | Out-Null
    Write-Ok "HKCU startup enabled"
}

# --- Verification ---------------------------------------------------------
Write-Step "Verifying registration"
$checks = @(
    @{ Name='CLSID InprocServer32 exists';  Path=$inprocPs },
    @{ Name='CLSID CodeBase matches';       Path=$inprocPs },
    @{ Name='Addins entry exists';          Path=$addinsPs },
    @{ Name='HKCU startup exists';          Path=$startupPs }
)
$fail = 0
foreach ($c in $checks) {
    if (Test-Path $c.Path) {
        if ($c.Name -like 'CLSID CodeBase*') {
            $cb = (Get-ItemProperty $c.Path).CodeBase
            if ($cb -eq $codeBase) { Write-Ok "$($c.Name)" } else { Write-Warn "$($c.Name): got '$cb' expected '$codeBase'"; $fail++ }
        } else {
            Write-Ok "$($c.Name)"
        }
    } else { Write-Warn "MISSING: $($c.Name)"; $fail++ }
}

if ($fail -eq 0) {
    Write-Step "Registration complete ($Mode)."
    if ($isPerUser) {
        Write-Host "    COM activation: works for current user (no admin)."
        Write-Host "    SW discovery:   launch SOLIDWORKS 2025 SP5 -> Tools > Add-Ins."
        Write-Host "      If 'BlueBrick Lab' appears & loads: HKCU discovery works -> admin-free forever."
        Write-Host "      If it does NOT: only the HKLM discovery key needs ONE-TIME admin"
        Write-Host "      (run '-Mode All' once to add that key); subsequent DLL re-registers"
        Write-Host "      via '-Mode PerUser' stay admin-free."
    } else {
        Write-Host "    Launch SOLIDWORKS 2025 SP5 to load 'BlueBrick Lab'. Backups: $BackupRoot"
    }
    Write-Host "    Backups: $BackupRoot" -ForegroundColor Green
} else {
    Write-Host "Registration incomplete ($fail check(s) failed). Backups: $BackupRoot" -ForegroundColor Red
    exit 1
}
