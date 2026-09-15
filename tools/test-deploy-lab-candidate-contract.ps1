$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$deployerSource = Join-Path $PSScriptRoot 'deploy-lab-candidate.ps1'
$sandbox = Join-Path ([IO.Path]::GetTempPath()) ('BlueBrick-DeployContract-' + [Guid]::NewGuid().ToString('N'))
try {
    $candidate = Join-Path $sandbox 'candidate'
    $target = Join-Path $sandbox 'destination'
    # Redirect only the temporary test copy; no real runtime path is read or written.
    $fixtureTools = Join-Path $sandbox 'tools'
    $fixtureScripts = Join-Path $sandbox 'scripts'
    New-Item -ItemType Directory -Path $fixtureTools,$fixtureScripts -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $repo 'scripts/bluebrick.ps1') -Destination $fixtureScripts
    $deployerText = Get-Content -LiteralPath $deployerSource -Raw
    $destinationAssignment = '$destinationRoot = ''C:\BlueBrickLab'''
    if (($deployerText.Split(@($destinationAssignment), [StringSplitOptions]::None)).Count -ne 2) { throw 'Expected one deployer destination assignment for fixture isolation.' }
    $script = Join-Path $fixtureTools 'deploy-lab-candidate.ps1'
    $deployerText.Replace($destinationAssignment, ('$destinationRoot = ''' + $target.Replace("'", "''") + "'")) | Set-Content -LiteralPath $script
    New-Item -ItemType Directory -Path $candidate,(Join-Path $candidate 'config'),(Join-Path $candidate 'AssistantWeb\dist'),(Join-Path $candidate 'SharedAI\catalog') -Force | Out-Null
    $files = @{
        'BlueBrick.Lab.dll'='dll'; 'config/appsettings.lab.json'='{"ConfigSchemaVersion":"1","Assistant":{"SharedAiModelIds":["nvidia-kimi-k3"]}}';
        'AssistantWeb/dist/index.html'='<meta name="bluebrick-build-id" content="test-build">';
        'AssistantWeb/dist/assistant-index.css'='css'; 'AssistantWeb/dist/assistant-web.js'='js';
        'SharedAI/catalog/providers.json'=(Get-Content (Join-Path $repo 'SharedAI/catalog/providers.json') -Raw); 'SharedAI/catalog/models.json'=(Get-Content (Join-Path $repo 'SharedAI/catalog/models.json') -Raw); 'SharedAI/catalog/routes.json'=(Get-Content (Join-Path $repo 'SharedAI/catalog/routes.json') -Raw)
    }
    foreach($pair in $files.GetEnumerator()) { $path=Join-Path $candidate $pair.Key; New-Item -ItemType Directory -Path (Split-Path $path -Parent) -Force | Out-Null; Set-Content -LiteralPath $path -Value $pair.Value -NoNewline }
    $runtime = [ordered]@{schema='bluebrick-lab-runtime-manifest.v2';product='BlueBrick';channel='Lab';buildId='test-build';frontendBuildId='test-build';configSchemaVersion='1';dll=@{sha256=(Get-FileHash (Join-Path $candidate 'BlueBrick.Lab.dll')).Hash};config=@{sha256=(Get-FileHash (Join-Path $candidate 'config/appsettings.lab.json')).Hash};frontend=@{artifacts=@{}};sharedAiCatalog=@{artifacts=@{}}}
    foreach($name in @('index.html','assistant-index.css','assistant-web.js')){$runtime.frontend.artifacts[$name]=@{sha256=(Get-FileHash (Join-Path $candidate "AssistantWeb/dist/$name")).Hash}}
    foreach($name in @('providers.json','models.json','routes.json')){$runtime.sharedAiCatalog.artifacts[$name]=@{sha256=(Get-FileHash (Join-Path $candidate "SharedAI/catalog/$name")).Hash}}
    $runtimePath=Join-Path $candidate 'runtime-manifest.json'; $runtime|ConvertTo-Json -Depth 8|Set-Content $runtimePath
    $manifestFiles=@($files.Keys|ForEach-Object{$path=Join-Path $candidate $_;[pscustomobject]@{relativePath=$_;sha256=(Get-FileHash $path).Hash}})+@([pscustomobject]@{relativePath='runtime-manifest.json';sha256=(Get-FileHash $runtimePath).Hash})
    [ordered]@{schema='bluebrick-frozen-lab-candidate.v1';target=$target;buildId='test-build';files=$manifestFiles}|ConvertTo-Json -Depth 6|Set-Content (Join-Path $candidate 'candidate-manifest.json')
    $output=& $script -CandidateRoot $candidate 2>&1
    if(($output|Out-String) -notmatch 'DRY_RUN_ONLY'){throw 'Valid fixture did not pass dry-run.'}
    function Refresh-FixtureHashes {
        $runtime.config.sha256 = (Get-FileHash (Join-Path $candidate 'config/appsettings.lab.json')).Hash
        $runtime.frontend.artifacts['index.html'].sha256 = (Get-FileHash (Join-Path $candidate 'AssistantWeb/dist/index.html')).Hash
        $runtime | ConvertTo-Json -Depth 8 | Set-Content $runtimePath
        foreach ($entry in $manifestFiles) { $entry.sha256 = (Get-FileHash (Join-Path $candidate $entry.relativePath)).Hash }
        [ordered]@{schema='bluebrick-frozen-lab-candidate.v1';target=$target;buildId='test-build';files=$manifestFiles} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $candidate 'candidate-manifest.json')
    }
    function Assert-Rejected([string]$pattern) {
        $rejected = $false
        try { & $script -CandidateRoot $candidate | Out-Null } catch { if ($_.Exception.Message -notmatch $pattern) { throw }; $rejected = $true }
        if (-not $rejected) { throw "Invalid fixture unexpectedly passed: $pattern" }
    }
    $indexPath = Join-Path $candidate 'AssistantWeb/dist/index.html'
    foreach ($html in @('<meta name="bluebrick-build-id" content="test<!--ignore-->-build">','<script>const tag = `<meta name="bluebrick-build-id" content="test-build">`;</script>','<meta data-name="bluebrick-build-id" content="test-build">','<meta name="bluebrick-build-id" data-content="test-build">','<!-- <meta name="bluebrick-build-id" content="test-build"> -->','<meta name="bluebrick-build-id" content="UNKNOWN">','<meta name="bluebrick-build-id" content="">','<meta name="bluebrick-build-id" content="other">','<meta name="bluebrick-build-id" content="test-build"><meta content="" name="bluebrick-build-id">')) {
        Set-Content -LiteralPath $indexPath -Value $html -NoNewline
        Refresh-FixtureHashes
        Assert-Rejected 'identity|meta'
    }
    Set-Content -LiteralPath $indexPath -Value $files['AssistantWeb/dist/index.html'] -NoNewline
    Set-Content -LiteralPath $indexPath -Value ($files['AssistantWeb/dist/index.html'] + '<!-- <meta name="bluebrick-build-id" content="ignored"> -->') -NoNewline
    Refresh-FixtureHashes
    if ((& $script -CandidateRoot $candidate | Out-String) -notmatch 'DRY_RUN_ONLY') { throw 'Commented duplicate prevented valid candidate.' }
    Set-Content -LiteralPath $indexPath -Value $files['AssistantWeb/dist/index.html'] -NoNewline
    $modelsPath = Join-Path $candidate 'SharedAI/catalog/models.json'
    foreach ($mutation in @('empty-roles','unknown-field','fractional-context','duplicate-role','unknown-capability','duplicate-property')) {
        $modelsFixture = ConvertFrom-Json $files['SharedAI/catalog/models.json']
        switch ($mutation) {
            'empty-roles' { $modelsFixture[0].roles = @() }
            'unknown-field' { $modelsFixture[0] | Add-Member -NotePropertyName unexpected -NotePropertyValue $true }
            'fractional-context' { $modelsFixture[0].contextLimit = 1.5 }
            'duplicate-role' { $modelsFixture[0].roles = @('general','general') }
            'unknown-capability' { $modelsFixture[0].capabilities | Add-Member -NotePropertyName unexpected -NotePropertyValue $true }
        }
        $modelJson = ConvertTo-Json -InputObject $modelsFixture -Depth 8
        if ($mutation -eq 'duplicate-property') { $modelJson = $modelJson -replace '"contextLimit":', '"contextLimit": 1, "contextLimit":' }
        Set-Content -LiteralPath $modelsPath -Value $modelJson
        $runtime.sharedAiCatalog.artifacts['models.json'].sha256 = (Get-FileHash $modelsPath).Hash
        Refresh-FixtureHashes
        Assert-Rejected 'catalog|model|role|field|property'
    }
    Set-Content -LiteralPath $modelsPath -Value $files['SharedAI/catalog/models.json'] -NoNewline
    $runtime.sharedAiCatalog.artifacts['models.json'].sha256 = (Get-FileHash $modelsPath).Hash
    $configPath = Join-Path $candidate 'config/appsettings.lab.json'
    foreach ($configJson in @('{','null','{"Assistant":{"SharedAiModelIds":null}}','{"Assistant":{"SharedAiModelIds":[]}}','{"Assistant":{"SharedAiModelIds":["unknown"]}}','{"Assistant":{"SharedAiModelIds":["nvidia-kimi-k3","nvidia-kimi-k3"]}}')) {
        Set-Content -LiteralPath $configPath -Value $configJson -NoNewline
        Refresh-FixtureHashes
        Assert-Rejected 'JSON|object|SharedAiModelIds|Invalid'
    }
    Set-Content -LiteralPath $configPath -Value $files['config/appsettings.lab.json'] -NoNewline
    foreach ($badBuildId in @($null, '', '   ')) {
        if ($null -eq $badBuildId) { $runtime.Remove('buildId') } else { $runtime.buildId = $badBuildId }
        Refresh-FixtureHashes
        Assert-Rejected 'build identity'
    }
    foreach ($badBuildIdJson in @('null', '[]', '["test-build"]', '["test-build","other"]', '{}', '{"value":"test-build"}', '123', 'true')) {
        $runtime.buildId = (ConvertFrom-Json ('{"buildId":' + $badBuildIdJson + '}')).buildId
        Refresh-FixtureHashes
        Assert-Rejected 'build identity must be a nonempty JSON string'
    }
    $runtime.buildId = 'test-build'
    Refresh-FixtureHashes
    if ((& $script -CandidateRoot $candidate | Out-String) -notmatch 'DRY_RUN_ONLY') { throw 'Restored string build identity did not pass.' }
    $runtime.schema = 'bluebrick-lab-runtime-manifest.v1'
    Refresh-FixtureHashes
    Assert-Rejected 'v2'
    $runtime.schema = 'bluebrick-lab-runtime-manifest.v2'
    $runtime.frontendBuildId = 'wrong'
    Refresh-FixtureHashes
    Assert-Rejected 'identity'
    $runtime.frontendBuildId = 'test-build'
    $runtime.sharedAiCatalog.artifacts['routes.json'].sha256 = 'wrong'
    Refresh-FixtureHashes
    Assert-Rejected 'routes.json'
    $runtime.sharedAiCatalog.artifacts['routes.json'].sha256 = (Get-FileHash (Join-Path $candidate 'SharedAI/catalog/routes.json')).Hash
    Refresh-FixtureHashes
    & {
        param($fixtureRoot, $sourceRepo)
        . (Join-Path $sourceRepo 'scripts/bluebrick.ps1') -LibraryOnly
        $labSource = Join-Path $fixtureRoot 'BlueBrick.Lab.dll'
        $labConfigSource = Join-Path $fixtureRoot 'config/appsettings.lab.json'
        $sourceFrontendRoot = Join-Path $fixtureRoot 'AssistantWeb/dist'
        $labFrontendSource = $sourceFrontendRoot
        $labCatalogSource = Join-Path $fixtureRoot 'SharedAI/catalog'
        $RunId = 'test-build'
        Assert-Source
        $RunId = 'incorrect-run-id'
        $rejected = $false
        try { Assert-Source } catch { if ($_.Exception.Message -notmatch 'identity') { throw }; $rejected = $true }
        if (-not $rejected) { throw 'Wrapper accepted mismatched RunId.' }
    } $candidate $repo
    & {
        param($fixtureSandbox, $sourceRepo)
        . (Join-Path $sourceRepo 'scripts/bluebrick.ps1') -LibraryOnly
        $lifecycleRoot = Join-Path $fixtureSandbox 'lifecycle'
        $labRoot = Join-Path $lifecycleRoot 'lab'
        $productionRoot = Join-Path $lifecycleRoot 'production-unused'
        $labTarget = Join-Path $labRoot 'BlueBrick.Lab.dll'
        $labConfigTarget = Join-Path $labRoot 'config/appsettings.lab.json'
        $labFrontendTarget = Join-Path $labRoot 'AssistantWeb/dist'
        $labRuntimeManifestTarget = Join-Path $labRoot 'runtime-manifest.json'
        $labCatalogTarget = Join-Path $labRoot 'SharedAI/catalog'
        $labCatalogSource = Join-Path $lifecycleRoot 'source/catalog'
        $BackupRoot = Join-Path $labRoot 'backups/good'
        $outsideTarget = Join-Path $lifecycleRoot 'outside-lab'
        New-Item -ItemType Directory -Path $labCatalogTarget,$labCatalogSource,$outsideTarget,(Split-Path $labConfigTarget -Parent),$labFrontendTarget -Force | Out-Null
        foreach ($path in @($labTarget,$labConfigTarget,$labRuntimeManifestTarget,(Join-Path $labFrontendTarget 'index.html'),(Join-Path $labCatalogTarget 'models.json'),(Join-Path $labCatalogSource 'models.json'),(Join-Path $outsideTarget 'sentinel.bin'))) {
            Set-Content -LiteralPath $path -Value ('before:' + $path) -NoNewline
        }
        # If a rejection regresses, stop before any external registration process.
        function Invoke-PowerShell { throw 'Fixture must never invoke registration.' }
        function Get-FixtureSnapshot([string]$root) {
            foreach ($item in Get-ChildItem -LiteralPath $root -Force | Sort-Object FullName) {
                if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { 'junction:' + $item.FullName; continue }
                if ($item.PSIsContainer) { 'directory:' + $item.FullName; Get-FixtureSnapshot $item.FullName }
                else { 'file:' + $item.FullName + ':' + (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash }
            }
        }
        function Assert-JunctionRejected([string]$label, [scriptblock]$operation, [string]$junction) {
            $before = @(Get-FixtureSnapshot $lifecycleRoot)
            $rejected = $false
            try { & $operation | Out-Null } catch {
                if ($_.Exception.Message -notlike 'Reparse point is not permitted:*' -or -not $_.Exception.Message.EndsWith($junction, [StringComparison]::OrdinalIgnoreCase)) { throw }
                $rejected = $true
            }
            if (-not $rejected) { throw "$label accepted a junction." }
            $after = @(Get-FixtureSnapshot $lifecycleRoot)
            if (@(Compare-Object $before $after).Count) { throw "$label changed fixture entries or bytes before rejecting." }
            Write-Host "lifecycle junction fixture: PASS ($label; no entries or bytes changed)"
        }
        function Remove-FixtureJunction([string]$junction) {
            $full = [IO.Path]::GetFullPath($junction)
            if (-not $full.StartsWith(([IO.Path]::GetFullPath($lifecycleRoot).TrimEnd('\') + '\'), [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe junction unlink boundary.' }
            Assert-NoReparsePoint (Split-Path $full -Parent)
            $item = Get-Item -LiteralPath $full -Force
            if (-not ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Expected fixture junction before unlink.' }
            # Non-recursive deletion unlinks the junction itself, never its target.
            [IO.Directory]::Delete($full)
        }
        $null = New-LabBackup
        if (-not (Test-Path -LiteralPath (Join-Path $BackupRoot 'SharedAI.catalog.orig/models.json'))) { throw 'Valid catalog backup failed.' }
        Set-Content -LiteralPath $labTarget -Value 'current DLL differs from backup; rollback must reject before copying' -NoNewline
        $normalCatalogTarget = $labCatalogTarget
        $normalBackupRoot = $BackupRoot

        $junction = Join-Path $labRoot 'redirected'
        New-Item -ItemType Junction -Path $junction -Target $outsideTarget | Out-Null
        try {
            $labCatalogTarget = Join-Path $junction 'catalog'
            Assert-JunctionRejected 'backup catalog ancestor' { New-LabBackup } $junction
            Assert-JunctionRejected 'replacement target ancestor' { Set-LabCatalog $labCatalogSource } $junction
            Assert-JunctionRejected 'rollback target ancestor' { Invoke-LabRollback $BackupRoot } $junction
        } finally { $labCatalogTarget = $normalCatalogTarget; Remove-FixtureJunction $junction }

        $junction = Join-Path $labRoot 'redirected-backup'
        New-Item -ItemType Junction -Path $junction -Target $outsideTarget | Out-Null
        try {
            $BackupRoot = Join-Path $junction 'backup'
            Assert-JunctionRejected 'backup destination ancestor' { New-LabBackup } $junction
            Assert-JunctionRejected 'rollback backup ancestor' { Invoke-LabRollback $BackupRoot } $junction
        } finally { $BackupRoot = $normalBackupRoot; Remove-FixtureJunction $junction }

        $junction = Join-Path $lifecycleRoot 'redirected-source'
        New-Item -ItemType Junction -Path $junction -Target $outsideTarget | Out-Null
        try { Assert-JunctionRejected 'replacement source ancestor' { Set-LabCatalog (Join-Path $junction 'catalog') } $junction }
        finally { Remove-FixtureJunction $junction }

        $junction = Join-Path $labCatalogTarget 'nested-junction'
        New-Item -ItemType Junction -Path $junction -Target $outsideTarget | Out-Null
        try {
            Assert-JunctionRejected 'backup catalog descendant' { New-LabBackup } $junction
            Assert-JunctionRejected 'replacement target descendant' { Set-LabCatalog $labCatalogSource } $junction
        } finally { Remove-FixtureJunction $junction }

        $junction = Join-Path $BackupRoot 'SharedAI.catalog.orig/nested-junction'
        New-Item -ItemType Junction -Path $junction -Target $outsideTarget | Out-Null
        try { Assert-JunctionRejected 'rollback backup catalog descendant' { Invoke-LabRollback $BackupRoot } $junction }
        finally { Remove-FixtureJunction $junction }

        $junction = Join-Path $labCatalogSource 'nested-junction'
        New-Item -ItemType Junction -Path $junction -Target $outsideTarget | Out-Null
        try { Assert-JunctionRejected 'replacement source descendant' { Set-LabCatalog $labCatalogSource } $junction }
        finally { Remove-FixtureJunction $junction }

        Set-LabCatalog $labCatalogSource
        if ((Get-FileHash -LiteralPath (Join-Path $labCatalogTarget 'models.json')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $labCatalogSource 'models.json')).Hash) { throw 'Valid catalog replacement failed.' }
        Set-LabCatalog (Join-Path $BackupRoot 'SharedAI.catalog.orig')
        if ((Get-FileHash -LiteralPath (Join-Path $labCatalogTarget 'models.json')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $BackupRoot 'SharedAI.catalog.orig/models.json')).Hash) { throw 'Valid catalog restore failed.' }
        Write-Host 'lifecycle catalog backup/replacement/restore controls: PASS'
    } $sandbox $repo
    Remove-Item -LiteralPath (Join-Path $candidate 'SharedAI/catalog/routes.json')
    try { & $script -CandidateRoot $candidate 2>$null; throw 'Missing opted-in catalog unexpectedly passed.' } catch { if($_.Exception.Message -notmatch 'routes.json'){throw} }
    Write-Host 'deploy candidate contract fixtures: PASS'
} finally {
    $resolvedSandbox = [IO.Path]::GetFullPath($sandbox)
    $tempBoundary = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolvedSandbox.StartsWith($tempBoundary, [StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolvedSandbox -Leaf) -notlike 'BlueBrick-DeployContract-*') { throw 'Unsafe fixture cleanup boundary.' }
    if (Test-Path -LiteralPath $resolvedSandbox) {
        # Refuse recursive cleanup if an unlink failed or any unexpected link appeared.
        & {
            param($root, $sourceRepo)
            . (Join-Path $sourceRepo 'scripts/bluebrick.ps1') -LibraryOnly
            Assert-NoReparseTree $root
        } $resolvedSandbox $repo
        Remove-Item -LiteralPath $resolvedSandbox -Recurse -Force
    }
}
