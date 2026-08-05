$paths = @(
    'HKLM:\SOFTWARE\SolidWorks\AddIns',
    'HKCU:\SOFTWARE\SolidWorks\AddIns'
)
foreach ($p in $paths) {
    if (Test-Path $p) {
        Write-Output "=== $p ==="
        Get-ChildItem $p | ForEach-Object { Write-Output $_.PSChildName }
    }
}

$guid = '{2713D927-26A2-4437-ABDA-798E2CA0824A}'
foreach ($hive in @('HKLM:\SOFTWARE', 'HKCU:\SOFTWARE')) {
    $addinPath = "$hive\SolidWorks\AddIns\$guid"
    if (Test-Path $addinPath) {
        Write-Output "=== $addinPath ==="
        Get-ItemProperty $addinPath | Format-List
    }
}
