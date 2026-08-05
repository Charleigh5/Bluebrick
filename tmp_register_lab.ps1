$labGuid = "251d6df2-3e7b-42ef-b7fc-175e1fdcb4c5"
$dllPath = "C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick\bin\Lab\BlueBrick.Lab.dll"

# Write to HKCU instead of HKLM (avoids admin requirement)
# SolidWorks also checks HKCU for add-in registrations
$addinsKey = "HKCU:\SOFTWARE\SolidWorks\Addins"
$startupKey = "HKCU:\SOFTWARE\SolidWorks\AddInsStartup"

# Create the Addins entry
$addinsPath = "$addinsKey\{$labGuid}"
if (-not (Test-Path $addinsPath)) {
    New-Item -Path $addinsPath -Force | Out-Null
}
Set-ItemProperty -Path $addinsPath -Name "(Default)" -Value 0
Set-ItemProperty -Path $addinsPath -Name "Description" -Value "ViraInsight SolidWorks lab add-in"
Set-ItemProperty -Path $addinsPath -Name "Title" -Value "BlueBrick Lab"
Write-Output "Registered add-in: $addinsPath"

# Create the AddInsStartup entry
$startupPath = "$startupKey\{$labGuid}"
if (-not (Test-Path $startupPath)) {
    New-Item -Path $startupPath -Force | Out-Null
}
Set-ItemProperty -Path $startupPath -Name "(Default)" -Value 1 -Type DWord
Write-Output "Set load at startup: $startupPath"

# Also try HKLM (may fail without admin)
try {
    $hklmPath = "HKLM:\SOFTWARE\SolidWorks\Addins\{$labGuid}"
    if (-not (Test-Path $hklmPath)) {
        New-Item -Path $hklmPath -Force | Out-Null
    }
    Set-ItemProperty -Path $hklmPath -Name "(Default)" -Value 0
    Set-ItemProperty -Path $hklmPath -Name "Description" -Value "ViraInsight SolidWorks lab add-in"
    Set-ItemProperty -Path $hklmPath -Name "Title" -Value "BlueBrick Lab"
    Write-Output "Registered add-in in HKLM: $hklmPath"
} catch {
    Write-Output "HKLM registration skipped (needs admin): $($_.Exception.Message)"
}

# Also register the COM component for the Lab build
try {
    $output = & "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" /codebase $dllPath 2>&1
    Write-Output "RegAsm output: $output"
} catch {
    Write-Output "RegAsm failed (needs admin): $($_.Exception.Message)"
}

Write-Output ""
Write-Output "=== Verification ==="
if (Test-Path $addinsPath) {
    Write-Output "HKCU Addins entry exists:"
    Get-ItemProperty $addinsPath | Format-List
}
if (Test-Path $startupPath) {
    Write-Output "HKCU AddInsStartup entry exists:"
    Get-ItemProperty $startupPath | Format-List
}
