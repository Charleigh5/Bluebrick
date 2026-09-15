[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$CandidateRoot,
    [switch]$Execute
)
$ErrorActionPreference = 'Stop'
$candidateExecute = $Execute
. (Join-Path $PSScriptRoot '..\scripts\bluebrick.ps1') -LibraryOnly
$Execute = $candidateExecute
function Assert-DestinationUnchanged($Item) {
    Assert-NoReparsePoint $Item.destination
    $observed = if (Test-Path -LiteralPath $Item.destination) { (Get-FileHash -LiteralPath $Item.destination -Algorithm SHA256).Hash } else { $null }
    if ($observed -ne $Item.previousSha256) { throw "Lab destination changed since preservation: $($Item.relativePath)" }
}
Assert-NoReparsePoint $CandidateRoot
$candidate = (Resolve-Path -LiteralPath $CandidateRoot).Path.TrimEnd('\')
$destinationRoot = 'C:\BlueBrickLab'
Assert-NoReparsePoint $destinationRoot
$manifestPath = Join-Path $candidate 'candidate-manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.schema -ne 'bluebrick-frozen-lab-candidate.v1' -or $manifest.target -ne $destinationRoot) { throw 'Expected an exact Lab candidate manifest.' }
if ($candidate -ieq $destinationRoot -or $candidate -ieq 'C:\BlueBrick' -or $candidate.StartsWith($destinationRoot+'\', [StringComparison]::OrdinalIgnoreCase) -or $candidate.StartsWith('C:\BlueBrick\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Candidate must be isolated from runtime outputs.' }
$seen = @{}
$plan = foreach ($entry in $manifest.files) {
    $relative = [string]$entry.relativePath
    if ([string]::IsNullOrWhiteSpace($relative) -or [IO.Path]::IsPathRooted($relative) -or $relative -match '(^|[\\/])\.{1,2}([\\/]|$)' -or $relative -match ':' -or @($relative -split '[\\/]' | Where-Object { $_ -eq '' -or $_ -match '[. ]$' }).Count) { throw 'Unsafe candidate path.' }
    $source = [IO.Path]::GetFullPath((Join-Path $candidate $relative))
    $destination = [IO.Path]::GetFullPath((Join-Path $destinationRoot $relative))
    if (-not $source.StartsWith($candidate+'\',[StringComparison]::OrdinalIgnoreCase) -or -not $destination.StartsWith($destinationRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Path escapes its boundary.' }
    if ($seen.ContainsKey($destination.ToLowerInvariant())) { throw 'Duplicate normalized destination.' }
    $seen[$destination.ToLowerInvariant()] = $true
    Assert-NoReparsePoint $source
    Assert-NoReparsePoint $destination
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Candidate hash mismatch: $relative" }
    [pscustomobject]@{relativePath=$relative;source=$source;destination=$destination;sha256=$entry.sha256;previousSha256=$(if(Test-Path -LiteralPath $destination){(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash}else{$null})}
}
foreach ($required in @('bluebrick.lab.dll','config/appsettings.lab.json','assistantweb/dist/index.html','assistantweb/dist/assistant-index.css','assistantweb/dist/assistant-web.js','runtime-manifest.json')) {
    if (-not @($plan | Where-Object { $_.relativePath.Replace('\','/').ToLowerInvariant() -eq $required }).Count) { throw "Required artifact missing: $required" }
}
# Validate the frozen runtime contract before even dry-run success or backup creation.
$runtime = Get-Content -LiteralPath (Join-Path $candidate 'runtime-manifest.json') -Raw | ConvertFrom-Json
if ($runtime.schema -cne 'bluebrick-lab-runtime-manifest.v2' -or $runtime.product -cne 'BlueBrick' -or $runtime.channel -cne 'Lab') { throw 'Expected Lab runtime manifest v2.' }
if ($runtime.buildId -isnot [string] -or [string]::IsNullOrWhiteSpace($runtime.buildId)) { throw 'Frozen candidate runtime build identity must be a nonempty JSON string.' }
$actualBuildId = Get-FrontendBuildId (Join-Path $candidate 'AssistantWeb\dist')
if ($actualBuildId -cne $runtime.buildId -or $actualBuildId -cne $runtime.frontendBuildId -or $actualBuildId -cne $manifest.buildId) { throw 'Frozen candidate build identity mismatch.' }
$config = Assert-ConfigCatalog (Join-Path $candidate 'config\appsettings.lab.json') (Join-Path $candidate 'SharedAI\catalog')
if ([string]::IsNullOrWhiteSpace([string]$config.ConfigSchemaVersion) -or [string]$config.ConfigSchemaVersion -cne [string]$runtime.configSchemaVersion) { throw 'Runtime config schema mismatch.' }
$runtimeHashes = @{
    'BlueBrick.Lab.dll'=$runtime.dll.sha256
    'config/appsettings.lab.json'=$runtime.config.sha256
}
foreach ($name in @('index.html','assistant-index.css','assistant-web.js')) { $runtimeHashes["AssistantWeb/dist/$name"]=$runtime.frontend.artifacts.$name.sha256 }
foreach ($name in @('providers.json','models.json','routes.json')) { $runtimeHashes["SharedAI/catalog/$name"]=$runtime.sharedAiCatalog.artifacts.$name.sha256 }
foreach ($relative in $runtimeHashes.Keys) {
    $entry = @($plan | Where-Object { $_.relativePath.Replace('\','/') -ieq $relative })
    if ($entry.Count -ne 1 -or [string]::IsNullOrWhiteSpace([string]$runtimeHashes[$relative]) -or $runtimeHashes[$relative] -ine $entry[0].sha256) { throw "Runtime artifact contract mismatch: $relative" }
}
if (-not $Execute) {
    [pscustomobject]@{status='DRY_RUN_ONLY';buildId=$manifest.buildId;target=$destinationRoot;fileCount=@($plan).Count;plan=$plan;registration='Separate exact Lab registration; no registry changes here';rollback='Before-image backup and rollback manifest written on execution'} | ConvertTo-Json -Depth 6
    return
}
if (Get-Process SLDWORKS -ErrorAction SilentlyContinue) { throw 'Close all SOLIDWORKS instances before replacing Lab runtime files. This tool will not close or kill them.' }
$backup = Join-Path (Split-Path $candidate -Parent) ('lab-before-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-ffff'))
Assert-NoReparsePoint $backup
New-Item -ItemType Directory -Path $backup | Out-Null
$rollback = @()
foreach ($item in $plan) {
    $saved = Join-Path $backup $item.relativePath
    Assert-DestinationUnchanged $item
    if ($item.previousSha256) {
        New-Item -ItemType Directory -Path (Split-Path $saved -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $item.destination -Destination $saved
        if ((Get-FileHash -LiteralPath $saved -Algorithm SHA256).Hash -ne $item.previousSha256) { throw 'Backup verification failed; no runtime file has been replaced.' }
    }
    $rollback += [pscustomobject]@{path=$item.destination;backup=$saved;existed=[bool]$item.previousSha256;beforeSha256=$item.previousSha256;candidateSha256=$item.sha256}
}
$rollback | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $backup 'rollback-manifest.json') -Encoding UTF8
# Recheck after the complete backup, before the first runtime write.
if (Get-Process SLDWORKS -ErrorAction SilentlyContinue) { throw 'SOLIDWORKS started during preparation; no runtime writes performed.' }
foreach ($item in $plan) {
    Assert-NoReparsePoint $item.source
    Assert-DestinationUnchanged $item
    if ((Get-FileHash -LiteralPath $item.source -Algorithm SHA256).Hash -ne $item.sha256) { throw 'Candidate changed after verification; no runtime writes performed.' }
}
try {
    foreach ($item in $plan) {
        Assert-NoReparsePoint $item.source
        Assert-DestinationUnchanged $item
        New-Item -ItemType Directory -Path (Split-Path $item.destination -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $item.source -Destination $item.destination -Force
        if ((Get-FileHash -LiteralPath $item.destination -Algorithm SHA256).Hash -ne $item.sha256) { throw "Deployed hash mismatch: $($item.relativePath)" }
    }
} catch {
    # Preserve all partial-state evidence. Rollback is explicit from the retained manifest.
    throw "Lab copy failed; stop loading Lab and restore exact before-images from $backup. Original error: $($_.Exception.Message)"
}
[pscustomobject]@{status='FILES_DEPLOYED_NOT_LOADED';buildId=$manifest.buildId;target=$destinationRoot;backup=$backup;registrationPerformed=$false;runtimeAcceptance='NOT_VERIFIED'} | ConvertTo-Json
