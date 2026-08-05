$src = "C:\Users\cweir\Documents\GitHub\VIRA GITHUB\Bluebrick\bin\Debug"
$dst = "C:\BlueBrick"
Get-ChildItem "$src\*" -Exclude "*.dll","*.pdb","*.xml" | Where-Object { -not $_.PSIsContainer } | ForEach-Object {
    Copy-Item $_.FullName "$dst\" -Force
    Write-Output ("Copied: " + $_.Name)
}
