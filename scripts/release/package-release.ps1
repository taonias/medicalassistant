#!/usr/bin/env pwsh
# Builds the production images on THIS machine, saves them to .tar files, and assembles a
# fresh, versioned, self-contained release/<Tag>/ folder to upload to the VM with FileZilla
# (SFTP:22).
#
# Base images (postgres, rabbitmq, azurite, curl, certbot) are NOT tarred — they are pulled
# on the server from Docker Hub, keeping the upload small.
#
# release/<Tag>/ is wiped and rebuilt from scratch every run, so it can never accumulate
# stale files from a previous package. Older tags (release/2026-08-20/, etc.) are untouched
# siblings — keep them around for rollback, or delete them by hand once retired.
#
# Usage:
#   ./scripts/release/package-release.ps1                 # build + tag 'latest' + package
#   ./scripts/release/package-release.ps1 -Tag 2026-08-20 # a versioned tag
#   ./scripts/release/package-release.ps1 -SkipBuild      # re-package already-built images
param(
    [string]$Tag = "latest",
    [string]$Prefix = "medicalassistant",
    [switch]$SkipBuild,
    # When set, copies this file into release/<Tag>/.env.prod as the very last step, AFTER
    # the secret-shaped-file scan and allowlist check below have already run — a deliberate,
    # explicit exception to "never bundle secrets," not something those checks need to allow.
    # Off by default; plain `./package-release.ps1` behaves exactly as before.
    [string]$EnvProdPath = $null,
    # Optional progress hooks for callers driving a UI (e.g. deploy-ui.ps1) on top of this
    # script. Both are no-ops when not supplied, so plain CLI usage is unchanged.
    #   OnStep -Stage <string> -Status <'Running'|'Done'|'Failed'> -Detail <string>
    #   OnLine <string>  -- one line of raw docker build/save output
    [scriptblock]$OnStep = $null,
    [scriptblock]$OnLine = $null
)

$ErrorActionPreference = "Stop"
# Repo root is two levels up from this script's folder (scripts/release/).
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location -Path $root

function Send-Step {
    param([string]$Stage, [string]$Status, [string]$Detail = "")
    if ($OnStep) { & $OnStep $Stage $Status $Detail }
}

function Send-Line {
    param([string]$Line)
    if ($OnLine) { & $OnLine $Line }
}

# Runs a native command, forwarding each output line to Send-Line (in addition to the
# normal console output) when a UI is listening; otherwise behaves exactly like a plain call.
function Invoke-NativeWithLines {
    param([Parameter(Mandatory)][string]$Exe, [Parameter(Mandatory)][string[]]$Args)
    if ($OnLine) {
        & $Exe @Args 2>&1 | ForEach-Object {
            Write-Host $_
            Send-Line "$_"
        }
    } else {
        & $Exe @Args
    }
    return $LASTEXITCODE
}

. (Join-Path $PSScriptRoot "PackageRelease.Checks.ps1")

$releaseDir = Join-Path $root "release" $Tag
$images     = Join-Path $releaseDir "images"

# --- Fresh versioned staging directory: never inherits a previous run's files. ---
if (Test-Path $releaseDir) {
    Write-Host "==> Removing existing release/$Tag (fresh staging)" -ForegroundColor Cyan
    Remove-Item -LiteralPath $releaseDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $images | Out-Null

# service name -> dockerfile (build context is always the repo root)
$builds = [ordered]@{
    "backend-api"          = "backend/src/MedicalAssistant.Api/Dockerfile"
    "backend-migrations"   = "backend/src/MedicalAssistant.Migrations/Dockerfile"
    "clinical-knowledge"   = "clinical-knowledge/src/MedicalAssistance.Ingestion.Api/Dockerfile"
    "transcription-worker" = "backend/src/MedicalAssistant.Transcription.Worker/Dockerfile"
    "proxy"                = "ops/deploy/proxy.Dockerfile"
}

# --- Compose image-coordinate check (before building — fails fast). ---
Send-Step "preflight" "Running" "Checking docker-compose.prod.yml against the build list"
Write-Host "==> Checking docker-compose.prod.yml's tag-templated services against `$builds" -ForegroundColor Cyan
$composeImageServices = Get-TagTemplatedServices -ComposeFilePath (Join-Path $root "docker-compose.prod.yml")
$expectedServices = $builds.Keys
$missingFromBuilds = $composeImageServices | Where-Object { $_ -notin $expectedServices }
$missingFromCompose = $expectedServices | Where-Object { $_ -notin $composeImageServices }
if ($missingFromBuilds -or $missingFromCompose) {
    if ($missingFromBuilds) {
        Write-Host "  docker-compose.prod.yml references these image-tagged services with no matching build entry: $($missingFromBuilds -join ', ')" -ForegroundColor Red
    }
    if ($missingFromCompose) {
        Write-Host "  `$builds has entries docker-compose.prod.yml doesn't reference by tag: $($missingFromCompose -join ', ')" -ForegroundColor Red
    }
    Send-Step "preflight" "Failed" "Compose/build-list drift"
    throw "Compose image coordinates and the build list have drifted apart. Fix `$builds or docker-compose.prod.yml before packaging."
}
Write-Host "  OK — $($composeImageServices.Count) services match." -ForegroundColor Green
Send-Step "preflight" "Done" "$($composeImageServices.Count) services match"

if (-not $SkipBuild) {
    $buildIndex = 0
    foreach ($name in $builds.Keys) {
        $buildIndex += 1
        $image = "$Prefix/$name`:$Tag"
        Send-Step "build" "Running" "Building $name ($buildIndex of $($builds.Count))"
        Write-Host "==> Building $image ($($builds[$name]))" -ForegroundColor Cyan
        $exitCode = Invoke-NativeWithLines -Exe "docker" -Args @("build", "-f", $builds[$name], "-t", $image, ".")
        if ($exitCode -ne 0) {
            Send-Step "build" "Failed" "Build failed for $name"
            throw "Build failed for $name"
        }
    }
    Send-Step "build" "Done" "$($builds.Count) of $($builds.Count) images built"
}

# --- Save + per-image tag verification against the actual saved tar. ---
$imageManifestEntries = [System.Collections.Generic.List[object]]::new()
$saveIndex = 0
foreach ($name in $builds.Keys) {
    $saveIndex += 1
    $image = "$Prefix/$name`:$Tag"
    $tar   = Join-Path $images "$name.tar"
    Send-Step "save" "Running" "Saving $name ($saveIndex of $($builds.Count))"
    Write-Host "==> Saving $image -> $tar" -ForegroundColor Cyan
    $exitCode = Invoke-NativeWithLines -Exe "docker" -Args @("save", "-o", $tar, $image)
    if ($exitCode -ne 0) {
        Send-Step "save" "Failed" "docker save failed for $name"
        throw "docker save failed for $name"
    }

    $repoTags = Get-TarRepoTags -TarPath $tar
    if ($repoTags -notcontains $image) {
        Send-Step "save" "Failed" "Tag mismatch for $name"
        throw "Image-tag verification failed for $name`: $tar has RepoTags [$($repoTags -join ', ')], expected `"$image`"."
    }
    $imageManifestEntries.Add([ordered]@{
        service  = $name
        image    = $image
        tarFile  = "images/$name.tar"
        sizeBytes = (Get-Item -LiteralPath $tar).Length
        repoTags = $repoTags
    })
}
Write-Host "  OK — every saved tar's RepoTags matches its expected coordinate." -ForegroundColor Green
Send-Step "save" "Done" "$($builds.Count) of $($builds.Count) images saved"

# --- Config + compose the server needs alongside the image tars. db-init/rabbitmq keep the
# same ops/-relative nesting inside release/<Tag>/ as in the repo, so docker-compose.prod.yml's
# relative volume paths resolve identically whether it runs from the repo root or from an
# unpacked release/<Tag>/. deploy.sh and friends land flat at release/<Tag>/'s top level (they
# cd to their own folder and expect docker-compose.prod.yml + .env.prod as siblings there). Only
# the 3 rabbitmq files prod's compose actually mounts are copied — not the dev-only
# docker-compose.yml/.env.example that used to come along for the ride. ---
Send-Step "package" "Running" "Copying config and deploy scripts"
Write-Host "==> Copying compose, config, and deploy scripts into release/$Tag" -ForegroundColor Cyan
Copy-Item (Join-Path $root "docker-compose.prod.yml") $releaseDir -Force
Copy-Item (Join-Path $root "ops/postgres/db-init") (Join-Path $releaseDir "ops/postgres/db-init") -Recurse -Force
New-Item -ItemType Directory -Force -Path (Join-Path $releaseDir "ops/rabbitmq") | Out-Null
foreach ($rabbitmqFile in @("rabbitmq.conf", "enabled_plugins", "provision.sh")) {
    Copy-Item (Join-Path $root "ops/rabbitmq" $rabbitmqFile) (Join-Path $releaseDir "ops/rabbitmq" $rabbitmqFile) -Force
}
Copy-Item (Join-Path $root "ops/deploy/deploy.sh")            $releaseDir -Force
Copy-Item (Join-Path $root "ops/deploy/init-letsencrypt.sh")  $releaseDir -Force
Copy-Item (Join-Path $root "ops/deploy/.env.prod.example")    $releaseDir -Force

# --- Secret rejection: fail if anything secret-shaped made it into the bundle. Should always
# pass by construction (the copy list above never touches .env.prod), but this is the active,
# defense-in-depth guard that catches a future copy-list mistake. ---
Write-Host "==> Scanning release/$Tag for secret-shaped files" -ForegroundColor Cyan
$secretFiles = Find-SecretFiles -Directory $releaseDir
if ($secretFiles) {
    $secretFiles | ForEach-Object { Write-Host "  SECRET-SHAPED FILE: $_" -ForegroundColor Red }
    Send-Step "package" "Failed" "Secret-shaped file(s) found"
    throw "Refusing to package: secret-shaped file(s) found in release/$Tag."
}
Write-Host "  OK — no secret-shaped files." -ForegroundColor Green

# --- Bundle content allowlist: fail if anything not on the explicit list snuck in. ---
Write-Host "==> Checking release/$Tag against the bundle content allowlist" -ForegroundColor Cyan
$disallowed = Find-DisallowedFiles -Directory $releaseDir -AllowedPatterns $script:ReleaseBundleAllowlist
if ($disallowed) {
    $disallowed | ForEach-Object { Write-Host "  NOT ON ALLOWLIST: $_" -ForegroundColor Red }
    Send-Step "package" "Failed" "File(s) outside the bundle allowlist"
    throw "Refusing to package: file(s) outside the bundle content allowlist found in release/$Tag."
}
Write-Host "  OK — every file is on the allowlist." -ForegroundColor Green

# --- Deliberate exception to "never bundle secrets": only runs if the caller explicitly
# passed -EnvProdPath, and only AFTER the secret-shaped-file scan and allowlist check above,
# so it can never trip either guard. release/<Tag>/.env.prod (if present) should be handled
# like any other secret file from here on — don't upload/share the folder carelessly. ---
$envProdIncluded = $false
if ($EnvProdPath) {
    if (-not (Test-Path $EnvProdPath)) {
        throw "EnvProdPath was set but the file doesn't exist: $EnvProdPath"
    }
    Copy-Item -LiteralPath $EnvProdPath -Destination (Join-Path $releaseDir ".env.prod") -Force
    $envProdIncluded = $true
    Write-Host "==> Included .env.prod in release/$Tag (contains real secrets — handle this folder accordingly)" -ForegroundColor Yellow
}

# --- Manifest: replaces release.info (confirmed unread by deploy.sh or anything else) with a
# record an operator can actually use to see what's deployable and why. ---
$gitCommit = (git rev-parse HEAD 2>$null)
$gitBranch = (git rev-parse --abbrev-ref HEAD 2>$null)
$gitDirty  = [bool](git status --porcelain 2>$null)
$manifest = [ordered]@{
    tag          = $Tag
    imagePrefix  = $Prefix
    builtAtUtc   = (Get-Date).ToUniversalTime().ToString("o")
    builtBy      = "$env:USERNAME@$env:COMPUTERNAME"
    gitCommit    = $gitCommit
    gitBranch    = $gitBranch
    gitDirty     = $gitDirty
    envProdIncluded = $envProdIncluded
    images       = $imageManifestEntries
}
$manifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $releaseDir "manifest.json")
Send-Step "package" "Done" "release/$Tag ready"

Write-Host ""
Write-Host "Release ready in: $releaseDir" -ForegroundColor Green
Write-Host "Next: FileZilla the ENTIRE 'release/$Tag' folder to the VM, then on the VM run deploy.sh." -ForegroundColor Green
