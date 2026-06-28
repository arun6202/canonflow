# Symphony and OpenMetadata Standards Alignment

Status: decisions partially implemented
Scope: architecture alignment plus first draft JSON bundle

## 1. Intent

Symphony should not be understood as only an Oracle to Elasticsearch ETL tool. That is
one important movement, but not the whole composition.

Symphony is the system that turns source truth, typed transformations, runtime
validation, search projections, lineage, quality, and catalog metadata into one
coherent governed artifact.

The OpenMetadata Standards project gives us a useful external vocabulary for that
larger shape. It defines schemas across data assets, governance, data quality, lineage,
data contracts, teams and users, operations, events, and semantic representations such
as JSON Schema, RDF/OWL, JSON-LD, SHACL, and PROV-O.

Reference:

- https://openmetadatastandards.org/
- https://openmetadatastandards.org/metadata-specifications/overview/
- https://openmetadatastandards.org/data-assets/search/search-index/
- https://openmetadatastandards.org/lineage/lineage/

## 2. Core Position

Symphony's typed F# spec remains the source of truth.

OpenMetadata Standards should be treated as an export target and interoperability
surface, not as the internal runtime model.

This preserves the current architecture:

```text
Symphony Spec
  -> compile Elasticsearch mapping
  -> compile bulk/load artifacts
  -> compile lineage
  -> compile quality checks
  -> compile contracts
  -> emit OKF docs
  -> emit OpenMetadata-compatible JSON
  -> later emit RDF/JSON-LD/SHACL
```

The principle is:

> One score, many performances.

The score is the typed spec. Elasticsearch, OKF, OpenMetadata, CI reports, and semantic
graphs are different performances of the same score.

## 3. Why The Name Symphony Fits

OpenMetadata Standards covers many different metadata areas. Symphony needs many
components because the problem itself has many instruments:

- Source schemas and constraints
- F# generated types and refinements
- Transform expressions
- Elasticsearch mappings and aliases
- Bulk load artifacts
- Runtime validation
- Field and table lineage
- Quality tests and profiles
- Contracts and service expectations
- Governance tags and glossary terms
- Owners, teams, and personas
- Operational events
- Semantic graph export

Calling the project Symphony is useful because it reminds us that no single component
is the product. The value comes from orchestration without drift.

## 4. Symphony Components

| Symphony component | Responsibility | Current / future |
|---|---|---|
| `Bridge.Spec` | Typed source of truth: fields, expressions, lineage, lossiness, constraints | Current |
| `Bridge.Folds` | Pure folds over the spec: ES mapping, lineage, OKF, future OMS emit | Current / expand |
| `Bridge.Cli` | Artifact generation and local extraction runner | Current |
| `Harvest` | Read schema, PK, FK, unique, check, nullability, precision, comments | Future |
| `Refine` | Turn constraints into `Refined<'T,'P>` witnesses and parsers | Current / expand |
| `Project` | Convert source rows into target documents at the chosen grain | Current / expand |
| `Elastic` | ES 8/9 index mapping, aliases, bulk chunks, validation, smoke queries | Current / expand |
| `Lineage` | Field/table dependency graph from spec expressions | Current / expand |
| `Quality` | Generated tests from constraints, profiles, reconciliation checks | Future |
| `Contracts` | Schema, quality, freshness, and serving guarantees | Future |
| `Catalog` | OKF and OpenMetadata-compatible metadata bundles | Current OKF / future OMS |
| `Governance` | PII, glossary, tags, policy hints, owners | Future |
| `Events` | Change events, build events, validation events, load events | Future |
| `Graph` | RDF/OWL, JSON-LD, SHACL, PROV-O export | Future |
| `CI Gates` | Fail/report on coverage, quality, contract, schema drift | Future |

## 5. OpenMetadata Standards Mapping

This mapping is the bridge from Symphony concepts to OpenMetadata-compatible artifacts.

| Symphony concept | OpenMetadata Standards entity | Notes |
|---|---|---|
| Source database system | `DatabaseService` | SQLite now, Oracle later |
| Source database | `Database` | Northwind, Oracle schema-owned databases |
| Source schema | `DatabaseSchema` | SQLite `main`, Oracle schema |
| Source table | `Table` | Includes columns, table constraints, owner, tags |
| Source column | `Column` | Includes type, nullability, constraint-derived metadata |
| Elasticsearch service | `SearchService` | ES 8/9 cluster/service |
| Elasticsearch index alias | `SearchIndex` | Prefer alias as public serving identity |
| Concrete ES index version | custom property on `SearchIndex` | Example: `idx_v001`, hidden behind alias |
| Projection spec | `DataContract` or custom property | Spec hash/version, declared grain, schema expectations |
| ETL projection run | `Pipeline` | Batch generation/load pipeline |
| Pipeline step | `Task` | Harvest, project, map, bulk, validate |
| Bulk chunk | custom operational artifact | Keep out of catalog unless needed for audit |
| Field lineage | `Lineage` | Column to field edges |
| Constraint/refinement | `TestCase` | NOT NULL, range, IN, length, uniqueness, FK reconciliation |
| Test group | `TestSuite` | Per table/index/spec version |
| Runtime profile | `DataProfile` | Counts, null rates, min/max, distinct counts |
| Business term | `GlossaryTerm` | Example: Customer, Order, Revenue |
| Classification | `Classification` / `Tag` | Example: PII, GDPR, Confidential |
| Owner | `Team` / `User` | Human review and accountability |
| Validation/load event | `ChangeEvent` or custom event | For build and operational audit |

## 6. MVP Export Scope

The first OpenMetadata-aligned export should be deliberately small:

1. `DatabaseService`
2. `Database`
3. `DatabaseSchema`
4. `Table`
5. `Column`
6. `SearchService`
7. `SearchIndex`
8. `Pipeline`
9. `Task`
10. `Lineage`
11. `TestSuite`
12. `TestCase`
13. `DataContract`

This gives Symphony enough external shape to say:

- Here is the source table.
- Here are the source columns and constraints.
- Here is the Elasticsearch serving index.
- Here is the pipeline that produced it.
- Here are the source-to-target lineage edges.
- Here are the quality/contract checks we expect.
- Here is whether validation passed.

Do not start with dashboards, ML models, notebooks, personas, full policy enforcement,
or RDF graph export.

## 7. Artifact Shape

Recommended initial output directory:

```text
Symphony/output/openmetadata/
  database-service.json
  database.json
  schema.json
  tables/
    northwind-orders.json
    northwind-order-details.json
  search/
    northwind-order-lines-alias.json
  pipelines/
    northwind-order-lines-projection.json
  lineage/
    northwind-order-lines-lineage.json
  quality/
    northwind-order-lines-suite.json
  contracts/
    northwind-order-lines-contract.json
```

Each artifact should include:

- `name`
- `fullyQualifiedName`
- `description`
- `version`
- `source`
- `owners` when known
- `tags` when known
- `customProperties` for Symphony-specific details
- `specHash` or equivalent immutable build identity

## 8. Symphony Custom Properties

OpenMetadata Standards supports extension/custom-property style modeling. Symphony
should use this instead of forcing every internal concept into a standard field.

Suggested custom properties:

| Property | Applies to | Meaning |
|---|---|---|
| `symphony.specHash` | all emitted entities | Hash of the typed spec used to generate artifact |
| `symphony.specVersion` | all emitted entities | Human-visible spec version |
| `symphony.sourceDialect` | database/table/pipeline | SQLite, Oracle, DuckDB |
| `symphony.targetDialect` | search index/pipeline | Elasticsearch 8/9 |
| `symphony.documentGrain` | search index/data contract | Example: `order-line` |
| `symphony.indexAlias` | search index | Public write/read identity |
| `symphony.concreteIndex` | search index | Physical ES index version, if known |
| `symphony.lineageCoverage` | table/search index/pipeline | Exact/Declared/Opaque coverage |
| `symphony.constraintCoverage` | table/search index/pipeline | Prevented/Inherited/Detected coverage |
| `symphony.lossinessCoverage` | table/search index/pipeline | Lossless/lossy mapping stats |
| `symphony.bulkChunks` | pipeline run event | Number and size of generated bulk chunks |

## 9. Lineage Strategy

Lineage should be constructed, not inferred.

For every target field in the Symphony spec:

- `Exact` lineage becomes trusted field-level lineage.
- `Declared` lineage becomes field-level lineage with an assertion marker.
- `Opaque` lineage becomes a visible coverage gap.

This aligns with the OpenMetadata lineage area while keeping Symphony honest about
what it can prove.

## 10. Data Quality Strategy

Quality should start from constraints already in the spec:

- NOT NULL -> completeness test
- PK -> uniqueness/entity identity test
- CHECK range -> value range test
- CHECK IN -> accepted values test
- length -> max length test
- FK -> reconciliation test
- secondary UNIQUE -> reconciliation test unless stream topology can prevent it

These become `TestCase` records grouped into `TestSuite` records.

For the current Northwind Elasticsearch slice, useful tests are:

- `id` is unique
- `orderId` is present
- `product.productId` is present
- `unitPrice >= 0`
- `quantity > 0`
- `discount between 0 and 1`
- `lineSales >= 0`
- strict mapping rejects unknown fields
- Elasticsearch document count equals source projection count

## 11. Data Contract Strategy

The first data contract should describe the serving projection, not the source table.

For `northwind_order_lines_alias`, contract fields:

- Document grain: one document per order line
- Required fields: `id`, `orderId`, `customer.customerId`, `product.productId`,
  `unitPrice`, `quantity`, `discount`, `lineSales`
- Mapping strictness: dynamic strict
- Compatibility: Elasticsearch 8.x and 9.x target
- Freshness: not applicable for current local batch MVP
- Quality expectations: linked to generated `TestSuite`
- Lineage expectations: no `Opaque` fields allowed in MVP

## 12. Phase Plan

### Phase A: Alignment Document

This document. No code.

Exit:

- Agree on component vocabulary.
- Agree that OpenMetadata is an export target, not the internal model.

### Phase B: Northwind OMS Draft Bundle

Emit static/generated JSON for the current Northwind order-line slice:

- Database service
- Database/schema
- Source tables/columns
- Search index
- Pipeline/tasks
- Lineage
- Test suite/test cases
- Data contract

Exit:

- JSON files exist.
- They reference each other consistently by fully qualified name.
- They include Symphony custom properties.

Current status:

- Implemented as neutral draft JSON files under `Symphony/output/openmetadata/`.
- Generated by `CompileOpenMetadata.emitBundle`.
- Uses the Elasticsearch alias as the public `SearchIndex`.
- Emits desired quality tests only, not run results.
- Fails generation if any serving field has `Opaque` lineage.

### Phase C: Validate Against OMS Schemas

Use OpenMetadata Standards JSON schemas to validate emitted artifacts.

Exit:

- Schema validation passes or every deviation is documented as a custom property.

### Phase D: Catalog Sink

Optionally push artifacts into OpenMetadata or another catalog.

Exit:

- External catalog can show source table, ES index, lineage, quality tests, and contract.

### Phase E: Semantic Export

Emit JSON-LD/RDF/SHACL/PROV-O from the same spec.

Exit:

- Knowledge graph can answer source-to-target and contract/quality questions.

## 13. Non-Goals For Now

- Do not replace the typed F# spec with OpenMetadata schemas.
- Do not build a full OpenMetadata server integration first.
- Do not model dashboards, ML models, notebooks, or personas in the MVP.
- Do not infer lineage from arbitrary SQL.
- Do not implement policy enforcement before catalog/contract output exists.
- Do not make RDF/OWL the first export format.

## 14. Decisions

1. Public Elasticsearch entity:
   Use the alias as `SearchIndex`. Concrete index names stay custom properties or
   validation/runtime metadata.

2. Quality checks:
   Emit desired tests first. Add run results only after the validator produces structured
   result metadata.

3. OpenMetadata output form:
   Emit neutral JSON bundle files first. Add an API sink later.

4. Shared catalog model:
   Defer a shared intermediate catalog model until the first bundle shape proves itself.

5. Coverage gate:
   MVP generation fails on any `Opaque` target field in the serving index.

## 15. Next Approved Slice

Implemented:

```text
Symphony typed spec
  -> OpenMetadata draft bundle JSON
```

Initial target:

```text
Northwind SQLite tables
Northwind order-line Elasticsearch alias
Generated lineage
Generated data quality tests
Generated data contract
```

No catalog push, no RDF, no Oracle, no CDC.

Next after this:

- Validate the draft JSON bundle against OpenMetadata Standards schemas.
- Add structured validator run results.
- Keep catalog push, RDF, Oracle, and CDC out until schema validation is clean.
