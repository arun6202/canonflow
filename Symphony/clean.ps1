$files = @(
    "e:\github\symphony\Symphony\SqlHydra.Source\src\SqlHydra.Cli\SqlServer\SqlServerSchemaProvider.fs",
    "e:\github\symphony\Symphony\SqlHydra.Source\src\SqlHydra.Cli\Oracle\OracleSchemaProvider.fs",
    "e:\github\symphony\Symphony\SqlHydra.Source\src\SqlHydra.Cli\Npgsql\NpgsqlSchemaProvider.fs",
    "e:\github\symphony\Symphony\SqlHydra.Source\src\SqlHydra.Cli\MySql\MySqlSchemaProvider.fs"
)
foreach ($f in $files) {
    $content = Get-Content $f -Raw
    $content = $content -replace "`r`nConstraint = None", ""
    $content = $content -replace "`r`nColumn\.Constraint = None", ""
    Set-Content -Path $f -Value $content -NoNewline -Encoding UTF8
}
