#!/usr/bin/env pwsh
# Stops the Medical Assistant backend services.
#   ./stop.ps1           # pause services, keep all data
#   ./stop.ps1 -Reset    # remove containers AND volumes (full wipe, re-migrates next start)
param(
    [switch]$Reset
)

$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot

if ($Reset) {
    Write-Host "==> Removing containers and volumes (full reset)..." -ForegroundColor Yellow
    docker compose down --volumes
    Write-Host "All data wiped. Next start re-runs migrations from empty." -ForegroundColor Green
} else {
    Write-Host "==> Stopping backend services (data preserved)..." -ForegroundColor Cyan
    docker compose stop
    Write-Host "Stopped. Run ./start.ps1 to resume. (The frontend, if running, stop with Ctrl+C.)" -ForegroundColor Green
}
