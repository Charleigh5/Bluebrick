<#!
.SYNOPSIS
    Bootstraps a developer workstation for the bluebrick-ai project.
.DESCRIPTION
    Installs Python dependencies, validates SolidWorks/EPDM prerequisites, and
    prepares local configuration files for development.
#>

param(
    [switch]$SkipValidation,
    [string]$PythonVersion = "3.11"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "==> Bootstrapping bluebrick-ai environment" -ForegroundColor Cyan

if (-not $SkipValidation) {
    Write-Host "-- Validating SolidWorks and EPDM prerequisites"
    python scripts/validate_prerequisites.py
}

Write-Host "-- Ensuring Python $PythonVersion is available"
$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) {
    throw "Python executable not found. Please install Python $PythonVersion and re-run."
}

Write-Host "-- Creating virtual environment"
python -m venv .venv

Write-Host "-- Activating virtual environment and installing dependencies"
$venvActivate = Join-Path .venv "Scripts\Activate.ps1"
if (-not (Test-Path $venvActivate)) {
    throw "Virtual environment activation script not found at $venvActivate"
}

& $venvActivate
pip install --upgrade pip
pip install -e .[dev]

Write-Host "==> Bootstrap completed" -ForegroundColor Green
