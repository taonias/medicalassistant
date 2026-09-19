# Pure, side-effect-free checks used by package-release.ps1. Kept in a
# separate dot-sourceable file so each check can be exercised on its own
# (e.g. against a hand-built fixture, or against an already-assembled
# release/ folder) without triggering a real build.

<#
.SYNOPSIS
  Returns the names of the compose services whose `image:` is templated as
  ${IMAGE_PREFIX...}/<name>:${IMAGE_TAG...} — i.e. the app images this
  release packages, as opposed to postgres/rabbitmq/azurite/certbot, which
  pull fixed images from Docker Hub.
#>
function Get-TagTemplatedServices {
    param([Parameter(Mandatory)][string]$ComposeFilePath)

    $currentService = $null
    $services = [System.Collections.Generic.List[string]]::new()

    foreach ($line in Get-Content -LiteralPath $ComposeFilePath) {
        if ($line -match '^  ([a-zA-Z0-9_-]+):\s*$') {
            $currentService = $Matches[1]
        }
        elseif ($currentService -and $line -match '^\s{4}image:\s*\$\{IMAGE_PREFIX') {
            $services.Add($currentService)
            $currentService = $null
        }
    }

    return $services.ToArray()
}

<#
.SYNOPSIS
  Extracts the RepoTags docker save baked into a .tar, without needing the
  Docker daemon — docker save's tar always contains a top-level manifest.json
  with a RepoTags array per image.
#>
function Get-TarRepoTags {
    param([Parameter(Mandatory)][string]$TarPath)

    $json = & tar -xO -f $TarPath manifest.json 2>$null
    if (-not $json) {
        throw "Could not read manifest.json from tar: $TarPath"
    }
    $manifest = $json | ConvertFrom-Json

    $repoTags = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in $manifest) {
        foreach ($tag in $entry.RepoTags) { $repoTags.Add($tag) }
    }
    return $repoTags.ToArray()
}

<#
.SYNOPSIS
  Finds files in $Directory that look like secrets and must never ship in a
  release bundle. Returns an empty array when clean.
#>
function Find-SecretFiles {
    param([Parameter(Mandatory)][string]$Directory)

    $patterns = @('.env.prod', '*.pem', '*.key', '*.pfx', 'id_rsa*')
    $violations = [System.Collections.Generic.List[string]]::new()

    Get-ChildItem -LiteralPath $Directory -Recurse -File | ForEach-Object {
        foreach ($pattern in $patterns) {
            if ($_.Name -like $pattern) {
                $violations.Add($_.FullName)
                break
            }
        }
    }
    return $violations.ToArray()
}

<#
.SYNOPSIS
  Finds files in $Directory whose path (relative to $Directory, forward
  slashes) doesn't match any pattern in $AllowedPatterns. Returns an empty
  array when the whole tree is on the allowlist.
#>
function Find-DisallowedFiles {
    param(
        [Parameter(Mandatory)][string]$Directory,
        [Parameter(Mandatory)][string[]]$AllowedPatterns
    )

    $baseFull = (Resolve-Path -LiteralPath $Directory).Path
    $violations = [System.Collections.Generic.List[string]]::new()

    Get-ChildItem -LiteralPath $Directory -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($baseFull.Length + 1) -replace '\\', '/'
        $allowed = $false
        foreach ($pattern in $AllowedPatterns) {
            if ($relative -like $pattern) { $allowed = $true; break }
        }
        if (-not $allowed) { $violations.Add($relative) }
    }
    return $violations.ToArray()
}

<#
.SYNOPSIS
  Every service name under the compose file's top-level `services:` block, in file
  order — used by deploy-ui.ps1 to populate the Live Logs service picker without
  hardcoding a list that can drift from docker-compose.prod.yml.
#>
function Get-ComposeServiceNames {
    param([Parameter(Mandatory)][string]$ComposeFilePath)

    $inServices = $false
    $services = [System.Collections.Generic.List[string]]::new()

    foreach ($line in Get-Content -LiteralPath $ComposeFilePath) {
        if ($line -match '^(\S.*):\s*$') {
            $inServices = ($Matches[1] -eq 'services')
            continue
        }
        if ($inServices -and $line -match '^  ([a-zA-Z0-9_-]+):\s*$') {
            $services.Add($Matches[1])
        }
    }
    return $services.ToArray()
}

<#
.SYNOPSIS
  Parses simple KEY=value lines (no quoting/escaping — matches what every .env file in this
  repo actually uses) from $Path into a hashtable. Returns @{} if the file doesn't exist.
#>
function Get-DotEnvValues {
    param([Parameter(Mandatory)][string]$Path)

    $values = @{}
    if (-not (Test-Path -LiteralPath $Path)) { return $values }
    foreach ($line in Get-Content -LiteralPath $Path) {
        if ($line -match '^([A-Z0-9_]+)=(.*)$') { $values[$Matches[1]] = $Matches[2] }
    }
    return $values
}

<#
.SYNOPSIS
  Ensures $SecretsPath exists, creating it from VM_HOST/VM_PORT/VM_USER/VM_PASSWORD/
  VM_REMOTE_PATH in $EnvPath (the repo-root .env you already maintain for local dev) the
  first time it's missing. An existing secrets.json is left untouched — this only fills the
  gap so you don't have to hand-maintain both files; it's not a sync/overwrite step.
#>
function Get-OrCreateVmSecretsFile {
    param(
        [Parameter(Mandatory)][string]$SecretsPath,
        [Parameter(Mandatory)][string]$EnvPath
    )

    if (Test-Path $SecretsPath) { return }

    if (-not (Test-Path $EnvPath)) {
        throw "Neither $SecretsPath nor $EnvPath exist. Add VM_HOST/VM_PORT/VM_USER/VM_PASSWORD/VM_REMOTE_PATH to $EnvPath, or create $SecretsPath by hand (see secrets.json.example)."
    }

    $envVars = Get-DotEnvValues -Path $EnvPath
    foreach ($required in @('VM_HOST', 'VM_USER', 'VM_PASSWORD', 'VM_REMOTE_PATH')) {
        if (-not $envVars[$required]) {
            throw "$EnvPath is missing $required. Add it (see the VM connection section at the top of .env) or create $SecretsPath by hand."
        }
    }

    $secrets = [ordered]@{
        host       = $envVars['VM_HOST']
        port       = if ($envVars['VM_PORT']) { [int]$envVars['VM_PORT'] } else { 22 }
        user       = $envVars['VM_USER']
        password   = $envVars['VM_PASSWORD']
        remotePath = $envVars['VM_REMOTE_PATH']
    }
    $secrets | ConvertTo-Json | Set-Content -LiteralPath $SecretsPath
}

# The exact allowlist a packaged release/<tag>/ must satisfy.
$script:ReleaseBundleAllowlist = @(
    'manifest.json',
    'docker-compose.prod.yml',
    'deploy.sh',
    'init-letsencrypt.sh',
    '.env.prod.example',
    'images/*.tar',
    'ops/postgres/db-init/*',
    'ops/rabbitmq/rabbitmq.conf',
    'ops/rabbitmq/enabled_plugins',
    'ops/rabbitmq/provision.sh'
)
