#Requires -Version 7.0
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter()]
    [switch] $RemoveVolumes,

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $ComposeFile = (Join-Path $PSScriptRoot '..\docker-compose.local.yml')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$composePath = (Resolve-Path -LiteralPath $ComposeFile).ProviderPath
$arguments = @('compose', '-f', $composePath, 'down')

if ($RemoveVolumes) {
    $arguments += '--volumes'
}

if ($PSCmdlet.ShouldProcess('symphony-lab', 'Stop local Docker lab')) {
    & docker @arguments

    if ($LASTEXITCODE -ne 0) {
        throw "docker compose down failed with exit code $LASTEXITCODE."
    }
}
