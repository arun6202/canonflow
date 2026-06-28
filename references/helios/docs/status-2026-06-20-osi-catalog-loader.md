# OSI Catalog Loader Status

Date: 2026-06-20

## Implemented

- Added `PlatformApi/Catalog.fs`.
- Added `YamlDotNet` to `PlatformApi`.
- Added `Catalog.loadDomainConfigOrFallback`.
- Wired `PlatformApi/Program.fs` schema registry to prefer catalog-derived `DomainConfig`.
- Kept the previous hardcoded configs as fallback so local development does not break if semantic model files are missing.
- Added `client_field` to UI/provider custom extensions in:
  - `semantic-models/northwind.osi.yaml`
  - `semantic-models/adventureworks.osi.yaml`

## Current Loader Scope

This is now a typed YAML-backed compatibility loader. It parses the OSI YAML with `YamlDotNet`, maps it into F# catalog records, then reads the JSON data embedded in OSI `custom_extensions` and extracts:

- `client_field`
- UI capabilities:
  - `supports_terms`
  - `supports_prefix`
  - `supports_range`
  - `supports_match`

It then generates the existing `SharedDomain.Dtos.DomainConfig` shape used by the Fable/Elmish UI.

## Why This Shape

This proves the first real architecture handoff:

```text
OSI YAML artifact -> catalog loader -> DomainConfig -> existing Fable UI
```

The loader still treats provider-specific execution hints as `NORTHWIND_PLATFORM` custom extension JSON. A later milestone should promote provider mappings and metric expressions into richer typed catalog records.

## Verification

Command run:

```powershell
dotnet build PlatformApi\PlatformApi.fsproj
```

Result:

- Build succeeded.
- 0 errors.
- 2 warnings.

Warnings observed:

- `SQLitePCLRaw.lib.e_sqlite3` has a known high severity vulnerability warning.

No compile errors came from the new catalog loader.

## Next Step

Implement provider validation and analytics support from the catalog:

```text
semantic-models/*.osi.yaml
    -> generated DomainConfig
    -> provider field validation
    -> SQLite analytics compiler
```

After that, move metric expressions and provider mappings out of ad hoc JSON and into richer typed catalog records.
