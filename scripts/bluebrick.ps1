<#
.SYNOPSIS
    Deterministic BlueBrick Lab lifecycle controller.

.DESCRIPTION
    The controller is read-only unless an explicit Lab action is requested with
    -Execute. Production deployment, Production registry mutation, PDM writes,
    credentials, and irreversible CAD actions are not controller capabilities.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateSet('doctor','build','prepare','launch','smoke','rollback')]
    [string]$Action = 'doctor',
    [ValidateSet('Lab')]
    [string]$Target = 'Lab',
    [ValidateSet('Release','Lab')]
    [string]$Configuration = 'Lab',
    [string]$BackupRoot = '',
    [switch]$Execute,
    [switch]$Json
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$labRoot = 'C:\BlueBrickLab'
$productionRoot = 'C:\BlueBrick'
$labSource = Join-Path $repo 'bin\Lab\BlueBrick.Lab.dll'
$labConfigSource = Join-Path $repo 'config\appsettings.lab.json'
$labTarget = Join-Path $labRoot 'BlueBrick.Lab.dll'
$labConfigTarget = Join-Path $labRoot 'config\appsettings.lab.json'

function Write-Step([string]$message) { Write-Host ("[bluebrick] " + $message) -ForegroundColor Cyan }
function Fail([string]$message) { throw ("[bluebrick] " + $message) }
function Invoke-PowerShell([string]$scriptPath, [string[]]$arguments) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $scriptPath @arguments
    if ($LASTEXITCODE -ne 0) { Fail "Command failed ($LASTEXITCODE): $scriptPath" }
}
function Assert-LabOnly {
    if ($Target -ne 'Lab') { Fail 'Only Target=Lab is available in this acceptance sprint.' }
    if ([IO.Path]::GetFullPath($labRoot).TrimEnd('\') -ieq [IO.Path]::GetFullPath($productionRoot).TrimEnd('\')) { Fail 'Lab and Production runtime roots must differ.' }
}
function Assert-LabPath([string]$path, [string]$label) {
    if ([string]::IsNullOrWhiteSpace($path)) { Fail "$label is required." }
    $full = [IO.Path]::GetFullPath($path).TrimEnd('\')
    $root = [IO.Path]::GetFullPath($labRoot).TrimEnd('\') + '\'
    if (-not $full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        Fail "$label must remain under $labRoot."
    }
}
function Assert-Source {
    if (-not (Test-Path -LiteralPath $labSource)) { Fail "Lab DLL not found: $labSource. Run build --target Lab first." }
    if (-not (Test-Path -LiteralPath $labConfigSource)) { Fail "Lab config not found: $labConfigSource." }
}
function Resolve-MSBuild {
    $command = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $path = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2>$null | Select-Object -First 1
        if ($path -and (Test-Path -LiteralPath $path)) { return $path }
    }
    Fail 'Visual Studio MSBuild was not found. This legacy .NET Framework solution must use VS MSBuild.'
}
function Resolve-SolidWorks {
    $command = Get-Command SLDWORKS.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    $candidates = @(
        (Join-Path ${env:ProgramFiles} 'SOLIDWORKS Corp\SOLIDWORKS\SLDWORKS.exe'),
        (Join-Path ${env:ProgramFiles} 'SOLIDWORKS Corp\SOLIDWORKS 2025\SLDWORKS.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'SOLIDWORKS Corp\SOLIDWORKS\SLDWORKS.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'SOLIDWORKS Corp\SOLIDWORKS 2025\SLDWORKS.exe')
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) { return (Resolve-Path -LiteralPath $candidate).Path }
    }
    return $null
}
function Invoke-Build([string]$configuration) {
    $msbuild = Resolve-MSBuild
    $project = if ($configuration -eq 'Lab') { Join-Path $repo 'BlueBrick.csproj' } else { Join-Path $repo 'BlueBrick.sln' }
    Write-Step "build configuration=$configuration project=$project"
    & $msbuild $project /t:Build /p:Configuration=$configuration /p:Platform=AnyCPU /m
    if ($LASTEXITCODE -ne 0) { Fail "MSBuild failed ($LASTEXITCODE)." }
}
function New-LabBackup {
    if ([string]::IsNullOrWhiteSpace($BackupRoot)) { $script:BackupRoot = Join-Path $labRoot ('backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
    Assert-LabPath $BackupRoot 'BackupRoot'
    New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null
    $manifest = [ordered]@{
        schema = 'bluebrick-lab-deployment.v1'
        createdUtc = [DateTime]::UtcNow.ToString('o')
        target = 'Lab'
        labRoot = $labRoot
        productionRoot = $productionRoot
        labDllTarget = $labTarget
        labConfigTarget = $labConfigTarget
        productionMutation = $false
        dllExisted = Test-Path -LiteralPath $labTarget
        configExisted = Test-Path -LiteralPath $labConfigTarget
    }
    if ($manifest.dllExisted) { Copy-Item -LiteralPath $labTarget -Destination (Join-Path $BackupRoot 'BlueBrick.Lab.dll.orig') -Force }
    if ($manifest.configExisted) { Copy-Item -LiteralPath $labConfigTarget -Destination (Join-Path $BackupRoot 'appsettings.lab.json.orig') -Force }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $BackupRoot 'lab-deployment.json') -Encoding UTF8
    return $BackupRoot
}
function Invoke-LabRollback([string]$root) {
    if ([string]::IsNullOrWhiteSpace($root)) { Fail 'rollback requires -BackupRoot pointing to one exact Lab backup.' }
    Assert-LabPath $root 'BackupRoot'
    $resolved = (Resolve-Path -LiteralPath $root -ErrorAction Stop).Path
    $manifestPath = Join-Path $resolved 'lab-deployment.json'
    if (-not (Test-Path -LiteralPath $manifestPath)) { Fail "Lab rollback manifest missing: $manifestPath" }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.productionMutation -ne $false -or
        $manifest.target -ne 'Lab' -or
        $manifest.labRoot -ne $labRoot -or
        $manifest.productionRoot -ne $productionRoot -or
        $manifest.labDllTarget -ne $labTarget -or
        $manifest.labConfigTarget -ne $labConfigTarget) {
        Fail 'Rollback manifest is not the expected Lab-only deployment.'
    }

    Write-Step "rollback Lab backup=$resolved"
    $dllBackup = Join-Path $resolved 'BlueBrick.Lab.dll.orig'
    $configBackup = Join-Path $resolved 'appsettings.lab.json.orig'
    New-Item -ItemType Directory -Path (Split-Path $labTarget -Parent), (Split-Path $labConfigTarget -Parent) -Force | Out-Null
    if (Test-Path -LiteralPath $dllBackup) { Copy-Item -LiteralPath $dllBackup -Destination $labTarget -Force } elseif (-not $manifest.dllExisted -and (Test-Path -LiteralPath $labTarget)) { Remove-Item -LiteralPath $labTarget -Force }
    if (Test-Path -LiteralPath $configBackup) { Copy-Item -LiteralPath $configBackup -Destination $labConfigTarget -Force } elseif (-not $manifest.configExisted -and (Test-Path -LiteralPath $labConfigTarget)) { Remove-Item -LiteralPath $labConfigTarget -Force }

    $register = Join-Path $repo 'tools\register-lab-addin.ps1'
    Invoke-PowerShell $register @('-Mode','PerUser','-Unregister','-LabDllPath',$labTarget,'-BackupRoot',(Join-Path $resolved 'registry'))
    $registryBackups = @(Get-ChildItem -LiteralPath (Join-Path $resolved 'registry') -Filter '*.reg' -File -ErrorAction SilentlyContinue)
    foreach ($regFile in $registryBackups) {
        if ($regFile.Name -notlike 'clsid-PerUser-*' -and $regFile.Name -notlike 'addins-PerUser-*' -and $regFile.Name -notlike 'startup-*') { continue }
        & reg.exe import $regFile.FullName | Out-Null
        if ($LASTEXITCODE -ne 0) { Fail "Lab registry restore failed: $($regFile.FullName)" }
    }
    Write-Step 'rollback complete; Production paths were not targeted'
}

Assert-LabOnly
switch ($Action) {
    'doctor' {
        $doctor = Join-Path $repo 'scripts\repo-doctor.ps1'
        $args = @('-RepositoryRoot',$repo)
        if ($Json) { $args += '-Json' }
        Invoke-PowerShell $doctor $args
    }
    'build' {
        Invoke-Build $Configuration
    }
    'prepare' {
        Assert-Source
        $sourceHash = (Get-FileHash -LiteralPath $labSource -Algorithm SHA256).Hash
        $configHash = (Get-FileHash -LiteralPath $labConfigSource -Algorithm SHA256).Hash
        Write-Step "prepare Lab source=$labSource SHA256=$($sourceHash.Substring(0,16))..."
        Write-Step "prepare Lab config=$labConfigSource SHA256=$($configHash.Substring(0,16))..."
        Write-Step "prepare target=$labRoot; Production=$productionRoot (no changes made)"
    }
    'launch' {
        Assert-Source
        if (-not $Execute) {
            Write-Step "dry-run Lab launch: backup $labRoot, copy Lab DLL/config, register PerUser Lab, start SOLIDWORKS"
            Write-Step 'no changes made; re-run with -Execute after T0-T3 and rollback gates are green'
            break
        }
        $runRoot = New-LabBackup
        try {
            New-Item -ItemType Directory -Path $labRoot, (Split-Path $labConfigTarget -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $labSource -Destination $labTarget -Force
            Copy-Item -LiteralPath $labConfigSource -Destination $labConfigTarget -Force
            $register = Join-Path $repo 'tools\register-lab-addin.ps1'
            Invoke-PowerShell $register @('-Mode','PerUser','-LabDllPath',$labSource,'-StageRoot',$labRoot,'-BackupRoot',(Join-Path $runRoot 'registry'))
            Write-Step "Lab deployment verified: $labTarget"
            $sw = Resolve-SolidWorks
            if (-not $sw) { Fail 'SOLIDWORKS executable was not found on PATH; Lab deployment remains staged.' }
            Start-Process -FilePath $sw | Out-Null
            Write-Step 'SOLIDWORKS launch requested; use smoke to collect read-only Lab registration/process evidence'
        } catch {
            Write-Step "Lab launch failed; attempting automatic Lab-only rollback from $runRoot"
            Invoke-LabRollback $runRoot
            throw
        }
    }
    'smoke' {
        Assert-Source
        $validator = Join-Path $repo 'tools\validate-lab-live.ps1'
        Invoke-PowerShell $validator @('-LabDllPath',$labTarget)
        Write-Step 'smoke complete; this action is read-only and does not prove SOLIDWORKS behavioral acceptance by itself'
    }
    'rollback' {
        if (-not $Execute) {
            Write-Step "dry-run Lab rollback from $BackupRoot; no changes made"
            break
        }
        Invoke-LabRollback $BackupRoot
    }
}
