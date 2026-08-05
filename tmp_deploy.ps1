$srcDir = "C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick\bin\Debug"
$dstDir = "C:\BlueBrick"

Write-Output "=== Build output DLLs ==="
Get-ChildItem "$srcDir\*.dll" | ForEach-Object {
    $h = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    Write-Output "$h  $($_.Name)"
}

Write-Output ""
Write-Output "=== Currently deployed DLLs ==="
Get-ChildItem "$dstDir\*.dll" -ErrorAction SilentlyContinue | ForEach-Object {
    $h = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    Write-Output "$h  $($_.Name)"
}

Write-Output ""
Write-Output "=== Copying DLLs ==="
Get-ChildItem "$srcDir\*.dll" | ForEach-Object {
    Copy-Item $_.FullName -Destination "$dstDir\$($_.Name)" -Force
    Write-Output "Copied: $($_.Name)"
}

Write-Output ""
Write-Output "=== Copying config ==="
if (Test-Path "$srcDir\..\config") {
    Get-ChildItem "$srcDir\..\config" -ErrorAction SilentlyContinue | ForEach-Object {
        Copy-Item $_.FullName -Destination "$dstDir\" -Force -Recurse
        Write-Output "Copied config: $($_.Name)"
    }
}

Write-Output ""
Write-Output "=== Verify deployed hashes ==="
Get-ChildItem "$dstDir\BlueBrick.dll" | ForEach-Object {
    $h = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    Write-Output "$h  $($_.Name)"
}

$srcHash = (Get-FileHash "$srcDir\BlueBrick.dll" -Algorithm SHA256).Hash
$dstHash = (Get-FileHash "$dstDir\BlueBrick.dll" -Algorithm SHA256).Hash
if ($srcHash -eq $dstHash) {
    Write-Output "HASH MATCH: $srcHash"
} else {
    Write-Output "HASH MISMATCH! src=$srcHash dst=$dstHash"
}
