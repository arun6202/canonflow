# Symphony Birds-Eye View

Status: working architecture snapshot
Date: 2026-06-29

## 1. What We Accomplished

Symphony has moved from a promising strict-typing idea into a real vertical slice.

The current system can:

- Read Northwind SQLite data through DuckDB.
- Generate order-line Elasticsearch documents.
- Emit a strict Elasticsearch mapping compatible with the ES 8/9 direction.
- Write deterministic bulk NDJSON in safe 50 MiB chunks.
- Validate the generated artifacts against a live Elasticsearch 8.19.17 Docker node.
- Generate OKF-style lineage documentation.
- Generate a first neutral OpenMetadata-style draft bundle.
- Block OpenMetadata bundle generation if any serving field has `Opaque` lineage.

The most important architectural step is this:

> Symphony is no longer just "data movement." It is becoming a typed projection,
> validation, lineage, quality, contract, and catalog system.

## 2. Current Working Slice

```text
Northwind SQLite
  -> DuckDB extraction
  -> typed Symphony spec
  -> strict Elasticsearch mapping
  -> order-line documents
  -> chunked bulk NDJSON
  -> Elasticsearch validation
  -> OKF lineage
  -> OpenMetadata-style draft bundle
```

Current generated output:

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
    database.json
    schema.json
    tables/
    search/
    pipelines/
    lineage/
    quality/
    contracts/
```

Current validation facts:

- Elasticsearch 8.19.17 Docker node tested.
- 609,283 order-line documents generated.
- 6 bulk chunks generated.
- Strict mapping rejects unknown fields.
- OpenMetadata draft bundle emits 14 artifacts.
- Lineage coverage: 18 fields total, 16 exact, 2 declared, 0 opaque.

## 3. What The Repo Looks Like Now

The repo has three major layers.

### Product Layer

- `README.md` now explains the project vision, current MVP, build steps, Elasticsearch flow,
  OpenMetadata alignment, known warnings, and TODO list.
- `birds-eye.md` gives this executive/architecture snapshot.
- `docs/` contains earlier project notes and web-doc assets.

### Implementation Layer

- `Symphony/Bridge.Spec`
  - Core typed model.
  - `Refined<'T,'P>` and `IPredicate<'T>`.
  - Expression/lineage model.
  - Generated Northwind records.

- `Symphony/Bridge.Folds`
  - Elasticsearch mapping generation.
  - OKF/catalog markdown generation.
  - OpenMetadata-style draft JSON bundle generation.

- `Symphony/Bridge.Cli`
  - Runs the current Northwind extraction.
  - Emits mapping, catalog, bulk chunks, manifest, and OpenMetadata bundle.

- `Symphony/validate-elasticsearch.ps1`
  - Creates a temporary validation index/alias.
  - Bulk-loads generated chunks.
  - Checks document count and strict mapping behavior.
  - Cleans up after validation.

### Reference Layer

- `references/oracle-es-bridge-comprehensive-plan.md`
  - The larger Oracle to Elasticsearch architecture.

- `references/symphony-openmetadata-alignment.md`
  - Alignment between Symphony and OpenMetadata Standards.
  - Decisions are now partially implemented.

- `references/helios`
  - Reference implementation and Elasticsearch/F# DSL inspiration.

- `references/ai-skills`
  - Skills/reference material for F#, ETL, Elasticsearch, Oracle, and related work.

## 4. Current Architectural Identity

Symphony's emerging identity:

```text
Typed source truth
  + generated constraints
  + projection spec
  + search mapping
  + lineage
  + quality
  + contract
  + catalog export
```

The most useful phrase is:

> One score, many performances.

The typed F# spec is the score. Elasticsearch, OKF, OpenMetadata, quality checks,
contracts, and future semantic graph exports are performances of that score.

## 5. What Is Strong Already

- The project has a real executable slice, not just diagrams.
- Elasticsearch validation found and fixed a real operational issue: chunk size.
- The architecture cleanly separates core spec/folds from shell/runtime effects.
- F# refined types give a credible path from database constraints to application-level
  proofs.
- The OpenMetadata direction is correctly positioned as an export target, not the internal
  source of truth.
- The current bundle already has lineage, quality, contract, pipeline, table, and search
  index artifacts.

## 6. What Is Still Weak

- Constraint parsing is still too string-matching-heavy.
- `FieldSpec`, `TableSpec`, and `RefineTag` still contain stringly areas.
- Computed expressions such as `lineSales` are declared dependencies, not fully modeled
  expression trees yet.
- OpenMetadata output is draft-shaped, not validated against official schemas yet.
- Runtime extraction still materializes full documents in memory.
- The SqlHydra source is embedded and modified, which creates long-term upstream maintenance
  cost.
- Build currently reports a known SQLite native package advisory.

## 7. SWOT

### Strengths

- **Type-driven architecture:** F# gives Symphony a strong base for making invalid states
  hard to represent.
- **Single-spec direction:** Mapping, lineage, quality, contracts, and catalog output can all
  fold from one artifact.
- **Real validation loop:** ES 8 Docker validation already exercises mapping, bulk load,
  document count, and strict mapping rejection.
- **Governance-aware early:** OpenMetadata/OKF/lineage/quality/contracts are entering before
  the system hardens into a narrow ETL tool.
- **Good reference material:** Helios, ai-skills, and the Oracle-ES plan give strong nearby
  design gravity.

### Weaknesses

- **Parser fragility:** SQLite `CHECK` constraints are not yet parsed into a robust closed AST.
- **Memory profile:** The current CLI is fine for the MVP but not yet billion-row safe.
- **Embedded dependency risk:** Forking/embedding SqlHydra gives control but increases update
  burden.
- **Schema validation gap:** OpenMetadata artifacts are useful, but not yet proven against OMS
  schemas.
- **Source/target type fidelity:** NUMBER/decimal precision, timestamps, time zones, blobs, and
  Oracle empty-string-as-null still need explicit treatment.

### Opportunities

- **OpenMetadata-compatible export:** Symphony can become a practical bridge between typed ETL
  specs and enterprise metadata catalogs.
- **Oracle production path:** Once SQLite is stable, Oracle SCN, flashback, constraints, and CDC
  make the architecture far more valuable.
- **Quality and contract generation:** Constraints can become executable tests and serving data
  contracts automatically.
- **Semantic graph:** JSON-LD/RDF/SHACL/PROV-O export could turn Symphony into a real metadata
  graph generator.
- **Elasticsearch 8/9 compatibility harness:** A reusable validation matrix can become a strong
  selling point for safe search-index evolution.

### Threats

- **Scope creep:** OpenMetadata, Oracle, CDC, RDF, quality, contracts, and ES ops can easily
  explode if not phased tightly.
- **False confidence:** Draft metadata artifacts can look official before schema validation is
  real.
- **Operational scale:** Billion-row loads will expose memory, retry, checkpoint, and index
  lifecycle problems if postponed too long.
- **Elasticsearch drift:** ES 8/9 compatibility needs continuous testing because mappings and
  bulk behavior can change.
- **Maintenance drag:** Carrying a patched SqlHydra source tree could slow progress if upstream
  divergence grows.

## 8. Best Next Moves

Recommended order:

1. Validate the OpenMetadata draft bundle against official OpenMetadata Standards schemas.
2. Add Elasticsearch 9.x Docker validation.
3. Add generated artifact tests for mapping JSON, bulk chunk shape, and lineage coverage.
4. Replace SQLite `CHECK` substring matching with a closed `CheckPred` model.
5. Add structured validator run results and emit them into OpenMetadata-style operation/event
   artifacts.
6. Move bulk/document generation toward streaming so the path can eventually scale.
7. Start Oracle harvesting only after the SQLite spec/fold/export loop is boring and repeatable.

## 9. Guiding Principle

Do not let Symphony become a pile of integrations.

Keep the center small:

```text
Typed spec -> pure folds -> thin shells
```

Everything else should orbit that center.

