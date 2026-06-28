# Symphony

Symphony is a typed metadata and projection system for building trustworthy search
surfaces from relational truth.

The current MVP starts with Northwind SQLite and Elasticsearch, but the intended
production direction is Oracle to Elasticsearch:

```text
Source database truth
  -> typed F# spec
  -> constraints and refinements
  -> Elasticsearch mapping and bulk/load artifacts
  -> lineage, quality, contracts, catalog metadata
  -> OpenMetadata/OKF/semantic exports
```

The guiding idea is:

> One score, many performances.

The typed F# spec is the score. Elasticsearch mappings, bulk files, OKF docs,
OpenMetadata-compatible metadata, lineage graphs, validation checks, and future RDF/JSON-LD
exports are all performances of that same score.

## Why Symphony Exists

Elasticsearch is an excellent read surface, but it does not naturally enforce the same
rules as a relational source system. Oracle or SQLite can have primary keys, nullability,
foreign keys, check constraints, precision rules, and transactional semantics. Elasticsearch
mostly gives us fast indexed documents.

Symphony exists to close that gap:

- Carry source constraints into F# types and runtime checks.
- Generate strict Elasticsearch mappings instead of hand-writing drift-prone JSON.
- Produce deterministic bulk artifacts with stable document identity.
- Track field-level lineage from source columns to target fields.
- Turn constraints into quality checks and future data contracts.
- Emit catalog metadata so the projection is governable, not just operational.

## Current MVP

The current working slice is:

```text
Northwind SQLite
  -> DuckDB staging
  -> order-line Elasticsearch documents
  -> strict ES 8/9-compatible mapping
  -> chunked bulk NDJSON
  -> OKF/catalog lineage
  -> OpenMetadata draft bundle
  -> local ES 8 and ES 9 validation
  -> local Oracle Docker smoke target
```

Generated artifacts live under:

```text
Symphony/output/
  mapping.json
  catalog.md
  bulk-manifest.json
  bulk/
    order-lines-0001.ndjson
    ...
  openmetadata/
    database-service.json
    search/
    lineage/
    quality/
    contracts/
```

The generated order-line projection currently emits 609,283 documents split into
50 MiB bulk chunks. The smaller chunk budget is intentional: the first ES 8 live test
rejected larger requests because they crossed the coordinating-operation byte limit.

## What Works Today

- `Bridge.Spec` contains the core typed model: columns, expressions, lineage grades,
  Elasticsearch field types, field specs, table specs, and `Refined<'T,'P>`.
- `SqlHydra.Source` is embedded and modified so SQLite schema information can generate
  stronger F# types.
- SQLite primary keys and simple `CHECK` constraints can become F# refined types such as
  `Refined<decimal, GreaterThanOrEqualToZero>`.
- `Bridge.Folds` emits strict Elasticsearch mappings and OKF-style field lineage from the spec.
- `Bridge.Cli` extracts Northwind order-line data through DuckDB and generates Elasticsearch
  artifacts.
- `validate-elasticsearch.ps1` validates generated mapping and bulk chunks against an
  Elasticsearch endpoint.
- `CompileOpenMetadata` emits a neutral OpenMetadata-style draft bundle under
  `Symphony/output/openmetadata/`.
- Local Docker services cover Elasticsearch 8.19.17, Elasticsearch 9.0.0, and Oracle Free.
- Oracle Free is reachable on `localhost:11521/FREEPDB1` with a `SYMPHONY` app user and
  a smoke table for the future Oracle harvester path.

Validation results from the current ES tests:

```text
ES 8.19.17: 609283 documents, 6 chunks, strict reject True
ES 9.0.0:   609283 documents, 6 chunks, strict reject True
```

## Architecture

### Core

The pure core should own descriptions, not effects:

- Source/target fields
- Transform expressions
- Lineage grades: `Exact`, `Declared`, `Opaque`
- Constraint grades: future `Prevented`, `Inherited`, `Detected`
- Lossiness grades
- Mapping specs
- Quality and contract definitions

### Folds

Every output should be a fold over the same spec:

- `compileEs` -> Elasticsearch mapping
- `lineageOf` -> field dependency graph
- `emitOkf` -> human-readable catalog docs
- future `emitOpenMetadata` -> OpenMetadata-compatible JSON bundle
- future `emitQuality` -> generated test suites/test cases
- future `emitContract` -> serving data contract
- future `emitSemanticGraph` -> JSON-LD/RDF/SHACL/PROV-O

### Shell

The shell owns I/O:

- DuckDB/SQLite extraction
- Future Oracle extraction
- Elasticsearch validation/load
- File emission
- Docker/local validation scripts
- Future catalog API push

## Project Layout

```text
Symphony/
  Bridge.Spec/          typed core model and generated Northwind records
  Bridge.Folds/         folds from spec to mapping/catalog/lineage artifacts
  Bridge.Cli/           local artifact generator and DuckDB extraction runner
  SqlHydra.DuckDb/      DuckDB schema helper spike
  SqlHydra.Source/      embedded modified SqlHydra source
  output/               generated ES/OKF artifacts

docs/                   static documentation site notes/assets
references/
  oracle-es-bridge-comprehensive-plan.md
  symphony-openmetadata-alignment.md
  local-docker-lab.md
  helios/               reference implementation and ES/DSL material
  ai-skills/            F#/ETL/reference skills and notes

docker/
  oracle/init/          Oracle Free initialization smoke SQL

scripts/
  Start-SymphonyLab.ps1 local Docker lab starter
  Stop-SymphonyLab.ps1  local Docker lab stopper
```

## OpenMetadata Alignment

OpenMetadata Standards gives Symphony an external vocabulary for catalog, quality,
lineage, contracts, governance, teams, events, and semantic export.

Symphony should not replace its typed F# spec with OpenMetadata schemas. Instead:

```text
Symphony typed spec
  -> OpenMetadata-compatible bundle
```

Initial OpenMetadata-aligned entities:

- `DatabaseService`
- `Database`
- `DatabaseSchema`
- `Table`
- `Column`
- `SearchService`
- `SearchIndex`
- `Pipeline`
- `Task`
- `Lineage`
- `TestSuite`
- `TestCase`
- `DataContract`

See:

[references/symphony-openmetadata-alignment.md](references/symphony-openmetadata-alignment.md)

## Elasticsearch

Current target:

- Elasticsearch 8.x: tested with Docker image `docker.elastic.co/elasticsearch/elasticsearch:8.19.17`
- Elasticsearch 9.x: tested with Docker image `docker.elastic.co/elasticsearch/elasticsearch:9.0.0`

The generated mapping uses:

- `dynamic: "strict"`
- explicit field mappings
- nested object structures for customer, employee, and product
- text fields with `.keyword` subfields
- alias-oriented bulk actions

Run the local Docker lab:

```powershell
pwsh -NoProfile -Command "& .\scripts\Start-SymphonyLab.ps1 -Service @('es8','es9','oracle')"
```

Validate generated artifacts:

```powershell
pwsh -NoProfile -File .\Symphony\validate-elasticsearch.ps1 -ElasticsearchUrl http://localhost:9208
pwsh -NoProfile -File .\Symphony\validate-elasticsearch.ps1 -ElasticsearchUrl http://localhost:9209
```

## Oracle

Current Oracle target:

- Docker image: `gvenzl/oracle-free:23-slim-faststart`
- Container: `symphony-oracle`
- Connection: `localhost:11521/FREEPDB1`
- App user: `SYMPHONY`
- Smoke table: `SYMPHONY.ORACLE_LAB_SMOKE`

The Oracle lab is intentionally small for now. Its purpose is to prove the container,
credentials, PDB context, and schema discovery target before importing Northwind-style
tables and harvesting `ALL_CONSTRAINTS` / `ALL_CONS_COLUMNS`.

## Oracle To Elasticsearch Roadmap

The long-term path is not just Oracle data movement. The valuable path is Oracle truth
becoming a typed, explainable, testable, documented Elasticsearch serving surface:

```text
Oracle schema and data
  -> harvested source metadata
  -> Symphony typed spec
  -> deterministic ETL extraction
  -> generated documentation/catalog
  -> generated Elasticsearch mapping and bulk files
  -> validation, quality gates, alias swap, and run audit
```

### Missing Next Capabilities

- Oracle source harvester: read `ALL_TAB_COLUMNS`, `ALL_CONSTRAINTS`,
  `ALL_CONS_COLUMNS`, indexes, table comments, column comments, nullability, precision,
  scale, and FK relationships.
- Oracle type fidelity map: define how `NUMBER`, `DATE`, `TIMESTAMP WITH TIME ZONE`,
  `CLOB`, `BLOB`, `CHAR`, `VARCHAR2`, and `RAW` become F# types and Elasticsearch fields.
- Snapshot identity: capture Oracle SCN, extract timestamp, source schema version, spec
  hash, and bulk manifest hash for every generated run.
- Incremental extraction plan: prove batch first, then add SCN or `last_modified` deltas
  before considering GoldenGate/Kafka CDC.
- Oracle-to-spec bridge: move from hand-shaped specs toward harvested Oracle metadata plus
  explicit business projection rules.
- Data quality gates: check required fields, primary-key uniqueness, FK presence, numeric
  ranges, source row counts, and source-vs-index document counts.
- Elasticsearch alias lifecycle: create versioned index, load, validate, swap alias, and
  keep rollback instructions.
- Load audit trail: persist rows read, docs emitted, bytes written, chunk count, ES
  version, validation result, source SCN, and spec hash.
- Contract diffing: classify schema/spec changes as additive, breaking, lossy, or
  operationally neutral.
- OpenMetadata validation and push path: validate generated artifacts against schemas first,
  then add an API sink.
- Operator runbook: document extract, validate, load, rollback, and failed-field
  investigation steps.

### High-Value Additions

- Add a `runId` across bulk manifests, generated docs, OpenMetadata artifacts, ES `_meta`,
  and validation output.
- Carry field confidence through the system: `Exact`, `Declared`, `Opaque`, plus explicit
  lossiness markers where Elasticsearch cannot preserve Oracle semantics exactly.
- Include Oracle table and column comments in generated catalog/docs.
- Add PII, classification, owner, and steward tags before real enterprise data is indexed.
- Generate human-readable field explanations: source fields, transform, constraints, and
  reason the target field exists.
- Generate ES smoke queries from the spec: term query, range query, aggregation, count, and
  bad-field strict-mapping rejection.
- Make replay safe: deterministic document IDs, deterministic chunking, checkpointable
  manifests, and resumable loads.
- Store validation output as structured JSON so docs and OpenMetadata can reference actual
  run evidence.
- Add spec diff reports so reviewers can see field additions, removals, type changes,
  constraint changes, and lineage confidence changes.
- Keep the typed spec as the center. ETL, docs, ES mappings, quality checks, and catalog
  exports should all be folds over the same source of truth.

## Build And Generate

Build:

```powershell
dotnet build .\Symphony\Symphony.slnx --no-restore
```

Generate Elasticsearch and catalog artifacts:

```powershell
dotnet run --project .\Symphony\Bridge.Cli\Bridge.Cli.fsproj
```

Regenerate SqlHydra schema models:

```powershell
Push-Location .\Symphony\Bridge.Spec
dotnet run --project ..\SqlHydra.Source\src\SqlHydra.Cli\SqlHydra.Cli.fsproj --framework net8.0 -- sqlite
Pop-Location
```

## Known Warnings

- Builds currently report a high-severity advisory for `SQLitePCLRaw.lib.e_sqlite3`
  2.1.11 through the dependency graph. This needs dependency remediation.
- `SqlHydra.Source` is embedded and patched. That gives deep control, but it also means
  upstream SqlHydra fixes must be merged manually.
- The current SQLite `CHECK` handling is still too string-matching-heavy. It needs a closed
  constraint model.

## TODO

### Immediate

- Recreate ES 8 under `docker-compose.local.yml` so all lab services are Compose-owned.
- Move ES version/container details into a small documented test matrix.
- Fix or suppress the `SQLitePCLRaw.lib_e_sqlite3` dependency advisory properly.
- Add generated artifact sanity tests: mapping parses, bulk chunks end in newline, chunk
  sizes stay under budget, and action/source pairs are balanced.
- Add a first-class Oracle smoke command from .NET instead of relying only on SQL*Plus.

### Spec And Lineage

- Remove remaining stringly areas from `TableSpec`, `FieldSpec`, and `RefineTag`.
- Make computed fields like `lineSales` first-class expressions where possible, not only
  declared raw expressions.
- Add coverage reporting: exact/declared/opaque lineage percentages.
- Fail the MVP build if any serving-index field has `Opaque` lineage.
- Track lossiness explicitly for numeric precision, timestamps, and binary/blob mappings.

### Constraint Harvesting

- Replace SQLite `CHECK` substring matching with a closed `CheckPred` model.
- Support string constraints: max length, non-empty, prefix, pattern/regex where safe.
- Support date/time boundary checks.
- Represent multi-column constraints as declared/detected instead of pretending they are
  single-column refinements.
- Add Oracle constraint harvesting for `ALL_CONSTRAINTS` and `ALL_CONS_COLUMNS`.

### Elasticsearch

- Add alias lifecycle helpers: create concrete index, attach write/read alias, validate,
  swap, rollback.
- Add generated smoke queries: term, match, range, aggregation.
- Capture validator output as structured run metadata.
- Add index version/spec hash into mapping `_meta`.
- Decide whether production writes should always use `require_alias=true`.

### Quality And Contracts

- Generate `TestSuite` and `TestCase` artifacts from constraints.
- Add quality checks for current Northwind order-line projection:
  `id` uniqueness, required fields, non-negative prices, positive quantities, discount
  range, source-vs-index document count.
- Generate a `DataContract` for `northwind_order_lines_alias`.
- Add freshness and SLA fields once Oracle/CDC is introduced.

### OpenMetadata And Catalog

- Validate the emitted OpenMetadata draft bundle under `Symphony/output/openmetadata/`
  against OpenMetadata Standards JSON schemas.
- Add structured validator run results after the ES validator emits stable JSON.
- Keep OpenMetadata as an export target first; add an API sink later.
- Preserve OKF/Markdown output for human-readable local review.
- Consider a shared intermediate catalog model only after schema validation is clean.

### Oracle Path

- Import the same Northwind-style shape into Oracle.
- Harvest the current `SYMPHONY.ORACLE_LAB_SMOKE` constraints from Oracle data dictionary views.
- Replace SQLite/DuckDB snapshot assumptions with Oracle flashback/SCN concepts.
- Add Oracle type fidelity rules: NUMBER precision, DATE/TIMESTAMP/TZ, CLOB/BLOB.
- Add SCN-stamped `BulkOp` versioning and tombstones.
- Plan CDC with GoldenGate/Kafka only after the batch path and contract path are proven.

### Scale

- Avoid full in-memory materialization for billion-row paths.
- Keep bulk generation streaming and byte-budgeted.
- Use DuckDB only as staging/diff where appropriate, not as an accidental bottleneck.
- Add checkpoint/resume manifests.
- Add metrics for rows read, docs emitted, bytes written, chunks loaded, failures, retries.

### Governance

- Add owner/team metadata.
- Add PII/classification tags.
- Decide coverage gates: fail vs report for declared, opaque, detected, and lossy fields.
- Add spec diff classification: additive vs breaking.
- Document review rules for schema/spec changes.

## Design References

- [Oracle to Elasticsearch comprehensive plan](references/oracle-es-bridge-comprehensive-plan.md)
- [Symphony and OpenMetadata Standards alignment](references/symphony-openmetadata-alignment.md)
- [Helios reference material](references/helios)

## License

This project is licensed under the Apache License 2.0. See [LICENSE](LICENSE).
