#Requires -Version 5.1
<#
.SYNOPSIS
  Starts the local Medical Assistant RabbitMQ Docker container.

.DESCRIPTION
  Uses rabbitmq/docker-compose.yml. On first run, copies .env.example to .env
  if .env is missing. Safe to re-run (starts or recreates as needed).

.EXAMPLE
  .\start-rabbitmq.ps1
#>

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$rabbitMqDir = Join-Path $repoRoot "rabbitmq"
$composeFile = Join-Path $rabbitMqDir "docker-compose.yml"
$envFile = Join-Path $rabbitMqDir ".env"
$envExample = Join-Path $rabbitMqDir ".env.example"

if (-not (Test-Path $composeFile)) {
    Write-Error "docker-compose.yml not found at: $composeFile"
}

function Test-DockerAvailable {
    try {
        docker info 1>$null 2>$null
        return $LASTEXITCODE -eq 0
    }
    catch {
        return $false
    }
}

if (-not (Test-DockerAvailable)) {
    Write-Error "Docker is not available. Install Docker Desktop and ensure it is running."
}

if (-not (Test-Path $envFile)) {
    if (-not (Test-Path $envExample)) {
        Write-Error ".env.example not found at: $envExample"
    }
    Copy-Item -Path $envExample -Destination $envFile
    Write-Host "Created rabbitmq\.env from .env.example (edit credentials if needed)."
}

Push-Location $rabbitMqDir
try {
    Write-Host "Starting RabbitMQ (medicalassistant-rabbitmq)..."
    docker compose up -d
    if ($LASTEXITCODE -ne 0) {
        Write-Error "docker compose up failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "RabbitMQ is starting."
Write-Host "  AMQP:           amqp://localhost:5672/"
Write-Host "  Management UI:  http://localhost:15672/"
Write-Host "  Credentials:    see rabbitmq\.env (defaults from .env.example)"
Write-Host ""
Write-Host "Align app settings with RABBITMQ_DEFAULT_USER / RABBITMQ_DEFAULT_PASS"
Write-Host "(API RabbitMq section and Transcriber RabbitMqConnection)."
