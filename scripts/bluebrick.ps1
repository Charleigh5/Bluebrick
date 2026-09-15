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
    [string]$RunId = '',
    [switch]$Execute,
    [switch]$LibraryOnly,
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
$sourceFrontendRoot = Join-Path $repo 'AssistantWeb\dist'
$labFrontendSource = Join-Path $repo 'bin\Lab\AssistantWeb\dist'
$labFrontendTarget = Join-Path $labRoot 'AssistantWeb\dist'
$labRuntimeManifestTarget = Join-Path $labRoot 'runtime-manifest.json'
$labCatalogSource = Join-Path $repo 'SharedAI\catalog'
$labCatalogTarget = Join-Path $labRoot 'SharedAI\catalog'
$requiredCatalogArtifacts = @('providers.json','models.json','routes.json')
$requiredFrontendArtifacts = @('index.html','assistant-index.css','assistant-web.js')

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
function Assert-NoReparsePoint([string]$Path) {
    $current = [IO.Path]::GetFullPath($Path)
    while ($current) {
        $item = Get-Item -LiteralPath $current -Force -ErrorAction SilentlyContinue
        if ($item -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Reparse point is not permitted: $current" }
        $parent = [IO.Directory]::GetParent($current)
        $current = if ($parent) { $parent.FullName } else { $null }
    }
}
function Assert-NoReparseTree([string]$Path) {
    Assert-NoReparsePoint $Path
    if (Test-Path -LiteralPath $Path -PathType Container) {
        # Check each child before descending; never enumerate through a junction.
        foreach ($child in Get-ChildItem -LiteralPath $Path -Force) { Assert-NoReparseTree $child.FullName }
    }
}
function Set-LabCatalog([string]$Source) {
    Assert-LabPath $labCatalogTarget 'LabCatalogTarget'
    Assert-NoReparseTree $labCatalogTarget
    if (-not [string]::IsNullOrWhiteSpace($Source)) {
        Assert-NoReparseTree $Source
        if (-not (Test-Path -LiteralPath $Source -PathType Container)) { Fail 'Catalog replacement source is missing.' }
    }
    if (Test-Path -LiteralPath $labCatalogTarget) { Remove-Item -LiteralPath $labCatalogTarget -Recurse -Force }
    if (-not [string]::IsNullOrWhiteSpace($Source)) {
        New-Item -ItemType Directory -Path (Split-Path $labCatalogTarget -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $Source -Destination $labCatalogTarget -Recurse -Force
    }
}
function Assert-FrontendTriplet([string]$root, [string]$label) {
    if (-not (Test-Path -LiteralPath $root -PathType Container)) {
        Fail "LAB_PACKAGE_INCOMPLETE: $label directory missing: $root"
    }
    $actual = @(Get-ChildItem -LiteralPath $root -File | ForEach-Object { $_.Name } | Sort-Object)
    $expected = @($requiredFrontendArtifacts | Sort-Object)
    $difference = @(Compare-Object -ReferenceObject $expected -DifferenceObject $actual)
    if ($difference.Count -ne 0) {
        Fail "LAB_PACKAGE_INCOMPLETE: $label must contain exactly $($expected -join ', '); observed $($actual -join ', ')."
    }
}
function Assert-FrontendParity([string]$leftRoot, [string]$rightRoot, [string]$transition) {
    Assert-FrontendTriplet $leftRoot "$transition source"
    Assert-FrontendTriplet $rightRoot "$transition target"
    foreach ($name in $requiredFrontendArtifacts) {
        $left = (Get-FileHash -LiteralPath (Join-Path $leftRoot $name) -Algorithm SHA256).Hash
        $right = (Get-FileHash -LiteralPath (Join-Path $rightRoot $name) -Algorithm SHA256).Hash
        if ($left -ne $right) {
            Fail "LAB_FRONTEND_HASH_MISMATCH: $transition diverged for $name ($left != $right)."
        }
    }
}
function Get-FrontendBuildId([string]$root, [string]$expected = '') {
    $html = Get-Content -LiteralPath (Join-Path $root 'index.html') -Raw
    $identities = @()
    foreach ($tag in [regex]::Matches($html, '(?is)<!--.*?(?:-->|$)|<(script|style|textarea|title|xmp|iframe|noembed|noframes|template)\b[^>]*>.*?(?:</\1\s*>|$)|<plaintext\b.*$|<[a-z][a-z0-9:-]*(?=\s|/?>)(?:[^>"'']|"[^"]*"|''[^'']*'')*>')) {
        if ($tag.Value -notmatch '(?i)^<meta(?=\s|/?>)') { continue }
        $attributes = [regex]::Matches($tag.Value.Substring(5, $tag.Length - 6), '(?s)(?<name>[^\s=/>]+)(?:\s*=\s*(?:"(?<value>[^"]*)"|''(?<value>[^'']*)''|(?<value>[^\s>]+)))?')
        $names = @($attributes | Where-Object { $_.Groups['name'].Value -ieq 'name' })
        if (@($names | Where-Object { [Net.WebUtility]::HtmlDecode($_.Groups['value'].Value) -ieq 'bluebrick-build-id' }).Count) {
            $contents = @($attributes | Where-Object { $_.Groups['name'].Value -ieq 'content' })
            if ($names.Count -ne 1 -or $contents.Count -ne 1) { Fail 'Duplicated or missing build identity attributes.' }
            $identities += [Net.WebUtility]::HtmlDecode($contents[0].Groups['value'].Value).Trim()
        }
    }
    if ($identities.Count -ne 1) { Fail 'Expected exactly one bluebrick-build-id meta.' }
    $id = $identities[0]
    if ([string]::IsNullOrWhiteSpace($id) -or $id -ieq 'UNKNOWN' -or ($expected -and $id -cne $expected)) { Fail 'Frontend build identity is empty, UNKNOWN, or mismatched.' }
    return $id
}
# Mirror SharedAI/generated/csharp/SharedCatalog.cs validation without a Node dependency.
function Read-ContractJson([string]$path) {
    $raw = Get-Content -LiteralPath $path -Raw
    $value = ConvertFrom-Json -InputObject $raw
    # ConvertFrom-Json keeps only the last duplicate key. Reject duplicates first,
    # including escaped equivalent keys, before callers can trust the parsed object.
    $objects = [Collections.Generic.Stack[object]]::new()
    $tokens = [regex]::Matches($raw, '"(?:[^"\\]|\\.)*"|[{}:]')
    for ($i = 0; $i -lt $tokens.Count; $i++) {
        $token = $tokens[$i].Value
        if ($token -eq '{') { $objects.Push([Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)) }
        elseif ($token -eq '}') { if ($objects.Count) { $null = $objects.Pop() } }
        elseif ($token.StartsWith('"') -and $i + 1 -lt $tokens.Count -and $tokens[$i + 1].Value -eq ':') {
            $key = (ConvertFrom-Json -InputObject ('{"key":' + $token + '}')).key
            if ($objects.Count -eq 0 -or -not $objects.Peek().Add($key)) { Fail 'Invalid JSON duplicate property.' }
        }
    }
    return ,$value
}
function Assert-CatalogFields($value, [string[]]$names) {
    if ($value -isnot [pscustomobject]) { Fail 'Invalid catalog object.' }
    $actual = @($value.PSObject.Properties.Name)
    if ($actual.Count -ne $names.Count) { Fail 'Invalid catalog fields.' }
    foreach ($name in $names) { if ($actual -cnotcontains $name) { Fail 'Invalid catalog field name.' } }
}
function Assert-CatalogString($value) {
    if ($value -isnot [string] -or [string]::IsNullOrWhiteSpace($value) -or $value.Length -gt 256 -or $value -match '[\r\n]|bearer\s|sk-[a-z0-9]|AIza|-----BEGIN') { Fail 'Invalid catalog string.' }
}
function Assert-CatalogStringArray($value, [string[]]$allowed) {
    if ($value -isnot [array] -or $value.Count -eq 0) { Fail 'Invalid catalog array.' }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($item in $value) {
        if ($item -isnot [string] -or $allowed -cnotcontains $item -or -not $seen.Add($item)) { Fail 'Invalid catalog array member.' }
    }
}
function Assert-ConfigCatalog([string]$configPath, [string]$catalogRoot) {
    $raw = Get-Content -LiteralPath $configPath -Raw
    $config = Read-ContractJson $configPath
    if ($null -eq $config -or $raw.TrimStart()[0] -ne '{') { Fail 'Config must be a JSON object.' }
    $ids = $null
    if ($config.Assistant -and $config.Assistant.PSObject.Properties.Name -ccontains 'SharedAiModelIds') {
        $ids = $config.Assistant.SharedAiModelIds
        if ($ids -isnot [array] -or $ids.Count -eq 0) { Fail 'SharedAiModelIds must be a nonempty array.' }
        $seenIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($id in $ids) {
            if ($id -isnot [string] -or [string]::IsNullOrWhiteSpace($id) -or -not $seenIds.Add($id)) { Fail 'SharedAiModelIds must contain unique nonempty strings.' }
        }
    }
    $providers = Read-ContractJson (Join-Path $catalogRoot 'providers.json')
    $models = Read-ContractJson (Join-Path $catalogRoot 'models.json')
    $routes = Read-ContractJson (Join-Path $catalogRoot 'routes.json')
    $bindings = @{
        nvidia=@('https://integrate.api.nvidia.com/v1','NVIDIA_API_KEY')
        'google-gemini'=@('https://generativelanguage.googleapis.com/v1beta/openai','GEMINI_API_KEY')
        openrouter=@('https://openrouter.ai/api/v1','OPENROUTER_API_KEY')
    }
    if ($providers -isnot [array] -or $providers.Count -ne 3 -or @($providers.id | Select-Object -Unique).Count -ne 3) { Fail 'Invalid providers catalog.' }
    foreach ($provider in $providers) {
        Assert-CatalogFields $provider @('id','protocol','baseUrl','credentialBinding')
        foreach ($field in @('id','protocol','baseUrl','credentialBinding')) { Assert-CatalogString $provider.$field }
        if (@('nvidia','google-gemini','openrouter') -cnotcontains $provider.id) { Fail 'Invalid catalog provider.' }
        if (-not $bindings.ContainsKey([string]$provider.id) -or $provider.protocol -cne 'openai-chat-completions' -or $provider.baseUrl -cne $bindings[$provider.id][0] -or $provider.credentialBinding -cne $bindings[$provider.id][1]) { Fail 'Invalid provider binding.' }
    }
    $approved = @('nvidia-kimi-k3','google-gemini-3-8-flash')
    if ($models -isnot [array] -or $models.Count -ne 2 -or @($models.id | Select-Object -Unique).Count -ne 2) { Fail 'Invalid models catalog.' }
    foreach ($model in $models) {
        Assert-CatalogFields $model @('id','providerId','providerModel','displayName','capabilities','contextLimit','roles')
        foreach ($field in @('id','providerId','providerModel','displayName')) { Assert-CatalogString $model.$field }
        if ($model.displayName.Length -gt 80) { Fail 'Invalid catalog display name.' }
        if (($model.contextLimit -isnot [int] -and $model.contextLimit -isnot [long]) -or $model.contextLimit -le 0 -or $model.contextLimit -gt [int]::MaxValue) { Fail 'Invalid catalog context limit.' }
        Assert-CatalogFields $model.capabilities @('text','vision','tools','streaming','structuredOutput')
        Assert-CatalogStringArray $model.roles @('general','vision','engineering','agent','fast','fallback')
        $providerId = if ($model.id -ceq $approved[0]) { 'nvidia' } else { 'google-gemini' }
        if ($approved -cnotcontains $model.id -or $model.providerId -cne $providerId -or $model.providerModel -cnotmatch '^[a-z0-9][a-z0-9./-]{0,99}$' -or [string]::IsNullOrWhiteSpace($model.displayName) -or $model.contextLimit -le 0) { Fail 'Invalid model identity.' }
        foreach ($cap in @('text','vision','tools','streaming','structuredOutput')) { if ($model.capabilities.$cap -isnot [bool]) { Fail 'Invalid model capability.' } }
    }
    Assert-CatalogFields $routes @('default','vision','tools')
    foreach ($route in @('default','vision','tools')) {
        $values = $routes.$route
        Assert-CatalogStringArray $values $approved
        if ($values -isnot [array] -or $values.Count -eq 0 -or @($values | Select-Object -Unique).Count -ne $values.Count) { Fail 'Invalid route.' }
        foreach ($id in $values) { if ($approved -cnotcontains $id) { Fail 'Unknown route model.' } }
    }
    foreach ($id in $ids) { if ($approved -cnotcontains $id) { Fail 'Unknown SharedAiModelIds model.' } }
    return $config
}
function Assert-CatalogParity([string]$left, [string]$right) {
    foreach ($name in $requiredCatalogArtifacts) {
        if ((Get-FileHash -LiteralPath (Join-Path $left $name) -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath (Join-Path $right $name) -Algorithm SHA256).Hash) { Fail "Catalog parity mismatch: $name" }
    }
}
function Assert-Source {
    Assert-NoReparseTree $labCatalogSource
    if (-not (Test-Path -LiteralPath $labSource)) { Fail "Lab DLL not found: $labSource. Run build --target Lab first." }
    if (-not (Test-Path -LiteralPath $labConfigSource)) { Fail "Lab config not found: $labConfigSource." }
    Assert-FrontendParity $sourceFrontendRoot $labFrontendSource 'source-to-build frontend'
    $null = Get-FrontendBuildId $sourceFrontendRoot $RunId
    $null = Get-FrontendBuildId $labFrontendSource $RunId
    $null = Assert-ConfigCatalog $labConfigSource $labCatalogSource
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
    $effectiveBuildId = if ([string]::IsNullOrWhiteSpace($RunId)) { 'BB20-SOL-BUILD-' + (Get-Date -Format 'yyyyMMdd-HHmmss') } else { $RunId }
    $commit = (& git -C $repo rev-parse HEAD).Trim()
    $buildUtc = [DateTime]::UtcNow.ToString('o')
    $savedEnvironment = $env:VITE_BLUEBRICK_ENVIRONMENT
    $savedCommit = $env:VITE_BLUEBRICK_SOURCE_COMMIT
    $savedBuildId = $env:VITE_BLUEBRICK_BUILD_ID
    $savedBuildUtc = $env:VITE_BLUEBRICK_BUILD_UTC
    try {
        $env:VITE_BLUEBRICK_ENVIRONMENT = $configuration.ToUpperInvariant()
        $env:VITE_BLUEBRICK_SOURCE_COMMIT = $commit
        $env:VITE_BLUEBRICK_BUILD_ID = $effectiveBuildId
        $env:VITE_BLUEBRICK_BUILD_UTC = $buildUtc
        Push-Location $sourceFrontendRoot
        try {
            & npm.cmd run typecheck
            if ($LASTEXITCODE -ne 0) { Fail "AssistantWeb typecheck failed ($LASTEXITCODE)." }
            & npm.cmd run build
            if ($LASTEXITCODE -ne 0) { Fail "AssistantWeb build failed ($LASTEXITCODE)." }
        } finally { Pop-Location }
    } finally {
        $env:VITE_BLUEBRICK_ENVIRONMENT = $savedEnvironment
        $env:VITE_BLUEBRICK_SOURCE_COMMIT = $savedCommit
        $env:VITE_BLUEBRICK_BUILD_ID = $savedBuildId
        $env:VITE_BLUEBRICK_BUILD_UTC = $savedBuildUtc
    }
    Write-Step "build configuration=$configuration project=$project"
    & $msbuild $project /t:Rebuild /p:Configuration=$configuration /p:Platform=AnyCPU /m
    if ($LASTEXITCODE -ne 0) { Fail "MSBuild failed ($LASTEXITCODE)." }
}
function New-LabBackup {
    if ([string]::IsNullOrWhiteSpace($BackupRoot)) { $script:BackupRoot = Join-Path $labRoot ('backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
    Assert-LabPath $BackupRoot 'BackupRoot'
    Assert-NoReparseTree $BackupRoot
    Assert-NoReparseTree $labCatalogTarget
    New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null
    $manifest = [ordered]@{
        schema = 'bluebrick-lab-deployment.v1'
        createdUtc = [DateTime]::UtcNow.ToString('o')
        target = 'Lab'
        labRoot = $labRoot
        productionRoot = $productionRoot
        labDllTarget = $labTarget
        labConfigTarget = $labConfigTarget
        labFrontendTarget = $labFrontendTarget
        labRuntimeManifestTarget = $labRuntimeManifestTarget
        productionMutation = $false
        dllExisted = Test-Path -LiteralPath $labTarget
        configExisted = Test-Path -LiteralPath $labConfigTarget
        frontendExisted = Test-Path -LiteralPath $labFrontendTarget
        runtimeManifestExisted = Test-Path -LiteralPath $labRuntimeManifestTarget
        catalogExisted = Test-Path -LiteralPath $labCatalogTarget
    }
    if ($manifest.dllExisted) { Copy-Item -LiteralPath $labTarget -Destination (Join-Path $BackupRoot 'BlueBrick.Lab.dll.orig') -Force }
    if ($manifest.configExisted) { Copy-Item -LiteralPath $labConfigTarget -Destination (Join-Path $BackupRoot 'appsettings.lab.json.orig') -Force }
    if ($manifest.frontendExisted) { Copy-Item -LiteralPath $labFrontendTarget -Destination (Join-Path $BackupRoot 'AssistantWeb.dist.orig') -Recurse -Force }
    if ($manifest.runtimeManifestExisted) { Copy-Item -LiteralPath $labRuntimeManifestTarget -Destination (Join-Path $BackupRoot 'runtime-manifest.json.orig') -Force }
    if ($manifest.catalogExisted) { Copy-Item -LiteralPath $labCatalogTarget -Destination (Join-Path $BackupRoot 'SharedAI.catalog.orig') -Recurse -Force }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $BackupRoot 'lab-deployment.json') -Encoding UTF8
    return $BackupRoot
}
function Write-LabRuntimeManifest([string]$deploymentRunId) {
    Assert-FrontendParity $sourceFrontendRoot $labFrontendSource 'source-to-build frontend'
    Assert-FrontendParity $labFrontendSource $labFrontendTarget 'build-to-deployed frontend'
    $config = Assert-ConfigCatalog $labConfigTarget $labCatalogTarget
    Assert-CatalogParity $labCatalogSource $labCatalogTarget
    foreach ($pair in @(@($labSource,$labTarget), @($labConfigSource,$labConfigTarget))) {
        if ((Get-FileHash -LiteralPath $pair[0] -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $pair[1] -Algorithm SHA256).Hash) { Fail 'Deployed DLL/config parity mismatch.' }
    }
    $commit = (& git -C $repo rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commit)) { Fail 'Unable to resolve source commit for runtime manifest.' }
    $effectiveRunId = Get-FrontendBuildId $labFrontendTarget $deploymentRunId
    $catalogArtifacts = [ordered]@{}
    foreach ($name in $requiredCatalogArtifacts) { $catalogArtifacts[$name] = @{ sha256 = (Get-FileHash -LiteralPath (Join-Path $labCatalogTarget $name) -Algorithm SHA256).Hash } }
    $frontend = [ordered]@{}
    foreach ($name in $requiredFrontendArtifacts) {
        $path = Join-Path $labFrontendTarget $name
        $frontend[$name] = [ordered]@{
            path = $path
            sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
            size = (Get-Item -LiteralPath $path).Length
        }
    }
    $manifest = [ordered]@{
        schema = 'bluebrick-lab-runtime-manifest.v2'
        sharedAiCatalog = @{ artifacts = $catalogArtifacts }
        product = 'BlueBrick'
        channel = 'Lab'
        targetVersion = '2.0-beta'
        gitCommit = $commit
        buildId = $effectiveRunId
        frontendBuildId = $effectiveRunId
        deployedUtc = [DateTime]::UtcNow.ToString('o')
        bridgePort = [int]$config.Agent.BridgePort
        configSchemaVersion = [string]$config.ConfigSchemaVersion
        dll = [ordered]@{ path = $labTarget; sha256 = (Get-FileHash -LiteralPath $labTarget -Algorithm SHA256).Hash }
        frontend = [ordered]@{ root = $labFrontendTarget; artifacts = $frontend }
        config = [ordered]@{ path = $labConfigTarget; sha256 = (Get-FileHash -LiteralPath $labConfigTarget -Algorithm SHA256).Hash }
    }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $labRuntimeManifestTarget -Encoding UTF8
    Write-Step "runtime manifest written: $labRuntimeManifestTarget"
}
function Invoke-LabRollback([string]$root) {
    if ([string]::IsNullOrWhiteSpace($root)) { Fail 'rollback requires -BackupRoot pointing to one exact Lab backup.' }
    Assert-LabPath $root 'BackupRoot'
    Assert-NoReparseTree $root
    Assert-NoReparseTree $labCatalogTarget
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
    $frontendBackup = Join-Path $resolved 'AssistantWeb.dist.orig'
    $runtimeManifestBackup = Join-Path $resolved 'runtime-manifest.json.orig'
    New-Item -ItemType Directory -Path (Split-Path $labTarget -Parent), (Split-Path $labConfigTarget -Parent) -Force | Out-Null
    if (Test-Path -LiteralPath $dllBackup) { Copy-Item -LiteralPath $dllBackup -Destination $labTarget -Force } elseif (-not $manifest.dllExisted -and (Test-Path -LiteralPath $labTarget)) { Remove-Item -LiteralPath $labTarget -Force }
    if (Test-Path -LiteralPath $configBackup) { Copy-Item -LiteralPath $configBackup -Destination $labConfigTarget -Force } elseif (-not $manifest.configExisted -and (Test-Path -LiteralPath $labConfigTarget)) { Remove-Item -LiteralPath $labConfigTarget -Force }
    $hasFrontendState = $manifest.PSObject.Properties.Name -contains 'frontendExisted'
    if ($hasFrontendState) {
        Assert-LabPath $labFrontendTarget 'LabFrontendTarget'
        if (Test-Path -LiteralPath $labFrontendTarget) { Remove-Item -LiteralPath $labFrontendTarget -Recurse -Force }
        if (Test-Path -LiteralPath $frontendBackup) {
            New-Item -ItemType Directory -Path (Split-Path $labFrontendTarget -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $frontendBackup -Destination $labFrontendTarget -Recurse -Force
        }
    }
    $hasRuntimeManifestState = $manifest.PSObject.Properties.Name -contains 'runtimeManifestExisted'
    if ($hasRuntimeManifestState) {
        if (Test-Path -LiteralPath $runtimeManifestBackup) { Copy-Item -LiteralPath $runtimeManifestBackup -Destination $labRuntimeManifestTarget -Force }
        elseif (Test-Path -LiteralPath $labRuntimeManifestTarget) { Remove-Item -LiteralPath $labRuntimeManifestTarget -Force }
    }

    if ($manifest.PSObject.Properties.Name -contains 'catalogExisted') {
        $catalogBackup = if ($manifest.catalogExisted) { Join-Path $resolved 'SharedAI.catalog.orig' } else { '' }
        Set-LabCatalog $catalogBackup
    }
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

if ($LibraryOnly) { return }
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
        Write-Step "prepare Lab frontend=$labFrontendSource exact-triplet=true source-package-parity=true"
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
            $stagingRoot = Join-Path $labRoot ('.staging-' + [Guid]::NewGuid().ToString('N'))
            Assert-LabPath $stagingRoot 'LabStagingRoot'
            Assert-NoReparsePoint $stagingRoot
            New-Item -ItemType Directory -Path $stagingRoot, (Join-Path $stagingRoot 'config'), (Join-Path $stagingRoot 'AssistantWeb') -Force | Out-Null
            Copy-Item -LiteralPath $labSource -Destination (Join-Path $stagingRoot 'BlueBrick.Lab.dll') -Force
            Copy-Item -LiteralPath $labConfigSource -Destination (Join-Path $stagingRoot 'config\appsettings.lab.json') -Force
            Copy-Item -LiteralPath $labFrontendSource -Destination (Join-Path $stagingRoot 'AssistantWeb\dist') -Recurse -Force
            New-Item -ItemType Directory -Path (Join-Path $stagingRoot 'SharedAI') -Force | Out-Null
            Assert-NoReparseTree $labCatalogSource
            Assert-NoReparseTree (Join-Path $stagingRoot 'SharedAI\catalog')
            Copy-Item -LiteralPath $labCatalogSource -Destination (Join-Path $stagingRoot 'SharedAI\catalog') -Recurse -Force
            Assert-FrontendParity $labFrontendSource (Join-Path $stagingRoot 'AssistantWeb\dist') 'build-to-staging frontend'
            $null = Get-FrontendBuildId (Join-Path $stagingRoot 'AssistantWeb\dist') (Get-FrontendBuildId $labFrontendSource $RunId)
            $null = Assert-ConfigCatalog (Join-Path $stagingRoot 'config\appsettings.lab.json') (Join-Path $stagingRoot 'SharedAI\catalog')
            Assert-CatalogParity $labCatalogSource (Join-Path $stagingRoot 'SharedAI\catalog')
            foreach ($pair in @(@($labSource,(Join-Path $stagingRoot 'BlueBrick.Lab.dll')), @($labConfigSource,(Join-Path $stagingRoot 'config\appsettings.lab.json')))) {
                if ((Get-FileHash -LiteralPath $pair[0] -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $pair[1] -Algorithm SHA256).Hash) { Fail 'Staged DLL/config parity mismatch.' }
            }
            New-Item -ItemType Directory -Path $labRoot, (Split-Path $labConfigTarget -Parent), (Split-Path $labFrontendTarget -Parent) -Force | Out-Null
            Copy-Item -LiteralPath (Join-Path $stagingRoot 'BlueBrick.Lab.dll') -Destination $labTarget -Force
            Copy-Item -LiteralPath (Join-Path $stagingRoot 'config\appsettings.lab.json') -Destination $labConfigTarget -Force
            Assert-LabPath $labFrontendTarget 'LabFrontendTarget'
            if (Test-Path -LiteralPath $labFrontendTarget) { Remove-Item -LiteralPath $labFrontendTarget -Recurse -Force }
            Copy-Item -LiteralPath (Join-Path $stagingRoot 'AssistantWeb\dist') -Destination $labFrontendTarget -Recurse -Force
            Set-LabCatalog (Join-Path $stagingRoot 'SharedAI\catalog')
            Write-LabRuntimeManifest $RunId
            $register = Join-Path $repo 'tools\register-lab-addin.ps1'
            Invoke-PowerShell $register @('-Mode','PerUser','-LabDllPath',$labTarget,'-BackupRoot',(Join-Path $runRoot 'registry'))
            Remove-Item -LiteralPath $stagingRoot -Recurse -Force
            Write-Step "Lab deployment verified: $labTarget"
            $sw = Resolve-SolidWorks
            if (-not $sw) { Fail 'SOLIDWORKS executable was not found on PATH; Lab deployment remains staged.' }
            $process = Start-Process -FilePath $sw -PassThru
            Write-Step "SOLIDWORKS launch requested pid=$($process.Id); use smoke to collect read-only Lab registration/process evidence"
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
