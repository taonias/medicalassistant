#!/usr/bin/env pwsh
# Uploads an already-packaged release/<Tag>/ folder to the production VM (SCP) and runs
# deploy.sh there over SSH — the automated replacement for the manual "FileZilla + SSH in
# and run ./deploy.sh" steps in ops/deploy/DEPLOYMENT.md.
#
# Requires:
#   - release/<Tag>/ to already exist (run scripts/release/package-release.ps1 -Tag <Tag> first)
#   - the Posh-SSH module: Install-Module -Name Posh-SSH -Scope CurrentUser
#   - scripts/release/secrets.json (gitignored — copy secrets.json.example and fill in the
#     VM's host/user/port/password/remotePath). This file holds a plaintext password by
#     design (you chose password auth over an SSH key) — it never leaves this machine and is
#     never committed.
#
# What it does:
#   1. Uploads release/<Tag>/ to <remotePath> on the VM (overwrites images/manifest/compose/
#      scripts; leaves .env.prod and anything else already there untouched — same as the
#      manual FileZilla step).
#   2. SSHes in, chmods the shell scripts, and runs ./deploy.sh (or --no-load with -NoLoad).
#
# Usage:
#   ./scripts/release/deploy-remote.ps1                  # tag "latest"
#   ./scripts/release/deploy-remote.ps1 -Tag 2026-09-19
#   ./scripts/release/deploy-remote.ps1 -NoLoad           # skip re-loading tars on the VM
param(
    [string]$Tag = "latest",
    [switch]$NoLoad
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)   # repo root (two levels up)
$releaseDir = Join-Path $root "release" $Tag

if (-not (Test-Path $releaseDir)) {
    throw "release/$Tag not found. Run ./scripts/release/package-release.ps1 -Tag $Tag first."
}

$secretsPath = Join-Path $PSScriptRoot "secrets.json"
if (-not (Test-Path $secretsPath)) {
    throw "Missing $secretsPath. Copy scripts/release/secrets.json.example to secrets.json and fill in the VM's connection details."
}
$secrets = Get-Content -LiteralPath $secretsPath -Raw | ConvertFrom-Json
foreach ($field in @("host", "user", "password", "remotePath")) {
    if (-not $secrets.$field) { throw "$secretsPath is missing required field: $field" }
}
$vmHost     = $secrets.host
$vmUser     = $secrets.user
$vmPort     = if ($secrets.port) { [int]$secrets.port } else { 22 }
$remotePath = $secrets.remotePath.TrimEnd('/')
$lastSlash  = $remotePath.LastIndexOf('/')
if ($lastSlash -lt 0) { throw "secrets.json remotePath must be an absolute path, got: $remotePath" }
$remoteParent = $remotePath.Substring(0, $lastSlash)
$remoteLeaf   = $remotePath.Substring($lastSlash + 1)

$securePw = ConvertTo-SecureString $secrets.password -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($vmUser, $securePw)

if (-not (Get-Module -ListAvailable -Name Posh-SSH)) {
    throw "Posh-SSH module not found. Install it: Install-Module -Name Posh-SSH -Scope CurrentUser"
}
Import-Module Posh-SSH -ErrorAction Stop

Write-Host "==> Ensuring $remoteParent exists on ${vmUser}@${vmHost}:$vmPort" -ForegroundColor Cyan
$session = New-SSHSession -ComputerName $vmHost -Port $vmPort -Credential $cred -AcceptKey
try {
    Invoke-SSHCommand -SSHSession $session -Command "mkdir -p '$remoteParent'" | Out-Null
} finally {
    Remove-SSHSession -SSHSession $session | Out-Null
}

# SCP nests the uploaded folder under -NewName at -Destination, so this lands the LOCAL
# release/<Tag>/ folder (whatever it's named) at exactly <remotePath> on the VM regardless
# of Tag — matching the fixed path deploy.sh and .env.prod (set up once on the VM) expect.
Write-Host "==> Uploading release/$Tag -> ${vmUser}@${vmHost}:$remotePath (this can take a while for large image tars)" -ForegroundColor Cyan
Set-SCPItem -ComputerName $vmHost -Port $vmPort -Credential $cred -Path $releaseDir -Destination $remoteParent -NewName $remoteLeaf -AcceptKey -Verbose

Write-Host "==> Running deploy.sh on the VM ($remotePath)" -ForegroundColor Cyan
$session = New-SSHSession -ComputerName $vmHost -Port $vmPort -Credential $cred -AcceptKey
try {
    $deployFlag = if ($NoLoad) { " --no-load" } else { "" }
    $remoteCmd = "cd '$remotePath' && chmod +x deploy.sh init-letsencrypt.sh 2>/dev/null; ./deploy.sh$deployFlag"
    $exitStatus = $null
    Invoke-SSHCommandStream -SSHSession $session -Command $remoteCmd -ExitStatusVariable "exitStatus" | ForEach-Object { Write-Host $_ }
    if ($exitStatus -ne 0) {
        throw "deploy.sh exited with status $exitStatus on the VM."
    }
} finally {
    Remove-SSHSession -SSHSession $session | Out-Null
}

Write-Host ""
Write-Host "Deploy complete." -ForegroundColor Green
