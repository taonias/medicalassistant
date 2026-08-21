#!/usr/bin/env pwsh
# Builds the production images on THIS machine, saves them to .tar files, and assembles a
# self-contained release/ folder to upload to the VM with FileZilla (SFTP:22).
#
# Base images (postgres, rabbitmq, azurite, curl, certbot) are NOT tarred — they are pulled
# on the server from Docker Hub, keeping the upload small.
#
# Usage:
#   ./deploy/package-release.ps1                 # build + tag 'latest' + package
#   ./deploy/package-release.ps1 -Tag 2026-08-20 # a versioned tag
#   ./deploy/package-release.ps1 -SkipBuild      # re-package already-built images
param(
    [string]$Tag = "latest",
    [string]$Prefix = "medicalassistant",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
# Repo root is the parent of this script's folder.
$root = Split-Path -Parent $PSScriptRoot
Set-Location -Path $root

$release = Join-Path $root "release"
$images  = Join-Path $release "images"
New-Item -ItemType Directory -Force -Path $images | Out-Null

# service name -> dockerfile (build context is always the repo root)
$builds = [ordered]@{
    "backend-api"          = "backend/src/MedicalAssistant.Api/Dockerfile"
    "backend-migrations"   = "backend/src/MedicalAssistant.Migrations/Dockerfile"
    "clinical-knowledge"   = "AI/src/MedicalAssistance.Ingestion.Api/Dockerfile"
    "transcription-worker" = "backend/src/MedicalAssistant.Transcription.Worker/Dockerfile"
    "proxy"                = "deploy/proxy.Dockerfile"
}

if (-not $SkipBuild) {
    foreach ($name in $builds.Keys) {
        $image = "$Prefix/$name`:$Tag"
        Write-Host "==> Building $image ($($builds[$name]))" -ForegroundColor Cyan
        docker build -f $builds[$name] -t $image .
        if ($LASTEXITCODE -ne 0) { throw "Build failed for $name" }
    }
}

foreach ($name in $builds.Keys) {
    $image = "$Prefix/$name`:$Tag"
    $tar   = Join-Path $images "$name.tar"
    Write-Host "==> Saving $image -> $tar" -ForegroundColor Cyan
    docker save -o $tar $image
    if ($LASTEXITCODE -ne 0) { throw "docker save failed for $name" }
}

# Config + compose the server needs alongside the image tars.
Write-Host "==> Copying compose, config, and deploy scripts into release/" -ForegroundColor Cyan
Copy-Item (Join-Path $root "docker-compose.prod.yml") $release -Force
Copy-Item (Join-Path $root "db-init")  (Join-Path $release "db-init")  -Recurse -Force
Copy-Item (Join-Path $root "rabbitmq") (Join-Path $release "rabbitmq") -Recurse -Force
Copy-Item (Join-Path $PSScriptRoot "deploy.sh")            $release -Force
Copy-Item (Join-Path $PSScriptRoot "init-letsencrypt.sh")  $release -Force
Copy-Item (Join-Path $PSScriptRoot ".env.prod.example")    $release -Force

# Record the tag/prefix so deploy.sh uses the same coordinates.
"IMAGE_PREFIX=$Prefix`nIMAGE_TAG=$Tag" | Set-Content (Join-Path $release "release.info")

Write-Host ""
Write-Host "Release ready in: $release" -ForegroundColor Green
Write-Host "Next: FileZilla the ENTIRE 'release' folder to the VM, then on the VM run deploy.sh." -ForegroundColor Green
