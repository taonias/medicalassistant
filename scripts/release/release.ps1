#!/usr/bin/env pwsh
# Fully automated deploy: build+package images (package-release.ps1), then upload + refresh
# the production VM (deploy-remote.ps1). This is the single command for the whole procedure
# documented across ops/deploy/DEPLOYMENT.md steps 1-4.
#
# Usage:
#   ./scripts/release/release.ps1                  # tag "latest", build + deploy
#   ./scripts/release/release.ps1 -Tag 2026-09-19
#   ./scripts/release/release.ps1 -SkipBuild       # re-use an already-built release/<Tag>/
#   ./scripts/release/release.ps1 -NoLoad          # skip re-loading tars on the VM
param(
    [string]$Tag = "latest",
    [switch]$SkipBuild,
    [switch]$NoLoad
)

$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "package-release.ps1") -Tag $Tag -SkipBuild:$SkipBuild
if ($LASTEXITCODE -ne 0) { throw "package-release.ps1 failed." }

$deployArgs = @{ Tag = $Tag }
if ($NoLoad) { $deployArgs.NoLoad = $true }
& (Join-Path $PSScriptRoot "deploy-remote.ps1") @deployArgs
if ($LASTEXITCODE -ne 0) { throw "deploy-remote.ps1 failed." }

Write-Host ""
Write-Host "==> Release $Tag built and deployed." -ForegroundColor Green
