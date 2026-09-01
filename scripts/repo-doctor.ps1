[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [switch]$Json
)

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) '..'
}

$repo = (Resolve-Path -LiteralPath $RepositoryRoot).Path
$canonical = 'C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick'
$productionRuntime = 'C:\BlueBrick'
$labRuntime = 'C:\BlueBrickLab'
$recoveryRoot = 'C:\VIRA-Recovery\BlueBrick'
$findings = Join-Path $repo '.superpowers\sdd\BLUEBRICK_AEON_UI_RECOVERY_SPRINT_PLAN_2026-08-28\TASK3_INTEGRATED_FINDINGS.md'

function Get-GitText {
    param([string[]]$Arguments)

    $result = & git -C $repo @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return (($result -join "`n").Trim())
}

function Get-GitLines {
    param([string[]]$Arguments)

    @(& git -C $repo @Arguments 2>$null | Where-Object { $_ -ne $null -and $_ -ne '' })
}

$checkout = Get-GitText @('rev-parse', '--show-toplevel')
$branch = Get-GitText @('branch', '--show-current')
$head = Get-GitText @('rev-parse', 'HEAD')
$upstream = Get-GitText @('rev-parse', '--abbrev-ref', '--symbolic-full-name', '@{u}')
if ([string]::IsNullOrWhiteSpace($upstream)) { $upstream = 'NONE' }

$ahead = 'N/A'
$behind = 'N/A'
$originCounts = Get-GitText @('rev-list', '--left-right', '--count', 'origin/main...HEAD')
if ($originCounts -match '^\s*(\d+)\s+(\d+)\s*$') {
    $behind = $Matches[1]
    $ahead = $Matches[2]
}

$trackedDirt = @(Get-GitLines @('status', '--porcelain=v1', '--untracked-files=no')).Count
$usefulUntracked = @(Get-GitLines @('ls-files', '--others', '--exclude-standard'))
$unignoredNodeModules = @($usefulUntracked | Where-Object { $_ -like 'AssistantWeb/node_modules/*' }).Count

$worktreeLines = @(Get-GitLines @('worktree', 'list', '--porcelain'))
$registeredWorktrees = @($worktreeLines | Where-Object { $_ -like 'worktree *' }).Count
$worktreePaths = @($worktreeLines | Where-Object { $_ -like 'worktree *' } | ForEach-Object { $_.Substring(9) })
$dirtyWorktrees = 0
foreach ($worktreePath in $worktreePaths) {
    if (@(& git -C $worktreePath status --porcelain=v1 2>$null | Where-Object { $_ -ne $null -and $_ -ne '' }).Count -gt 0) {
        $dirtyWorktrees++
    }
}

$gitDir = Get-GitText @('rev-parse', '--git-dir')
$activeOperation = $false
if ($gitDir) {
    foreach ($marker in @('MERGE_HEAD', 'CHERRY_PICK_HEAD', 'REVERT_HEAD', 'rebase-apply', 'rebase-merge')) {
        if (Test-Path -LiteralPath (Join-Path $gitDir $marker)) {
            $activeOperation = $true
            break
        }
    }
}

$canonicalPass = $false
if ($checkout) {
    $canonicalPass = [IO.Path]::GetFullPath($checkout).TrimEnd('\') -ieq [IO.Path]::GetFullPath($canonical).TrimEnd('\')
}
$development = if (-not $canonicalPass -or $activeOperation) {
    'BLOCKED'
} elseif ($trackedDirt -gt 0 -or $usefulUntracked.Count -gt 0) {
    'ALLOWED_WITH_WARNINGS'
} else {
    'ALLOWED'
}

$cleanup = if (-not $canonicalPass -or $activeOperation) {
    'BLOCKED'
} elseif ($trackedDirt -gt 0 -or $usefulUntracked.Count -gt 0 -or $unignoredNodeModules -gt 0) {
    'PRESERVATION_REQUIRED'
} else {
    'SAFE'
}

function Get-ToolVersion([string]$name) {
    $command = Get-Command $name -ErrorAction SilentlyContinue
    if (-not $command) { return $null }
    try { return (& $command.Source --version 2>$null | Select-Object -First 1).ToString().Trim() } catch { return $null }
}

function Get-ArtifactState([string]$sourcePath, [string]$targetPath) {
    $sourceExists = Test-Path -LiteralPath $sourcePath
    $targetExists = Test-Path -LiteralPath $targetPath
    $sourceHash = if ($sourceExists) { (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash } else { $null }
    $targetHash = if ($targetExists) { (Get-FileHash -LiteralPath $targetPath -Algorithm SHA256).Hash } else { $null }
    $state = if (-not $sourceExists -or -not $targetExists) { 'UNKNOWN' } elseif ($sourceHash -eq $targetHash) { 'READY' } else { 'DEGRADED' }
    [pscustomobject]@{ state = $state; source = $sourcePath; target = $targetPath; sourceExists = $sourceExists; targetExists = $targetExists; hashMatch = ($sourceExists -and $targetExists -and $sourceHash -eq $targetHash); sourceHash = $sourceHash; targetHash = $targetHash }
}

$msbuild = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
if (-not $msbuild) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $msbuildPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2>$null | Select-Object -First 1
        if ($msbuildPath) { $msbuild = Get-Item -LiteralPath $msbuildPath -ErrorAction SilentlyContinue }
    }
}
$vstest = Get-Command vstest.console.exe -ErrorAction SilentlyContinue
if (-not $vstest) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $vstestPath = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe' 2>$null | Select-Object -First 1
        if ($vstestPath -and (Test-Path -LiteralPath $vstestPath)) { $vstest = Get-Item -LiteralPath $vstestPath -ErrorAction SilentlyContinue }
    }
}
$nodeVersion = Get-ToolVersion 'node'
$npmVersion = Get-ToolVersion 'npm'
$webViewPath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft\EdgeWebView\Application'
$webViewPresent = Test-Path -LiteralPath $webViewPath
$releaseArtifact = Get-ArtifactState (Join-Path $repo 'bin\Release\BlueBrick.dll') (Join-Path $productionRuntime 'BlueBrick.dll')
$labArtifact = Get-ArtifactState (Join-Path $repo 'bin\Lab\BlueBrick.Lab.dll') (Join-Path $labRuntime 'BlueBrick.Lab.dll')
$releaseConfig = Test-Path -LiteralPath (Join-Path $productionRuntime 'config\appsettings.json')
$labConfig = Test-Path -LiteralPath (Join-Path $labRuntime 'config\appsettings.lab.json')
$portStates = @()
foreach ($port in @(17178, 17179)) {
    $listeners = @(Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue)
    $portStates += [pscustomobject]@{ port = $port; state = if ($listeners.Count -eq 0) { 'READY' } else { 'DEGRADED' }; owners = @($listeners | Select-Object -ExpandProperty OwningProcess -Unique) }
}
$registryStates = @()
foreach ($root in @('HKCU:\Software\ViraInsight\BlueBrick\Settings','HKCU:\Software\ViraInsight\BlueBrickLab\Settings','HKCU:\Software\SolidWorks\Addins\{C56E0AFF-0BD3-4364-90CB-1A581046CD7D}','HKCU:\Software\SolidWorks\Addins\{251d6df2-3e7b-42ef-b7fc-175e1fdcb4c5}')) {
    $registryStates += [pscustomobject]@{ path = $root; state = if (Test-Path -LiteralPath $root) { 'READY' } else { 'UNKNOWN' } }
}
$configJson = $null
try { $configJson = Get-Content -LiteralPath (Join-Path $repo 'config\appsettings.lab.json') -Raw | ConvertFrom-Json } catch { }
$pdmState = if ($configJson -and $configJson.Pdm -and $configJson.Pdm.AllowAssistantReadOnlySearch -and $configJson.AssistantTools.EnablePdmSearch) { 'DEGRADED' } else { 'NOT_APPLICABLE' }
$providerStates = @()
foreach ($name in @('OPENAI_API_KEY','NVIDIA_API_KEY')) {
    $providerStates += [pscustomobject]@{ name = $name; state = if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($name))) { 'UNKNOWN' } else { 'READY' } }
}
$processStates = [pscustomobject]@{
    solidworks = if (@(Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue).Count -gt 0) { 'READY' } else { 'UNKNOWN' }
    bluebrick = if (@(Get-Process -Name BlueBrick* -ErrorAction SilentlyContinue).Count -gt 0) { 'READY' } else { 'UNKNOWN' }
}
$overallState = if (-not $canonicalPass -or $activeOperation) { 'BLOCKED' } elseif (-not $msbuild -or -not $nodeVersion) { 'DEGRADED' } else { 'READY' }
$doctor = [pscustomobject]@{
    schema = 'bluebrick-doctor.v2'
    checkedUtc = [DateTime]::UtcNow.ToString('o')
    state = $overallState
    repository = [pscustomobject]@{ canonical = $canonicalPass; checkout = $checkout; branch = $branch; head = $head; upstream = $upstream; ahead = $ahead; behind = $behind; trackedDirty = $trackedDirt; usefulUntracked = $usefulUntracked.Count; cleanup = $cleanup; activeGitOperation = $activeOperation }
    worktrees = [pscustomobject]@{ registered = $registeredWorktrees; dirty = $dirtyWorktrees }
    tools = [pscustomobject]@{ msbuild = if ($msbuild) { $msbuild.FullName } else { $null }; vstest = if ($vstest) { $vstest.FullName } else { $null }; node = $nodeVersion; npm = $npmVersion; webview2Runtime = if ($webViewPresent) { 'READY' } else { 'UNKNOWN' } }
    artifacts = [pscustomobject]@{ release = $releaseArtifact; lab = $labArtifact; productionConfig = $releaseConfig; labConfig = $labConfig }
    ports = $portStates
    registry = $registryStates
    processes = $processStates
    capabilities = [pscustomobject]@{ pdmReadOnly = $pdmState; providers = $providerStates }
    recovery = [pscustomobject]@{ root = $recoveryRoot; present = (Test-Path -LiteralPath $recoveryRoot); findingsPresent = (Test-Path -LiteralPath $findings) }
}

if ($Json) {
    $doctor | ConvertTo-Json -Depth 8
    if ($overallState -eq 'BLOCKED') { exit 2 }
    exit 0
}

Write-Output 'BLUEBRICK REPOSITORY DOCTOR'
Write-Output ''
Write-Output ('Canonical source ........ ' + $(if ($canonicalPass) { 'PASS' } else { 'FAIL' }))
Write-Output ('Current checkout ........ ' + $checkout)
Write-Output ('Branch .................. ' + $branch)
Write-Output ('HEAD .................... ' + $head)
Write-Output ('Upstream ................ ' + $upstream)
Write-Output ('Ahead origin/main ....... ' + $ahead)
Write-Output ('Behind origin/main ...... ' + $behind)
Write-Output ('Tracked dirt ............ ' + $trackedDirt)
Write-Output ('Useful untracked ........ ' + $usefulUntracked.Count)
Write-Output ('Unignored node_modules .. ' + $unignoredNodeModules)
Write-Output ('Registered worktrees .... ' + $registeredWorktrees)
Write-Output ('Dirty worktrees ......... ' + $dirtyWorktrees)
Write-Output ('Production runtime ...... ' + $(if (Test-Path -LiteralPath $productionRuntime) { 'PRESENT' } else { 'MISSING' }))
Write-Output ('Lab runtime ............. ' + $(if (Test-Path -LiteralPath $labRuntime) { 'PRESENT' } else { 'MISSING' }))
Write-Output ('Recovery root ........... ' + $(if (Test-Path -LiteralPath $recoveryRoot) { 'PRESENT' } else { 'MISSING' }))
Write-Output ('Task3 findings .......... ' + $(if (Test-Path -LiteralPath $findings) { 'PRESENT' } else { 'MISSING' }))
Write-Output ('MSBuild ................. ' + $(if ($msbuild) { $msbuild.FullName } else { 'MISSING' }))
Write-Output ('VSTest .................. ' + $(if ($vstest) { $vstest.FullName } else { 'MISSING' }))
Write-Output ('Node/npm ................ ' + $(if ($nodeVersion -and $npmVersion) { 'READY' } else { 'DEGRADED' }))
Write-Output ('WebView2 Runtime ....... ' + $(if ($webViewPresent) { 'READY' } else { 'UNKNOWN' }))
Write-Output ('Release artifact ........ ' + $releaseArtifact.state)
Write-Output ('Lab artifact ............ ' + $labArtifact.state)
Write-Output ('Lab config .............. ' + $(if ($labConfig) { 'READY' } else { 'UNKNOWN' }))
Write-Output ('PDM capability .......... ' + $pdmState + ' (no login attempted)')
Write-Output ''
Write-Output ('DEVELOPMENT: ' + $development)
Write-Output ('CLEANUP: ' + $cleanup)
Write-Output ('OVERALL: ' + $overallState)
