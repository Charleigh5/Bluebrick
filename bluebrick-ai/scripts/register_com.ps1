<#!
.SYNOPSIS
    Registers SolidWorks and EPDM COM components required by bluebrick-ai.
.DESCRIPTION
    Invokes regsvr32 for the primary COM libraries used by SolidWorks automation.
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$SolidWorksPath,
    [Parameter(Mandatory=$true)]
    [string]$EpdmPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Register-ComLibrary {
    param(
        [string]$Library
    )

    if (-not (Test-Path $Library)) {
        throw "COM library not found: $Library"
    }

    Write-Host "Registering $Library" -ForegroundColor Cyan
    & regsvr32.exe /s $Library
}

Register-ComLibrary -Library (Join-Path $SolidWorksPath "swconst.tlb")
Register-ComLibrary -Library (Join-Path $SolidWorksPath "sldworks.tlb")
Register-ComLibrary -Library (Join-Path $EpdmPath "EdmInterface.dll")

Write-Host "COM registration complete" -ForegroundColor Green
