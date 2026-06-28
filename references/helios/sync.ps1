<#
.SYNOPSIS
    Synchronizes Northwind SQLite database to an Elasticsearch cluster.

.DESCRIPTION
    This script automates the synchronization process by ensuring Elasticsearch is running,
    building and running the F# synchronization application, and verifying the data
    insertion using the Elasticsearch REST API.
#>
[CmdletBinding()]
param(
    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $NorthwindDir = "E:\github\Adventureworks\gem\northwind",

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $DataDir = "E:\github\Adventureworks\data",

    [Parameter()]
    [ValidateNotNullOrEmpty()]
    [string] $ElasticUri = "http://localhost:9200"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

try {
    # 1. Ensure Elasticsearch is running
    Write-Verbose "Starting Elasticsearch via run-es.ps1"
    Write-Information "Ensuring Elasticsearch is running..." -InformationAction Continue
    $esRunScript = Join-Path $DataDir "run-es.ps1"
    & $esRunScript

    # 2. Run F# Sync App
    Write-Verbose "Building and executing the F# synchronization application."
    Write-Information "Running F# Sync App..." -InformationAction Continue
    $syncAppPath = Join-Path $NorthwindDir "ElasticSync"
    
    $originalLocation = Get-Location
    Set-Location -Path $syncAppPath
    
    try {
        & dotnet run
        if ($LASTEXITCODE -ne 0) {
            throw "F# Sync App failed with exit code $LASTEXITCODE."
        }
    } finally {
        Set-Location -Path $originalLocation
    }

    Write-Information "Sync process completed successfully." -InformationAction Continue

    # 3. Verify with Elasticsearch API
    Write-Verbose "Querying Elasticsearch to verify index payload."
    Write-Information "Verifying Elasticsearch contents..." -InformationAction Continue
    
    # Allow Elasticsearch a moment to fully flush its index
    Start-Sleep -Seconds 2

    $searchUri = "$ElasticUri/orders/_search?size=1"
    $response = Invoke-RestMethod -Uri $searchUri -Method Get

    $totalHits = $response.hits.total.value
    
    if ($totalHits -gt 0) {
        $firstOrder = $response.hits.hits[0]._source
        
        [pscustomobject]@{
            Status = "Success"
            TotalOrdersIndexed = $totalHits
            SampleOrderId = $firstOrder.id
            SampleTotalAmount = $firstOrder.totalAmount
            CustomerName = $firstOrder.customer.companyName
            LinesCount = @($firstOrder.lines).Count
        }
    } else {
        Write-Warning "Elasticsearch query succeeded but returned 0 orders."
    }

} catch {
    Write-Error "Synchronization pipeline failed: $_"
    throw
}
