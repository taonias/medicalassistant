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
#   ./scripts/release/deploy-remote.ps1 -EnvProdPath ./scripts/release/.env.prod
#                                                          # also overwrite .env.prod on the VM
#   ./scripts/release/deploy-remote.ps1 -DeployOnly       # skip connect+upload; just (re)run
#                                                          # deploy.sh against whatever is
#                                                          # already on the VM right now
#   ./scripts/release/deploy-remote.ps1 -EnvOnly -EnvProdPath ./scripts/release/.env.prod
#                                                          # upload ONLY .env.prod; no images,
#                                                          # no deploy.sh run, containers untouched
param(
    [string]$Tag = "latest",
    [switch]$NoLoad,
    # Skips the connect/upload steps entirely and just (re)runs deploy.sh on the VM against
    # whatever release is already sitting at <remotePath> — for retrying a deploy.sh failure
    # (e.g. a bad .env.prod value) without rebuilding or re-uploading anything.
    [switch]$DeployOnly,
    # Uploads -EnvProdPath (required with this) to <remotePath>/.env.prod and stops — no image
    # upload, no deploy.sh, no IMAGE_TAG change, running containers left exactly as they are.
    # For fixing a bad value on the VM (e.g. a missing required var) without a restart.
    [switch]$EnvOnly,
    # When set, also uploads this local file to <remotePath>/.env.prod, overwriting whatever
    # is already on the VM. Off by default — .env.prod normally lives only on the VM (see
    # DEPLOYMENT.md) and this is meant for deliberate, reviewed pushes, not routine deploys.
    # Ignored when -DeployOnly is set (nothing is uploaded in that mode); required by -EnvOnly.
    [string]$EnvProdPath = $null,
    # Optional progress hooks for callers driving a UI (e.g. deploy-ui.ps1) on top of this
    # script. All are no-ops when not supplied, so plain CLI usage is unchanged.
    #   OnStep -Stage <string> -Status <'Running'|'Done'|'Failed'> -Detail <string>
    #   OnLine <string>  -- one line of raw SSH output (deploy.sh)
    #   OnUploadProgress -FileName <string> -FileUploaded <long> -FileSize <long>
    #                     -OverallUploaded <long> -OverallSize <long> -FileIndex <int> -FileCount <int>
    [scriptblock]$OnStep = $null,
    [scriptblock]$OnLine = $null,
    [scriptblock]$OnUploadProgress = $null
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)   # repo root (two levels up)
$releaseDir = Join-Path $root "release" $Tag

. (Join-Path $PSScriptRoot "PackageRelease.Checks.ps1")

function Send-Step {
    param([string]$Stage, [string]$Status, [string]$Detail = "")
    if ($OnStep) { & $OnStep $Stage $Status $Detail }
}

function Send-Line {
    param([string]$Line)
    if ($OnLine) { & $OnLine $Line }
}

function Format-ByteSize {
    param([long]$Bytes)
    if ($Bytes -ge 1GB) { return "{0:N2} GB" -f ($Bytes / 1GB) }
    if ($Bytes -ge 1MB) { return "{0:N1} MB" -f ($Bytes / 1MB) }
    if ($Bytes -ge 1KB) { return "{0:N0} KB" -f ($Bytes / 1KB) }
    return "$Bytes B"
}

<#
.SYNOPSIS
  Uploads every file under $LocalDir to $RemoteDir (same relative layout), using SSH.NET's
  ScpClient directly instead of Posh-SSH's Set-SCPItem — its Uploading event reports real
  bytes-transferred per file, which is what -OnUploadProgress needs. Console output (when no
  UI is attached) is throttled to avoid spamming the terminal on fast local-network transfers.
#>
function Send-DirectoryViaScp {
    param(
        [Parameter(Mandatory)][string]$VmHost,
        [Parameter(Mandatory)][int]$VmPort,
        [Parameter(Mandatory)][string]$VmUser,
        [Parameter(Mandatory)][string]$VmPassword,
        [Parameter(Mandatory)][string]$LocalDir,
        [Parameter(Mandatory)][string]$RemoteDir
    )

    $files = Get-ChildItem -LiteralPath $LocalDir -Recurse -File
    $totalSize = ($files | Measure-Object -Property Length -Sum).Sum
    $fileCount = $files.Count

    $scpCred = New-Object System.Management.Automation.PSCredential($VmUser, (ConvertTo-SecureString $VmPassword -AsPlainText -Force))

    # Pre-create every remote directory in one round trip (including $RemoteDir itself, for a
    # brand-new tag/first deploy) — SCP won't create intermediate directories for a single-file
    # upload on its own.
    $relativeDirs = $files |
        ForEach-Object { (Split-Path -Parent $_.FullName.Substring($LocalDir.Length + 1)) -replace '\\', '/' } |
        Select-Object -Unique
    $session = New-SSHSession -ComputerName $VmHost -Port $VmPort -Credential $scpCred -AcceptKey
    try {
        $dirArgs = (@($RemoteDir) + ($relativeDirs | Where-Object { $_ } | ForEach-Object { "$RemoteDir/$_" })) |
            ForEach-Object { "'$_'" }
        Invoke-SSHCommand -SSHSession $session -Command "mkdir -p $($dirArgs -join ' ')" | Out-Null
    } finally {
        Remove-SSHSession -SSHSession $session | Out-Null
    }

    $scp = New-Object Renci.SshNet.ScpClient($VmHost, $VmPort, $VmUser, $VmPassword)

    # Event handlers fire on every progress tick and must mutate the SAME counters across
    # calls, which plain closure-captured locals don't reliably do in PowerShell — script
    # scope is the standard way to share mutable state with a .NET event handler here.
    $script:ScpBytesFromCompletedFiles = 0L
    $script:ScpFileIndex = 0
    $script:ScpCurrentFile = $null
    $script:ScpLastConsoleUpdate = [DateTime]::MinValue

    $handler = {
        param($sender, $e)
        if ($e.Filename -ne $script:ScpCurrentFile) {
            $script:ScpFileIndex += 1
            $script:ScpCurrentFile = $e.Filename
        }
        $overall = $script:ScpBytesFromCompletedFiles + $e.Uploaded
        if ($OnUploadProgress) {
            & $OnUploadProgress $e.Filename $e.Uploaded $e.Size $overall $totalSize $script:ScpFileIndex $fileCount
        }
        if (-not $OnUploadProgress -and ((Get-Date) - $script:ScpLastConsoleUpdate).TotalSeconds -ge 1) {
            $pct = if ($totalSize -gt 0) { [Math]::Round(($overall / $totalSize) * 100, 1) } else { 0 }
            Write-Host ("  {0} ({1} of {2}) — {3}%" -f (Split-Path -Leaf $e.Filename), $script:ScpFileIndex, $fileCount, $pct)
            $script:ScpLastConsoleUpdate = Get-Date
        }
    }
    $scp.add_Uploading($handler)

    try {
        $scp.Connect()
        foreach ($file in $files) {
            $relativePath = $file.FullName.Substring($LocalDir.Length + 1) -replace '\\', '/'
            $scp.Upload([System.IO.FileInfo]$file.FullName, "$RemoteDir/$relativePath")
            $script:ScpBytesFromCompletedFiles += $file.Length
        }
    } finally {
        $scp.remove_Uploading($handler)
        if ($scp.IsConnected) { $scp.Disconnect() }
        $scp.Dispose()
        Remove-Variable -Name ScpBytesFromCompletedFiles, ScpFileIndex, ScpCurrentFile, ScpLastConsoleUpdate -Scope Script -ErrorAction SilentlyContinue
    }
}

<#
.SYNOPSIS
  Uploads a single local file to an exact remote path, reusing the same ScpClient + progress
  wiring as Send-DirectoryViaScp. Used for the optional .env.prod push, which is one file to
  a fixed destination rather than a whole directory tree.
#>
function Send-FileViaScp {
    param(
        [Parameter(Mandatory)][string]$VmHost,
        [Parameter(Mandatory)][int]$VmPort,
        [Parameter(Mandatory)][string]$VmUser,
        [Parameter(Mandatory)][string]$VmPassword,
        [Parameter(Mandatory)][string]$LocalFile,
        [Parameter(Mandatory)][string]$RemoteFile
    )

    $fileInfo = Get-Item -LiteralPath $LocalFile
    $totalSize = $fileInfo.Length
    $fileCount = 1

    $scp = New-Object Renci.SshNet.ScpClient($VmHost, $VmPort, $VmUser, $VmPassword)
    $handler = {
        param($sender, $e)
        if ($OnUploadProgress) {
            & $OnUploadProgress $e.Filename $e.Uploaded $e.Size $e.Uploaded $totalSize 1 $fileCount
        }
    }
    $scp.add_Uploading($handler)

    try {
        $scp.Connect()
        $scp.Upload($fileInfo, $RemoteFile)
    } finally {
        $scp.remove_Uploading($handler)
        if ($scp.IsConnected) { $scp.Disconnect() }
        $scp.Dispose()
    }
}

<#
.SYNOPSIS
  Updates (or adds) IMAGE_TAG=<Tag> in the VM's .env.prod via a surgical in-place edit over
  SSH — touches only that one line, so it's safe to run whether or not -EnvProdPath is set.
  deploy.sh reads IMAGE_TAG to pick which already-uploaded image tag to actually start;
  without this, the running containers could silently mismatch what was just built/uploaded.
#>
function Set-RemoteImageTag {
    param(
        [Parameter(Mandatory)][string]$VmHost,
        [Parameter(Mandatory)][int]$VmPort,
        [Parameter(Mandatory)][System.Management.Automation.PSCredential]$Credential,
        [Parameter(Mandatory)][string]$RemotePath,
        [Parameter(Mandatory)][string]$Tag
    )
    # Tags are simple date/version strings in practice, but escape sed's special characters
    # defensively rather than assume that.
    $escapedTag = $Tag -replace '\\', '\\\\' -replace '&', '\&' -replace '\|', '\|'
    $session = New-SSHSession -ComputerName $VmHost -Port $VmPort -Credential $Credential -AcceptKey
    try {
        $cmd = "cd '$RemotePath' && if [ -f .env.prod ]; then " +
            "if grep -q '^IMAGE_TAG=' .env.prod; then sed -i 's|^IMAGE_TAG=.*|IMAGE_TAG=$escapedTag|' .env.prod; " +
            "else echo 'IMAGE_TAG=$escapedTag' >> .env.prod; fi; echo UPDATED; else echo NO_ENV_FILE; fi"
        $result = Invoke-SSHCommand -SSHSession $session -Command $cmd
        return ($result.Output -join "`n").Trim()
    } finally {
        Remove-SSHSession -SSHSession $session | Out-Null
    }
}

if ($DeployOnly -and $EnvOnly) {
    throw "-DeployOnly and -EnvOnly are mutually exclusive."
}
if ($EnvOnly -and -not $EnvProdPath) {
    throw "-EnvOnly requires -EnvProdPath."
}
if (-not $DeployOnly -and -not $EnvOnly -and -not (Test-Path $releaseDir)) {
    throw "release/$Tag not found. Run ./scripts/release/package-release.ps1 -Tag $Tag first."
}

$secretsPath = Join-Path $PSScriptRoot "secrets.json"
Get-OrCreateVmSecretsFile -SecretsPath $secretsPath -EnvPath (Join-Path $root ".env")
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

$securePw = ConvertTo-SecureString $secrets.password -AsPlainText -Force
$cred = New-Object System.Management.Automation.PSCredential($vmUser, $securePw)

if (-not (Get-Module -ListAvailable -Name Posh-SSH)) {
    throw "Posh-SSH module not found. Install it: Install-Module -Name Posh-SSH -Scope CurrentUser"
}
Import-Module Posh-SSH -ErrorAction Stop

if ($DeployOnly) {
    Send-Step "connect" "Done" "Skipped (deploy.sh only)"
    Send-Step "upload" "Done" "Skipped (deploy.sh only)"
} elseif ($EnvOnly) {
    Send-Step "connect" "Done" "Skipped (.env.prod only)"
    if (-not (Test-Path $EnvProdPath)) {
        Send-Step "upload" "Failed" ".env.prod not found: $EnvProdPath"
        throw "EnvProdPath was set but the file doesn't exist: $EnvProdPath"
    }
    Send-Step "upload" "Running" "Uploading .env.prod only"
    Write-Host "==> Uploading .env.prod -> ${vmUser}@${vmHost}:$remotePath/.env.prod (nothing else — no images, no deploy.sh)" -ForegroundColor Yellow
    Send-Line "Uploading .env.prod only (overwriting the VM's copy; no images touched, deploy.sh will NOT run, running containers stay as they are)..."
    Send-FileViaScp -VmHost $vmHost -VmPort $vmPort -VmUser $vmUser -VmPassword $secrets.password -LocalFile $EnvProdPath -RemoteFile "$remotePath/.env.prod"
    Send-Step "upload" "Done" ".env.prod uploaded"
} else {
    Send-Step "connect" "Running" "Connecting to ${vmUser}@${vmHost}:$vmPort"
    Write-Host "==> Ensuring $remoteParent exists on ${vmUser}@${vmHost}:$vmPort" -ForegroundColor Cyan
    $session = New-SSHSession -ComputerName $vmHost -Port $vmPort -Credential $cred -AcceptKey
    try {
        Invoke-SSHCommand -SSHSession $session -Command "mkdir -p '$remoteParent'" | Out-Null
    } finally {
        Remove-SSHSession -SSHSession $session | Out-Null
    }
    Send-Step "connect" "Done" "Connected"

    # Uploads directly to <remotePath> (not $remoteParent/-NewName) — Send-DirectoryViaScp mirrors
    # release/<Tag>/'s own layout at $remotePath, matching the fixed path deploy.sh and .env.prod
    # (set up once on the VM) expect, regardless of Tag.
    Send-Step "upload" "Running" "Uploading release/$Tag (this can take a while for large image tars)"
    Write-Host "==> Uploading release/$Tag -> ${vmUser}@${vmHost}:$remotePath (this can take a while for large image tars)" -ForegroundColor Cyan
    Send-DirectoryViaScp -VmHost $vmHost -VmPort $vmPort -VmUser $vmUser -VmPassword $secrets.password -LocalDir $releaseDir -RemoteDir $remotePath

    if ($EnvProdPath) {
        if (-not (Test-Path $EnvProdPath)) {
            Send-Step "upload" "Failed" ".env.prod not found: $EnvProdPath"
            throw "EnvProdPath was set but the file doesn't exist: $EnvProdPath"
        }
        Write-Host "==> Also uploading .env.prod -> ${vmUser}@${vmHost}:$remotePath/.env.prod" -ForegroundColor Yellow
        Send-Line "Also uploading .env.prod (overwriting the VM's copy)..."
        Send-FileViaScp -VmHost $vmHost -VmPort $vmPort -VmUser $vmUser -VmPassword $secrets.password -LocalFile $EnvProdPath -RemoteFile "$remotePath/.env.prod"
    }

    Send-Step "upload" "Done" "Upload complete"
}

if ($EnvOnly) {
    Send-Step "deploy" "Done" "Skipped (.env.prod only — deploy.sh was not run)"
    Write-Host ""
    Write-Host ".env.prod uploaded. deploy.sh was NOT run — the running containers are untouched." -ForegroundColor Green
    return
}

Send-Step "deploy" "Running" "Setting IMAGE_TAG=$Tag on the VM"
Write-Host "==> Setting IMAGE_TAG=$Tag in .env.prod on the VM" -ForegroundColor Cyan
Send-Line "Setting IMAGE_TAG=$Tag in .env.prod on the VM (so deploy.sh starts the tag you just built, not whatever it was set to before)..."
$tagUpdateResult = Set-RemoteImageTag -VmHost $vmHost -VmPort $vmPort -Credential $cred -RemotePath $remotePath -Tag $Tag
if ($tagUpdateResult -eq "NO_ENV_FILE") {
    Write-Host "  WARNING: .env.prod not found on the VM yet — IMAGE_TAG was not set. deploy.sh will fail until .env.prod exists." -ForegroundColor Yellow
    Send-Line "WARNING: .env.prod not found on the VM — IMAGE_TAG was NOT updated. deploy.sh will likely fail until .env.prod exists (see DEPLOYMENT.md step 3, or use the 'Also upload .env.prod' checkbox)."
} else {
    Write-Host "  IMAGE_TAG=$Tag set." -ForegroundColor Cyan
    Send-Line "IMAGE_TAG=$Tag is now set in .env.prod on the VM."
}

Send-Step "deploy" "Running" "Running deploy.sh on the VM"
Write-Host "==> Running deploy.sh on the VM ($remotePath)" -ForegroundColor Cyan
$session = New-SSHSession -ComputerName $vmHost -Port $vmPort -Credential $cred -AcceptKey
try {
    $deployFlag = if ($NoLoad) { " --no-load" } else { "" }
    $remoteCmd = "cd '$remotePath' && chmod +x deploy.sh init-letsencrypt.sh 2>/dev/null; ./deploy.sh$deployFlag"
    $exitStatus = $null
    # Keep a rolling tail plus anything that looks like an actual error, so a failure carries
    # enough of deploy.sh's own output to explain itself instead of just an exit code.
    $tailLines = [System.Collections.Generic.List[string]]::new()
    $flaggedLines = [System.Collections.Generic.List[string]]::new()
    $maxTail = 25
    $errorPattern = '(?i)(error|exception|fail|cannot|refused|fatal|panic|unable)'
    Invoke-SSHCommandStream -SSHSession $session -Command $remoteCmd -ExitStatusVariable "exitStatus" | ForEach-Object {
        Write-Host $_
        Send-Line "$_"
        $tailLines.Add("$_")
        if ($tailLines.Count -gt $maxTail) { $tailLines.RemoveAt(0) }
        if ("$_" -match $errorPattern) { $flaggedLines.Add("$_") }
    }
    if ($exitStatus -ne 0) {
        Send-Step "deploy" "Failed" "exit code $exitStatus — see error details"
        $parts = [System.Collections.Generic.List[string]]::new()
        $parts.Add("deploy.sh exited with status $exitStatus on the VM.")
        if ($flaggedLines.Count -gt 0) {
            $parts.Add("")
            $parts.Add("Lines that look like the cause:")
            $parts.AddRange([string[]]($flaggedLines | Select-Object -Last 8))
        }
        $parts.Add("")
        $parts.Add("Last $($tailLines.Count) line(s) of output:")
        $parts.AddRange([string[]]$tailLines)
        throw ($parts -join "`n")
    }
} finally {
    Remove-SSHSession -SSHSession $session | Out-Null
}
Send-Step "deploy" "Done" "deploy.sh finished"

Write-Host ""
Write-Host "Deploy complete." -ForegroundColor Green
