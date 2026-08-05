$src = "C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick\bin\Debug"
$dst = "C:\BlueBrick"
if (-not (Test-Path $dst)) { New-Item -Path $dst -ItemType Directory -Force | Out-Null }
Get-ChildItem "$src\*.dll" | ForEach-Object {
    Copy-Item $_.FullName "$dst\" -Force
    Write-Output ("Copied: " + $_.Name)
}
Write-Output ""
Write-Output "Files in C:\BlueBrick:"
Get-ChildItem "C:\BlueBrick\*.dll" | ForEach-Object { Write-Output ("  " + $_.Name + "  " + $_.LastWriteTime) }
