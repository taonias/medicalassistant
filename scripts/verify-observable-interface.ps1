[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$scriptDirectory = Split-Path -Parent $PSCommandPath
$repositoryRoot = (Resolve-Path (Join-Path $scriptDirectory "..")).Path
$snapshotPath = Join-Path $repositoryRoot "docs/architecture/contracts/observable-interface.snapshot.json"
$snapshot = Get-Content -Raw -LiteralPath $snapshotPath | ConvertFrom-Json

function Assert-EqualSet {
    param(
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [object[]] $Expected,
        [Parameter(Mandatory)] [object[]] $Actual
    )

    $expectedValues = @($Expected | ForEach-Object { "$_" } | Sort-Object -Unique)
    $actualValues = @($Actual | ForEach-Object { "$_" } | Sort-Object -Unique)
    $difference = Compare-Object -ReferenceObject $expectedValues -DifferenceObject $actualValues
    if ($difference) {
        $rendered = $difference | Format-Table -AutoSize | Out-String
        throw "$Name changed from the committed observable-interface snapshot.$([Environment]::NewLine)$rendered"
    }
}

function Get-ControllerRoutes {
    param([Parameter(Mandatory)] [string[]] $ControllerDirectories)

    foreach ($controllerDirectory in $ControllerDirectories) {
        Get-ChildItem -LiteralPath $controllerDirectory -Filter "*Controller.cs" -File | ForEach-Object {
            $controllerFile = $_
            $controllerName = $controllerFile.BaseName -replace "Controller$", ""
            $sourceLines = Get-Content -LiteralPath $controllerFile.FullName
            $baseRoute = $null

            foreach ($sourceLine in $sourceLines) {
                if (-not $baseRoute -and $sourceLine -match '^\s*\[Route\("([^"]+)"\)\]') {
                    $baseRoute = $Matches[1].Replace("[controller]", $controllerName)
                    continue
                }

                if ($sourceLine -match '^\s*\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]') {
                    if (-not $baseRoute) {
                        throw "No controller route was found for $($controllerFile.FullName)."
                    }

                    $method = $Matches[1].ToUpperInvariant()
                    $actionRoute = $Matches[2]
                    if ($null -eq $actionRoute) {
                        $actionRoute = ""
                    }
                    if ($actionRoute.StartsWith("/")) {
                        $route = $actionRoute
                    }
                    elseif ([string]::IsNullOrWhiteSpace($actionRoute)) {
                        $route = "/$baseRoute"
                    }
                    else {
                        $route = "/$baseRoute/$actionRoute"
                    }

                    "$method $($route -replace '/+', '/')"
                }
            }
        }
    }
}

function Get-EndpointStatusSignatures {
    param([Parameter(Mandatory)] [string[]] $ControllerDirectories)

    $statusMap = @{
        Ok = "200"
        CreatedAtAction = "201"
        Accepted = "202"
        NoContent = "204"
        Unauthorized = "401"
        NotFound = "404"
        BadRequest = "400"
        Conflict = "409"
        File = "200"
    }

    foreach ($controllerDirectory in $ControllerDirectories) {
        Get-ChildItem -LiteralPath $controllerDirectory -Filter "*Controller.cs" -File | ForEach-Object {
            $controllerFile = $_
            $controllerName = $controllerFile.BaseName -replace "Controller$", ""
            $source = Get-Content -Raw -LiteralPath $controllerFile.FullName
            $baseMatch = [regex]::Match($source, '(?m)^\s*\[Route\("([^"]+)"\)\]')
            $baseRoute = $baseMatch.Groups[1].Value.Replace("[controller]", $controllerName)
            $actionMatches = [regex]::Matches(
                $source,
                '(?m)^\s*\[Http(Get|Post|Put|Patch|Delete)(?:\("([^"]*)"\))?\]')

            for ($index = 0; $index -lt $actionMatches.Count; $index++) {
                $actionMatch = $actionMatches[$index]
                $endIndex = if ($index + 1 -lt $actionMatches.Count) {
                    $actionMatches[$index + 1].Index
                }
                else {
                    $source.Length
                }
                $actionBlock = $source.Substring($actionMatch.Index, $endIndex - $actionMatch.Index)
                $method = $actionMatch.Groups[1].Value.ToUpperInvariant()
                $actionRoute = $actionMatch.Groups[2].Value
                if ($actionRoute.StartsWith("/")) {
                    $route = $actionRoute
                }
                elseif ([string]::IsNullOrWhiteSpace($actionRoute)) {
                    $route = "/$baseRoute"
                }
                else {
                    $route = "/$baseRoute/$actionRoute"
                }
                $route = $route -replace '/+', '/'

                $statuses = @()
                foreach ($statusMatch in [regex]::Matches($actionBlock, 'StatusCodes\.Status([0-9]{3})[A-Za-z]+')) {
                    $statuses += $statusMatch.Groups[1].Value
                }
                foreach ($methodMatch in [regex]::Matches(
                    $actionBlock,
                    '\b(Ok|CreatedAtAction|Accepted|NoContent|Unauthorized|NotFound|BadRequest|Conflict|File)\s*\(')) {
                    $statuses += $statusMap[$methodMatch.Groups[1].Value]
                }
                if ($actionBlock -match 'enableRangeProcessing:\s*true') {
                    $statuses += "206"
                }

                $statusSignature = @($statuses | Sort-Object -Unique) -join ","
                "{0} {1}={2}" -f $method, $route, $statusSignature
            }
        }
    }
}

function Assert-SourceAssertions {
    param(
        [Parameter(Mandatory)] [string] $Name,
        [Parameter(Mandatory)] [object[]] $Assertions
    )

    foreach ($assertion in $Assertions) {
        $sourcePath = Join-Path $repositoryRoot $assertion.path
        $sourceText = Get-Content -Raw -LiteralPath $sourcePath
        foreach ($requiredText in $assertion.contains) {
            if (-not $sourceText.Contains($requiredText)) {
                throw "$Name assertion missing from $($assertion.path): $requiredText"
            }
        }
    }
}

$controllerDirectories = @(
    (Join-Path $repositoryRoot "backend/src/MedicalAssistant.Api/Controllers"),
    (Join-Path $repositoryRoot "AI/src/MedicalAssistance.Ingestion.Api/Controllers")
)
Assert-EqualSet -Name "HTTP routes" -Expected $snapshot.httpRoutes -Actual @(Get-ControllerRoutes $controllerDirectories)
Assert-EqualSet -Name "Endpoint status signatures" -Expected $snapshot.endpointStatusSignatures -Actual @(
    Get-EndpointStatusSignatures $controllerDirectories
)

$backendProgram = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "backend/src/MedicalAssistant.Api/Program.cs")
$clinicalProgram = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "AI/src/MedicalAssistance.Ingestion.Api/Program.cs")
$apiKeySource = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "AI/src/MedicalAssistance.Ingestion.Api/Security/ApiKeyAuthentication.cs")
$backendSecurityName = [regex]::Match($backendProgram, 'AddSecurityDefinition\("([^"]+)"').Groups[1].Value
$clinicalSecurityName = [regex]::Match($apiKeySource, 'SchemeName = "([^"]+)"').Groups[1].Value
$clinicalSecurityHeader = [regex]::Match($apiKeySource, 'HeaderName = "([^"]+)"').Groups[1].Value
$actualOpenApi = @()
if ($backendProgram.Contains("app.UseSwagger();")) {
    $actualOpenApi += "backend:/swagger/v1/swagger.json:$backendSecurityName"
}
if ($clinicalProgram.Contains("app.UseSwagger();")) {
    $actualOpenApi += ("clinical-knowledge:/swagger/v1/swagger.json:{0}:{1}" -f $clinicalSecurityName, $clinicalSecurityHeader)
}
Assert-EqualSet -Name "OpenAPI endpoints/security" -Expected $snapshot.openApi -Actual $actualOpenApi

$backendHubPath = [regex]::Match($backendProgram, 'MapHub<[^>]*ChatHub>\("([^"]+)"\)').Groups[1].Value
$clinicalHubPath = [regex]::Match($clinicalProgram, 'MapHub<IngestionStatusHub>\("([^"]+)"\)').Groups[1].Value
$chatHubSource = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "backend/src/MedicalAssistant.Api/Realtime/ChatHub.cs")
$ingestionStatusSource = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "AI/src/MedicalAssistance.Ingestion.Api/Realtime/IngestionStatusPublisher.cs")
$chatClientMethod = [regex]::Match($chatHubSource, 'ClientMethod = "([^"]+)"').Groups[1].Value
$ingestionClientMethod = [regex]::Match($ingestionStatusSource, 'ClientMethod = "([^"]+)"').Groups[1].Value
Assert-EqualSet -Name "SignalR hubs/client methods" -Expected $snapshot.signalR -Actual @(
    ("{0}:{1}" -f $backendHubPath, $chatClientMethod),
    ("{0}:{1}" -f $clinicalHubPath, $ingestionClientMethod)
)

$eventContractSource = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "backend/src/MedicalAssistant.EventBus/Contracts/ConsultationIntegrationEvents.cs")
$actualEventRoutingKeys = @(
    [regex]::Matches($eventContractSource, '"((?:consultation|clinicalknowledge)\.[a-z-]+\.v[0-9]+)"') |
        ForEach-Object { $_.Groups[1].Value }
)
Assert-EqualSet -Name "Integration Event routing keys" -Expected $snapshot.eventRoutingKeys -Actual $actualEventRoutingKeys

$envelopeSource = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "backend/src/MedicalAssistant.EventBus/IntegrationEventEnvelope.cs")
$actualEnvelopeFields = @(
    [regex]::Matches($envelopeSource, 'JsonPropertyName\("([^"]+)"\)') |
        ForEach-Object { $_.Groups[1].Value }
)
Assert-EqualSet -Name "Integration Event envelope fields" -Expected $snapshot.eventEnvelopeFields -Actual $actualEnvelopeFields

$eventConstants = @{}
foreach ($constantMatch in [regex]::Matches(
    $eventContractSource,
    'const string ([A-Za-z0-9_]+) = "((?:consultation|clinicalknowledge)\.[a-z-]+\.v[0-9]+)"')) {
    $eventConstants[$constantMatch.Groups[1].Value] = $constantMatch.Groups[2].Value
}

$payloadSource = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "backend/src/MedicalAssistant.EventBus/Contracts/ConsultationContracts.cs")
$payloadFields = @{}
foreach ($recordMatch in [regex]::Matches(
    $payloadSource,
    'public sealed record ([A-Za-z0-9_]+)\((.*?)\);',
    [Text.RegularExpressions.RegexOptions]::Singleline)) {
    $payloadFields[$recordMatch.Groups[1].Value] = @(
        [regex]::Matches($recordMatch.Groups[2].Value, 'JsonPropertyName\("([^"]+)"\)') |
            ForEach-Object { $_.Groups[1].Value }
    )
}

$actualPayloadShapes = @()
foreach ($registryMatch in [regex]::Matches(
    $eventContractSource,
    '\.Add<([A-Za-z0-9_]+)>\(([A-Za-z0-9_]+),\s*[0-9]+\)')) {
    $payloadType = $registryMatch.Groups[1].Value
    $eventConstant = $registryMatch.Groups[2].Value
    $actualPayloadShapes += "{0}={1}" -f $eventConstants[$eventConstant], ($payloadFields[$payloadType] -join ",")
}
Assert-EqualSet -Name "Integration Event JSON payload shapes" -Expected $snapshot.eventPayloadShapes -Actual $actualPayloadShapes

[xml] $solution = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "MedicalAssistant.slnx")
$solutionProjects = @($solution.Solution.Folder.Project | ForEach-Object { $_.Path.Replace("\", "/") })
Assert-EqualSet -Name "Root solution membership" -Expected $snapshot.solutionProjects -Actual $solutionProjects

Push-Location $repositoryRoot
try {
    $composeServices = @(docker compose config --services)
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose config --services failed."
    }
    Assert-EqualSet -Name "Compose services" -Expected $snapshot.composeServices -Actual $composeServices

    $composeVolumes = @(docker compose config --volumes)
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose config --volumes failed."
    }
    Assert-EqualSet -Name "Compose volumes" -Expected $snapshot.composeVolumes -Actual $composeVolumes
}
finally {
    Pop-Location
}

foreach ($snapshotProperty in $snapshot.modelSnapshots.PSObject.Properties) {
    $modelPath = Join-Path $repositoryRoot $snapshotProperty.Name
    $modelText = (Get-Content -Raw -LiteralPath $modelPath).Replace("`r`n", "`n")
    $actualHash = [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($modelText)))
    if ($actualHash -ne $snapshotProperty.Value) {
        throw "EF model snapshot changed: $($snapshotProperty.Name). Expected $($snapshotProperty.Value), got $actualHash."
    }
}

foreach ($migrationProperty in $snapshot.migrationFiles.PSObject.Properties) {
    $migrationDirectory = Join-Path $repositoryRoot $migrationProperty.Name
    $actualMigrations = @(
        Get-ChildItem -LiteralPath $migrationDirectory -Filter "*.cs" -File |
            Where-Object { $_.Name -notlike "*.Designer.cs" -and $_.Name -notlike "*ModelSnapshot.cs" } |
            Select-Object -ExpandProperty Name
    )
    Assert-EqualSet -Name "Migration inventory for $($migrationProperty.Name)" -Expected $migrationProperty.Value -Actual $actualMigrations
}

Assert-SourceAssertions -Name "Status-code" -Assertions $snapshot.statusAssertions
Assert-SourceAssertions -Name "Configuration" -Assertions $snapshot.configurationAssertions
Assert-SourceAssertions -Name "DI lifetime" -Assertions $snapshot.diAssertions
Assert-SourceAssertions -Name "Observable source" -Assertions $snapshot.sourceAssertions

Write-Host "Observable interface snapshot verified."
