$files = @(
    "e:\github\symphony\Symphony\SqlHydra.Source\src\SqlHydra.Cli\SqlServer\SqlServerSchemaProvider.fs",
    "e:\github\symphony\Symphony\SqlHydra.Source\src\SqlHydra.Cli\Oracle\OracleSchemaProvider.fs",
    "e:\github\symphony\Symphony\SqlHydra.Source\src\SqlHydra.Cli\Npgsql\NpgsqlSchemaProvider.fs",
    "e:\github\symphony\Symphony\SqlHydra.Source\src\SqlHydra.Cli\MySql\MySqlSchemaProvider.fs"
)
foreach ($f in $files) {
    $lines = Get-Content $f
    $newLines = @()
    foreach ($line in $lines) {
        $newLines += $line
        if ($line -match "^(\s*)ColumnSchema\.DefaultValue = None") {
            $ws = $matches[1]
            $newLines += ($ws + "ColumnSchema.Constraint = None")
        }
        if ($line -match "^(\s*)Column\.IsPK = (.*?)$") {
            $ws = $matches[1]
            $newLines += ($ws + "Column.Constraint = None")
        }
    }
    Set-Content -Path $f -Value ($newLines -join "`r`n") -Encoding UTF8
}
