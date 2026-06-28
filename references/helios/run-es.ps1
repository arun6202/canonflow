$ErrorActionPreference = "Stop"
$dataDir = "E:\github\Adventureworks\data"

# Find Elasticsearch directory (e.g. elasticsearch-8.19.16 or 8.14.0)
$esDirs = @(Get-ChildItem -Path $dataDir -Directory -Filter "elasticsearch-8.*")
if ($esDirs.Count -eq 0) {
    Write-Error "Elasticsearch directory not found in $dataDir"
}
$esDir = $esDirs[0].FullName

Write-Host "Starting Elasticsearch from $esDir..."
$esBat = Join-Path $esDir "bin\elasticsearch.bat"

# Start in background
Start-Process -FilePath $esBat -WindowStyle Minimized

Write-Host "Waiting for Elasticsearch to respond on http://localhost:9200..."
$timeout = 120 # seconds
$sw = [Diagnostics.Stopwatch]::StartNew()
$isUp = $false

while ($sw.Elapsed.TotalSeconds -lt $timeout) {
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:9200" -Method Get -ErrorAction Stop
        if ($response.version.number -like "8.*") {
            $isUp = $true
            break
        }
    } catch {
        # Ignore and wait
    }
    Start-Sleep -Seconds 3
}

if ($isUp) {
    Write-Host "Elasticsearch is UP and healthy."
} else {
    Write-Error "Elasticsearch failed to start within $timeout seconds."
}
