# Milestones 2-7 Autopilot Status

Date: 2026-06-20

## Implemented in Code

### Milestone 2: F# Catalog Domain

- `PlatformApi/Catalog.fs` parses OSI YAML with `YamlDotNet`.
- Catalog records now include:
  - semantic document
  - semantic models
  - datasets
  - fields
  - provider extensions
  - relationships
  - metrics
- Catalog loading returns accumulated errors.
- Added `Catalog.validateDomain`.
- Added API endpoint:

```text
GET /api/catalog/{domain}/validate
```

### Milestone 3: DomainConfig Generation

- `/api/schema/{domain}` uses catalog-derived `DomainConfig`.
- Existing hardcoded configs remain as fallback.
- Fable/Elmish UI contract remains unchanged.

### Milestone 4: SQLite Analytics Compiler

- `SqliteQueryProvider.ExecuteAnalytics` is now implemented.
- Supports:
  - `ClientAggregation.Terms` as `GROUP BY`
  - `ClientAggregation.Sum` as `SUM(...)`
  - optional filter predicates reused from the existing parameterized predicate compiler
- SQLite provider now rejects unknown fields instead of passing raw field names into SQL.

## Added Governance Artifacts

### Milestone 5: Quality Evidence

- `quality-rules/reconciliation-queries.sql`
- `golden-samples/adventureworks-analytics-country.json`
- `golden-samples/northwind-country-revenue-top5.json`
- `run-ledger/2026-06-20-local-baseline.yaml`

### Milestone 6: Relationship-Aware Planning

- `docs/adventureworks-relationship-planning.md`
- Catalog parser now reads relationship names and endpoints.

### Milestone 7: Elasticsearch Rebuild Discipline

- `mapping/northwind-orders-v001.json`
- `docs/elasticsearch-rebuild-runbook.md`

## Verification

Command:

```powershell
dotnet build PlatformApi\PlatformApi.fsproj
```

Result:

- Build succeeded.
- 0 errors.
- 2 warnings.

Warnings:

- `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 has a known high severity vulnerability.

## Still Pending

- Unit tests for valid/invalid catalog parsing.
- Automated quality runner for reconciliation SQL and golden samples.
- Catalog-backed provider field validation for Elasticsearch.
- Catalog metric expression compilation.
- Relationship graph endpoint and relationship-aware SQL planner.
- Versioned Elasticsearch rebuild implementation with alias cutover.
- Dependency remediation for `SQLitePCLRaw.lib.e_sqlite3`.
