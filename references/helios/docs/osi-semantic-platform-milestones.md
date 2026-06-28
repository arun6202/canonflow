# OSI Semantic Platform Milestones

Generated: 2026-06-20

## Mission

Move the current Northwind/AdventureWorks query app from hardcoded domain metadata to an OSI-aligned semantic data platform.

The runtime query architecture should remain:

```text
Fable/Elmish UI -> ClientPredicate / ClientAggregation -> provider compiler -> backend engine
```

The meaning layer should move to versioned artifacts:

```text
semantic-models/ -> catalog loader -> DomainConfig -> UI + provider validation
```

## Guiding Rules

1. OSI is the semantic catalog, not the runtime query AST.
2. The existing Fable/Elmish DSL stays and becomes catalog-driven.
3. Business meaning belongs in artifacts, not scattered provider pattern matches.
4. Provider compilers remain responsible for physical execution.
5. Every new semantic rule should have at least one sample or quality check.

## Milestone 1: Semantic Artifact Baseline

Goal: establish the first OSI-aligned source of truth.

Deliverables:

- `semantic-models/northwind.osi.yaml`
- `semantic-models/adventureworks.osi.yaml`
- `quality-rules/semantic-catalog-quality-rules.md`

Definition of done:

- Both domains describe datasets, fields, relationships, and core metrics.
- Fields include labels, descriptions, AI synonyms, and provider extension hints.
- The files are readable enough for human review before code generation begins.

Status: completed.

## Milestone 2: F# Catalog Domain

Goal: parse and type-check semantic model artifacts inside the F# backend.

Deliverables:

- `SharedDomain` or new `PlatformCatalog` types for semantic models.
- A loader that parses OSI YAML into typed F# records.
- Validation that accumulates all catalog errors.
- Unit tests for valid and invalid semantic models.

Definition of done:

- Invalid models fail with precise errors.
- Logical field names are constrained before reaching provider compilers.
- No provider hardcoded field list is needed for basic schema output.

Status: mostly completed. Typed YAML parsing, typed catalog records, accumulated load errors, relationship/metric parsing, and validation endpoint are implemented. Unit tests are still pending.

## Milestone 3: DomainConfig Generation

Goal: make the existing UI consume catalog-derived metadata without changing its query AST.

Deliverables:

- `DomainConfig` generated from loaded semantic models.
- Field capabilities derived from catalog extensions.
- API schema endpoint backed by catalog rather than hardcoded records.

Definition of done:

- The Fable domain picker still works.
- Visual Builder field lists are catalog-driven.
- Existing Northwind and AdventureWorks search behavior remains intact.

Status: completed for current UI needs. Schema endpoint is catalog-backed with hardcoded fallback retained for resilience.

## Milestone 4: SQLite Analytics Compiler

Goal: make AdventureWorks analytics real.

Deliverables:

- Compile `ClientAggregation.Terms` into `GROUP BY`.
- Compile `ClientAggregation.Sum` into `SUM(...)`.
- Reuse the existing SQLite predicate compiler for filtered analytics.
- Add support for catalog metric expressions such as `total_revenue`.

Definition of done:

- AdventureWorks Analytics tab returns real buckets.
- Terms-by-country and sum-line-sales scenarios are covered by tests.
- SQL remains parameterized.

Status: completed for current `ClientAggregation.Terms` and `ClientAggregation.Sum` support over `AdventureWorksFlat`. Catalog metric expression compilation is still pending.

## Milestone 5: Quality Evidence

Goal: add the first data quality runway from the skills pack.

Deliverables:

- Source count checks for Northwind and AdventureWorks.
- Metric reconciliation queries.
- Golden sample rows and expected output DTOs.
- A simple run ledger for catalog and ingest checks.

Definition of done:

- A reviewer can see row counts, metric totals, and known samples without rerunning manual SQL.
- Regressions in semantic mapping become visible.

Status: started. Reconciliation SQL, golden samples, and a baseline run ledger exist. Automated quality execution is pending.

## Milestone 6: Relationship-Aware Planning

Goal: move beyond flattened AdventureWorks views.

Deliverables:

- Relationship graph built from OSI relationships.
- Join path planner for a small allowed set of star-schema joins.
- Join cardinality report for AdventureWorks fact-to-dimension joins.

Definition of done:

- AdventureWorks can query at least one field from `DimCustomer` and one metric from `FactInternetSales` without relying on `AdventureWorksFlat`.
- Required versus optional relationships are explicit.

Status: started. Relationships are parsed from the semantic model and a planning note exists. Query planning from relationships is pending.

## Milestone 7: Elasticsearch Rebuild Discipline

Goal: make Northwind Elasticsearch a derived serving index with evidence.

Deliverables:

- Explicit mapping/settings artifact.
- Deterministic document ID policy.
- Bulk load report.
- Alias/cutover plan for versioned index builds.

Definition of done:

- A Northwind index rebuild can be reconciled from SQLite source to Elasticsearch count and sample fetches.

Status: started. Seed mapping and rebuild runbook exist. Versioned index creation, bulk report generation, and alias cutover implementation are pending.

## First Vertical Slice

Recommended next coding slice:

```text
semantic-models/northwind.osi.yaml
    -> typed F# catalog loader
    -> generated DomainConfig
    -> existing Fable UI unchanged
```

This proves OSI feeds the current system without forcing the Fable/Elmish DSL to go away.
