# Symphony Local Docker Lab

Status: initial local lab

This lab gives Symphony repeatable local services for Elasticsearch and Oracle work.

## Services

| Service | Container | Port | Purpose |
|---|---|---:|---|
| `es8` | `symphony-es8` | 9208 | Validate Elasticsearch 8 artifacts |
| `es9` | `symphony-es9` | 9209 | Validate Elasticsearch 9 artifacts |
| `oracle` | `symphony-oracle` | 11521 | Prepare the Oracle path |

## Start Oracle

```powershell
pwsh -NoProfile -File .\scripts\Start-SymphonyLab.ps1 -Service oracle
```

Defaults:

```text
Host:     localhost
Port:     11521
Service:  FREEPDB1
User:     SYMPHONY
Password: Symphony_12345
Sys pass: Symphony_12345
```

Override credentials with environment variables before starting:

```powershell
$env:ORACLE_PASSWORD = 'YourSysPassword_123'
$env:ORACLE_APP_USER = 'SYMPHONY'
$env:ORACLE_APP_PASSWORD = 'YourAppPassword_123'
$env:ORACLE_HOST_PORT = '11521'
pwsh -NoProfile -File .\scripts\Start-SymphonyLab.ps1 -Service oracle
```

## Start Elasticsearch 8 and 9

```powershell
pwsh -NoProfile -Command "& .\scripts\Start-SymphonyLab.ps1 -Service @('es8','es9')"
```

Validate the current generated artifacts:

```powershell
pwsh -NoProfile -File .\Symphony\validate-elasticsearch.ps1 -ElasticsearchUrl http://localhost:9208
pwsh -NoProfile -File .\Symphony\validate-elasticsearch.ps1 -ElasticsearchUrl http://localhost:9209
```

## Stop

Stop containers but keep named volumes:

```powershell
pwsh -NoProfile -File .\scripts\Stop-SymphonyLab.ps1
```

Remove containers and named volumes:

```powershell
pwsh -NoProfile -File .\scripts\Stop-SymphonyLab.ps1 -RemoveVolumes
```

## Oracle Notes

The Oracle service uses `gvenzl/oracle-free` for local developer ergonomics. It creates an
application user via environment variables and runs SQL files from:

```text
docker/oracle/init/
```

The first init script creates a tiny `symphony.oracle_lab_smoke` table with a primary key,
not-null column, default timestamp, and check constraint. This gives the future Oracle
harvester a known object to discover before we import Northwind.

## Next Oracle Step

After the container is stable:

1. Add an Oracle connection smoke command from .NET.
2. Harvest `ALL_CONSTRAINTS` and `ALL_CONS_COLUMNS` for `SYMPHONY.ORACLE_LAB_SMOKE`.
3. Import Northwind-style tables into Oracle.
4. Compare SQLite harvested constraints vs Oracle harvested constraints.
