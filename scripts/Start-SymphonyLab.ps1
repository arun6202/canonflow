#Requires -Version 7.0
[CmdletBinding()]
[OutputType([pscustomobject])]
param(
    [Parameter()]
    [ValidateSet('all', 'es8', 'es9', 'oracle')]
    [string[]] $Service = @('all'),

    [Parameter()]
    [ValidateRange(30, 1800)]
    [int] $TimeoutSeconds = 900,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $ComposeFile = (Join-Path $PSScriptRoot '..\docker-compose.local.yml')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-LabService {
    [CmdletBinding()]
    [OutputType([string[]])]
    param(
        [Parameter(Mandatory)]
        [string[]] $Name
    )

    if ($Name -contains 'all') {
        return @('es8', 'es9', 'oracle')
    }

    $Name
}

function Invoke-DockerCompose {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]] $Argument
    )

    $composePath = (Resolve-Path -LiteralPath $ComposeFile).ProviderPath
    & docker compose -f $composePath @Argument

    if ($LASTEXITCODE -ne 0) {
        throw "docker compose failed with exit code $LASTEXITCODE."
    }
}

function Wait-HttpEndpoint {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string] $Name,

        [Parameter(Mandatory)]
        [uri] $Uri,

        [Parameter(Mandatory)]
        [datetime] $Deadline
    )

    do {
        try {
            $response = Invoke-RestMethod -Uri $Uri -TimeoutSec 5 -ErrorAction Stop
            return [pscustomobject]@{
                Service = $Name
                Status  = 'ready'
                Url     = $Uri.AbsoluteUri
                Version = $response.version.number
            }
        }
        catch {
            Start-Sleep -Seconds 5
        }
    } while ((Get-Date) -lt $Deadline)

    throw "$Name did not become ready before timeout."
}

function Wait-DockerHealth {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string] $Name,

        [Parameter(Mandatory)]
        [ValidateNotNullOrEmpty()]
        [string] $ContainerName,

        [Parameter(Mandatory)]
        [datetime] $Deadline
    )

    do {
        $status = & docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' $ContainerName 2>$null

        if ($LASTEXITCODE -eq 0 -and $status -eq 'healthy') {
            return [pscustomobject]@{
                Service    = $Name
                Status     = 'ready'
                Container  = $ContainerName
                Connection = "localhost:$($env:ORACLE_HOST_PORT ?? '11521')/FREEPDB1"
                User       = $env:ORACLE_APP_USER ?? 'SYMPHONY'
            }
        }

        Start-Sleep -Seconds 10
    } while ((Get-Date) -lt $Deadline)

    Invoke-DockerCompose -Argument @('logs', '--tail', '120', 'oracle')
    throw "$Name did not become healthy before timeout."
}

$services = Resolve-LabService -Name $Service
$deadline = (Get-Date).AddSeconds($TimeoutSeconds)

Invoke-DockerCompose -Argument (@('up', '-d') + $services)

foreach ($serviceName in $services) {
    switch ($serviceName) {
        'es8' {
            Wait-HttpEndpoint -Name 'elasticsearch-8' -Uri 'http://localhost:9208' -Deadline $deadline
        }
        'es9' {
            Wait-HttpEndpoint -Name 'elasticsearch-9' -Uri 'http://localhost:9209' -Deadline $deadline
        }
        'oracle' {
            Wait-DockerHealth -Name 'oracle-free' -ContainerName 'symphony-oracle' -Deadline $deadline
        }
    }
}
