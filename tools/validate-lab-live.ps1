<#
  validate-lab-live.ps1  --  R3 LIVE LAB VALIDATION (post-registration, NON-ELEVATED, read-only)

  Verifies the BlueBrick Lab add-in registration end-to-end WITHOUT requiring elevation:
    1. Discovery key   HKLM\SOFTWARE\SolidWorks\Addins\{251d6df2-3e7b-42ef-b7fc-175e1fdcb4c5}
    2. Activation key  HKCR\CLSID\{251d6df2-3e7b-42ef-b7fc-175e1fdcb4c5}\InprocServer32
    3. Per-user load   HKCU\SOFTWARE\SolidWorks\AddInsStartup\{251d6df2-...}
    4. Lab DLL presence, version, SHA256 integrity
    5. CodeBase match  (registered CodeBase resolves to the same DLL being validated)
    6. Assembly load   BlueBrick.Lab.dll -> BlueBrick.SwAddin type resolution
    7. COM activation  [Activator]::CreateInstance via the registered CLSID (probe)

  This is the evidence source for the R3_LAB_VALIDATION_PARTIAL / R4 gate.
  Nothing in this script writes to the registry, filesystem, or network.

  USAGE:
      powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\validate-lab-live.ps1
      powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\validate-lab-live.ps1 -LabDllPath C:\path\to\BlueBrick.Lab.dll
#>
[CmdletBinding()]
param(
    [string]$LabDllPath = (Join-Path $PSScriptRoot '..\bin\Lab\BlueBrick.Lab.dll'),
    [switch]$SkipComProbe
)

$ErrorActionPreference = 'Stop'
$LabGuid      = '{251d6df2-3e7b-42ef-b7fc-175e1fdcb4c5}'
$ExpectedVer  = '1.0.13.4'
$ExpectedClass = 'BlueBrick.SwAddin'
$results = [System.Collections.Generic.List[string]]::new()

function Add-Result([string]$Kind, [string]$Msg) {
    $results.Add(('[{0}] {1}' -f $Kind, $Msg))
    Write-Host ('[{0}] {1}' -f $Kind, $Msg)
}

Write-Host '=== BlueBrick Lab add-in: R3 live validation (read-only) ===' -ForegroundColor Cyan

# ---- 1. Discovery key (All-mode HKLM OR PerUser-mode HKCU) ----
$kHklm = "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\SolidWorks\Addins\$LabGuid"
$kHkcu = "Registry::HKEY_CURRENT_USER\Software\SolidWorks\Addins\$LabGuid"
if (Test-Path $kHklm) { $disc = @{ Kind='HKLM'; Path=$kHklm } }
elseif (Test-Path $kHkcu) { $disc = @{ Kind='HKCU'; Path=$kHkcu } }
else { $disc = @{} }
if ($disc.Count) {
    $p = Get-ItemProperty $disc.Path
    $title = $p.Title; $dflt = $p.'(default)'
    if ($title) { Add-Result 'PASS' "Discovery ($($disc.Kind)): $($disc.Path)  Title='$title' Default=$dflt" }
    else { Add-Result 'WARN' "Discovery ($($disc.Kind)): exists but Title missing" }
    if ($disc.Kind -eq 'HKCU') {
        Add-Result 'INFO' 'HKCU discovery present - launching SW 2025 SP5 will reveal if honored'
    }
} else {
    Add-Result 'FAIL' 'Discovery key missing (neither HKLM nor HKCU) -> run tools\register-lab-addin.ps1 elevated or -Mode PerUser'
}

# ---- 2. Activation key (HKCR) ----
$k2 = "Registry::HKEY_CLASSES_ROOT\CLSID\$LabGuid\InprocServer32"
if (Test-Path $k2) {
    $p2 = Get-ItemProperty $k2
    Add-Result 'PASS' "Activation: $k2  Class=$($p2.Class) TM=$($p2.ThreadingModel) RV=$($p2.RuntimeVersion)"
    if ($p2.CodeBase) {
        $decoded = [System.Uri]::UnescapeDataString(($p2.CodeBase -replace '^file:///',''))
        Add-Result 'INFO'  "CodeBase registered: $($p2.CodeBase)"
        $resolvedLab = (Resolve-Path $LabDllPath -ErrorAction SilentlyContinue).Path
        if ($resolvedLab -and $decoded -and (($decoded -replace '/','\') -ieq ($resolvedLab -replace '/','\'))) {
            Add-Result 'PASS' 'CodeBase resolves to the validated Lab DLL (match)'
        } else {
            Add-Result 'WARN' "CodeBase mismatch: registered='$decoded' vs validated='$resolvedLab'"
        }
    } else {
        Add-Result 'WARN' 'Activation key present but no CodeBase value'
    }
} else {
    Add-Result 'FAIL' "Activation key missing: $k2  -> run tools\register-lab-addin.ps1 from an ELEVATED PowerShell"
}

# ---- 3. Per-user startup (HKCU) ----
$k3 = "Registry::HKEY_CURRENT_USER\SOFTWARE\SolidWorks\AddInsStartup\$LabGuid"
if ((Test-Path $k3) -and ((Get-ItemProperty $k3).'(default)' -eq 1 -or (Get-Item $k3).GetValue('') -eq 1)) {
    Add-Result 'PASS' "Per-user load enabled: $k3 = 1"
} else {
    Add-Result 'WARN' "Per-user load not enabled: $k3 (add = 1 to auto-load for this user)"
}

# ---- 4. Lab DLL presence / version / integrity ----
if (Test-Path $LabDllPath) {
    $dll = Get-Item $LabDllPath
    $ver = $dll.VersionInfo.FileVersion
    $hash = (Get-FileHash $dll.FullName -Algorithm SHA256).Hash
    if ($ver -eq $ExpectedVer) { Add-Result 'PASS' "Lab DLL $($dll.FullName) version=$ver" }
    else { Add-Result 'FAIL' "Lab DLL version=$ver (expected $ExpectedVer)" }
    Add-Result 'INFO' "SHA256=$hash"
    if ((Get-Item 'C:\BlueBrick\BlueBrick.dll' -ErrorAction SilentlyContinue)) {
        Add-Result 'INFO' "Production DLL untouched: SHA256=$((Get-FileHash 'C:\BlueBrick\BlueBrick.dll').Hash)"
    }
} else {
    Add-Result 'FAIL' "Lab DLL not found: $LabDllPath"
}

# ---- 5/6. Assembly load + type resolution ----
try {
    $asm = [System.Reflection.Assembly]::LoadFrom($LabDllPath)
    $type = $asm.GetType($ExpectedClass, $true, $true)
    Add-Result 'PASS' "Assembly loaded: $($asm.GetName().Name) v$($asm.GetName().Version); type $ExpectedClass resolved"
} catch {
    Add-Result 'FAIL' "Assembly/type resolution: $($_.Exception.Message)"
}

# ---- 7. COM activation probe ----
if (-not $SkipComProbe) {
    try {
        $t = [System.Type]::GetTypeFromCLSID([guid]$LabGuid)
        if ($null -eq $t) {
            Add-Result 'WARN' 'COM probe: GetTypeFromCLSID returned null (registration incomplete or architecture mismatch)'
        } else {
            $inst = [System.Activator]::CreateInstance($t)
            if ($inst) {
                Add-Result 'PASS' "COM activation succeeded via CLSID $LabGuid -> $($inst.GetType().FullName)"
                [System.Runtime.InteropServices.Marshal]::ReleaseComObject($inst) | Out-Null
            } else {
                Add-Result 'WARN' 'COM activation returned null instance'
            }
        }
    } catch {
        Add-Result 'WARN' "COM activation probe failed (non-fatal): $($_.Exception.Message)"
    }
} else {
    Add-Result 'INFO' 'COM activation probe skipped (-SkipComProbe)'
}

Write-Host ''
$fails = @($results | Where-Object { $_.StartsWith('[FAIL]') })
$warns = @($results | Where-Object { $_.StartsWith('[WARN]') })
if ($fails.Count -eq 0 -and $warns.Count -eq 0) { Add-Result 'PASS' 'ALL CHECKS GREEN' }
elseif ($fails.Count -eq 0) { Add-Result 'WARN' "All mandatory checks pass; $($warns.Count) warning(s) to review" }
else { Add-Result 'FAIL' "$($fails.Count) failure(s); registration incomplete -> run tools\register-lab-addin.ps1 elevated, then re-run this script" }

if ($fails.Count -gt 0) { exit 1 } else { exit 0 }
