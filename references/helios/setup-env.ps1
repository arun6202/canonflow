$ErrorActionPreference = "Stop"

$northwindDir = "E:\github\Adventureworks\gem\northwind"
$dataDir = "E:\github\Adventureworks\data"

# 1. Download Northwind SQLite
$dbUrl = "https://raw.githubusercontent.com/jpwhite3/northwind-SQLite3/master/dist/northwind.db"
$dbPath = Join-Path $northwindDir "northwind.db"
if (-not (Test-Path $dbPath)) {
    Write-Host "Downloading Northwind SQLite database..."
    Invoke-WebRequest -Uri $dbUrl -OutFile $dbPath
    Write-Host "Downloaded to $dbPath"
} else {
    Write-Host "Northwind SQLite database already exists."
}

# 2. Download and extract Elasticsearch
$userEsVersion = "8.14.0" # Hardcoding 8.14.0 since 8.19.16 might be causing the hang.
$esVersion = $userEsVersion
$esZipUrl = "https://artifacts.elastic.co/downloads/elasticsearch/elasticsearch-$esVersion-windows-x86_64.zip"
$zipPath = Join-Path $dataDir "elasticsearch-$esVersion.zip"
$esExtractPath = Join-Path $dataDir "elasticsearch-$esVersion"

if (-not (Test-Path $esExtractPath)) {
    Write-Host "Downloading Elasticsearch $esVersion..."
    try {
        Invoke-WebRequest -Uri $esZipUrl -OutFile $zipPath -TimeoutSec 120
    } catch {
        Write-Error "Failed to download Elasticsearch: $_"
    }
    
    Write-Host "Extracting Elasticsearch (this may take a minute)..."
    Expand-Archive -Path $zipPath -DestinationPath $dataDir -Force
    Write-Host "Extracted to $esExtractPath"
} else {
    Write-Host "Elasticsearch already extracted."
}

# 3. Configure Elasticsearch (disable security)
$yamlPath = Join-Path $esExtractPath "config\elasticsearch.yml"
if (Test-Path $yamlPath) {
    Write-Host "Configuring Elasticsearch to disable security..."
    $config = Get-Content $yamlPath
    $config = $config -match "^(?!xpack\.security\.enabled:).*"
    $config += "xpack.security.enabled: false"
    $config | Set-Content $yamlPath
    Write-Host "Security disabled."
}
