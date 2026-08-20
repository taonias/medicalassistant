#!/usr/bin/env pwsh
# Starts every Medical Assistant component and opens the key pages in Chrome:
#   - all backend services via Docker Compose (API, worker, Clinical Knowledge,
#     both PostgreSQL DBs, RabbitMQ + provisioner, Azurite, one-shot migrations)
#   - the Vite frontend dev server (in its own window)
#   - Chrome tabs: frontend, backend Swagger, Clinical Knowledge Swagger, RabbitMQ UI
#
# Usage:
#   ./start.ps1             # start everything + open browser (asks whether to rebuild)
#   ./start.ps1 -Build      # incremental image rebuild first (docker compose --build)
#   ./start.ps1 -Rebuild    # full from-scratch rebuild: delete built images + --no-cache
#   ./start.ps1 -NoFrontend # backend only, no frontend, no browser
#   ./start.ps1 -NoBrowser  # start everything but don't open Chrome
param(
    [switch]$Build,
    [switch]$Rebuild,
    [switch]$NoFrontend,
    [switch]$NoBrowser
)

$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot

function Test-Url([string]$Url) {
    try { return (Invoke-WebRequest -UseBasicParsing -Uri $Url -TimeoutSec 2).StatusCode -eq 200 }
    catch { return $false }
}

function Get-EnvValue([string]$Name) {
    $line = (Get-Content ".env" -ErrorAction SilentlyContinue | Where-Object { $_ -match "^$Name=" } | Select-Object -First 1)
    if ($line) { return $line.Substring($line.IndexOf('=') + 1).Trim() }
    return ""
}

function Open-InChrome([string[]]$Urls) {
    $chrome = @(
        "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
        "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe",
        "$env:LOCALAPPDATA\Google\Chrome\Application\chrome.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if ($chrome) {
        Start-Process $chrome -ArgumentList $Urls
    } else {
        Write-Host "Chrome not found; opening in the default browser instead." -ForegroundColor Yellow
        foreach ($u in $Urls) { Start-Process $u }
    }
}

if (-not (Test-Path ".env")) {
    Write-Host "No .env found. Copy compose.env.example to .env and fill in keys first." -ForegroundColor Red
    exit 1
}

# Decide how to bring the stack up:
#   -Rebuild : delete the locally-built images and rebuild from scratch (--no-cache)
#   -Build   : incremental image rebuild
#   neither  : ask whether to do a full from-scratch rebuild, default No
$doRebuild = [bool]$Rebuild
if (-not $Rebuild -and -not $Build) {
    $answer = Read-Host "Completely rebuild all Docker images from scratch? This deletes the built images and rebuilds with --no-cache (slow; data volumes are kept) [y/N]"
    if ($answer -match '^(y|yes)$') { $doRebuild = $true }
}

if ($doRebuild) {
    Write-Host "==> Removing locally-built images and rebuilding from scratch (docker compose)..." -ForegroundColor Cyan
    docker compose down --rmi local --remove-orphans
    docker compose build --no-cache
    docker compose up -d
} elseif ($Build) {
    Write-Host "==> Starting backend services with an incremental image rebuild (docker compose)..." -ForegroundColor Cyan
    docker compose up -d --build
} else {
    Write-Host "==> Starting backend services (docker compose)..." -ForegroundColor Cyan
    docker compose up -d
}

Write-Host "==> Waiting for backend API (http://localhost:7037/health/ready)..." -ForegroundColor Cyan
$ready = $false
for ($i = 0; $i -lt 60; $i++) {
    if (Test-Url "http://localhost:7037/health/ready") { $ready = $true; break }
    Start-Sleep -Seconds 2
}
if ($ready) { Write-Host "Backend ready." -ForegroundColor Green }
else { Write-Host "Backend not confirmed ready; check 'docker compose ps' / 'docker compose logs'." -ForegroundColor Yellow }

Write-Host ""
Write-Host "Endpoints:" -ForegroundColor Cyan
Write-Host "  Frontend           http://localhost:5173"
Write-Host "  Backend API        http://localhost:7037/swagger"
Write-Host "  Clinical Knowledge http://localhost:8000/swagger"
Write-Host "  RabbitMQ UI        http://localhost:15672"
Write-Host "  pgAdmin            http://localhost:5050"
Write-Host "  Vector DB UI       http://localhost:8787"
Write-Host "  Telemetry (Aspire) http://localhost:18888"
Write-Host ""

$dbPass = Get-EnvValue "POSTGRES_APP_PASSWORD"; if (-not $dbPass) { $dbPass = "medicalassistant-local" }
Write-Host "Database (one server, two DBs, same credentials) - from host:" -ForegroundColor Cyan
Write-Host "  App DB      Host=localhost;Port=5432;Database=MedicalAssistantDb;Username=medicalassistant;Password=$dbPass"
Write-Host "  Clinical DB Host=localhost;Port=5432;Database=ai_med;Username=medicalassistant;Password=$dbPass"
Write-Host ""

if ($NoFrontend) {
    Write-Host "Backend up. Start the frontend with:  cd frontend; npm run dev -- --port 5173" -ForegroundColor Green
    exit 0
}

# Start the frontend (in its own window) unless it is already running.
$frontendDir = Join-Path $PSScriptRoot "frontend"
if (Test-Url "http://localhost:5173") {
    Write-Host "Frontend already running on http://localhost:5173." -ForegroundColor Green
} else {
    if (-not (Test-Path (Join-Path $frontendDir "node_modules"))) {
        Write-Host "==> Installing frontend dependencies..." -ForegroundColor Cyan
        Push-Location $frontendDir; npm install; Pop-Location
    }
    Write-Host "==> Starting frontend (Vite) in a new window..." -ForegroundColor Cyan
    $psExe = (Get-Process -Id $PID).Path
    Start-Process $psExe -ArgumentList @(
        "-NoExit", "-Command",
        "Set-Location -LiteralPath '$frontendDir'; npm run dev -- --port 5173"
    )
    Write-Host "==> Waiting for frontend (http://localhost:5173)..." -ForegroundColor Cyan
    for ($i = 0; $i -lt 60; $i++) {
        if (Test-Url "http://localhost:5173") { break }
        Start-Sleep -Seconds 1
    }
}

if (-not $NoBrowser) {
    Write-Host "==> Opening Chrome..." -ForegroundColor Cyan
    Open-InChrome @(
        "http://localhost:5173",
        "http://localhost:7037/swagger",
        "http://localhost:8000/swagger",
        "http://localhost:15672",
        "http://localhost:5050",
        "http://localhost:8787",
        "http://localhost:18888"
    )
}

Write-Host "All set. The frontend runs in its own window (close it or Ctrl+C there to stop it)." -ForegroundColor Green
