$src = "C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick\bin\Debug"
$dst = "C:\BlueBrick"
if (-not (Test-Path "$dst\config")) { New-Item -Path "$dst\config" -ItemType Directory -Force | Out-Null }
$cfgSrc = "C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick\config"
Copy-Item "$cfgSrc\*" "$dst\config\" -Recurse -Force
Write-Output "Copied config files to C:\BlueBrick\config"
Get-ChildItem "$dst\config" | ForEach-Object { Write-Output ("  " + $_.Name) }
