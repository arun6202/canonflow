$files = @(
    "e:\github\symphony\Symphony\SqlHydra.Source\src\SqlHydra.Cli\SqlServer\SqlServerSchemaProvider.fs",
    "e:\github\symphony\Symphony\SqlHydra.Source\src\SqlHydra.Cli\Oracle\OracleSchemaProvider.fs",
    "e:\github\symphony\Symphony\SqlHydra.Source\src\SqlHydra.Cli\Npgsql\NpgsqlSchemaProvider.fs",
    "e:\github\symphony\Symphony\SqlHydra.Source\src\SqlHydra.Cli\MySql\MySqlSchemaProvider.fs"
)
foreach ($f in $files) {
    $content = Get-Content $f -Raw
    $content = [regex]::Replace($content, '([ \t]*DefaultValue = None)', { param($m) $m.Value + "`r`n" + $m.Value.Replace('DefaultValue = None', 'Constraint = None') })
    $content = [regex]::Replace($content, '([ \t]*Column\.IsPK = col\.IsPrimaryKey)', { param($m) $m.Value + "`r`n" + $m.Value.Replace('Column.IsPK = col.IsPrimaryKey', 'Column.Constraint = None') })
    Set-Content -Path $f -Value $content -NoNewline -Encoding UTF8
}
