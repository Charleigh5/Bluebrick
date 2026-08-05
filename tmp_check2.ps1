$guids = @(
    'C56E0AFF-0BD3-4364-90CB-1A581046CD7D',
    '251d6df2-3e7b-42ef-b7fc-175e1fdcb4c5'
)
foreach ($g in $guids) {
    foreach ($hive in @('HKLM:\SOFTWARE', 'HKCU:\SOFTWARE')) {
        $p = "$hive\SolidWorks\AddIns\$g"
        if (Test-Path $p) {
            Write-Output "=== $p ==="
            Get-ItemProperty $p | Format-List
        }
        $sp = "$hive\SolidWorks\AddInsStartup\$g"
        if (Test-Path $sp) {
            Write-Output "=== $sp ==="
            Get-ItemProperty $sp | Format-List
        }
    }
}
