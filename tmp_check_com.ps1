# Check COM registration for the BlueBrick add-in
$guid = "{C56E0AFF-0BD3-4364-90CB-1A581046CD7D}"
$clsidPath = "HKLM:\SOFTWARE\Classes\CLSID\$guid"
if (Test-Path $clsidPath) {
    Write-Output "COM CLSID found in HKLM"
    Get-ItemProperty $clsidPath -ErrorAction SilentlyContinue | Format-List
    $inprocPath = "$clsidPath\InprocServer32"
    if (Test-Path $inprocPath) {
        Write-Output "InprocServer32:"
        Get-ItemProperty $inprocPath -ErrorAction SilentlyContinue | Format-List
    }
} else {
    Write-Output "COM CLSID not found in HKLM for $guid"
}

# Check with the actual CLSID from the assembly
$labGuid = "{251d6df2-3e7b-42ef-b7fc-175e1fdcb4c5}"
$labClsidPath = "HKLM:\SOFTWARE\Classes\CLSID\$labGuid"
if (Test-Path $labClsidPath) {
    Write-Output "COM CLSID found in HKLM for Lab GUID"
} else {
    Write-Output "COM CLSID not found in HKLM for Lab GUID"
}

# Try to find the BlueBrick COM registration
Write-Output ""
Write-Output "=== Searching for BlueBrick in COM registry ==="
Get-ChildItem "HKLM:\SOFTWARE\Classes\CLSID" -ErrorAction SilentlyContinue | Where-Object {
    $subKey = Get-ItemProperty "$($_.PSPath)\InprocServer32" -ErrorAction SilentlyContinue
    if ($subKey) {
        $codebase = $subKey.CodeBase
        if ($codebase -and $codebase -like "*BlueBrick*") { $_.PSChildName }
    }
}
