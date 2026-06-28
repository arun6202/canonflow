# Northwind Elasticsearch Rebuild Runbook

Generated: 2026-06-20

## Purpose

Treat Elasticsearch as a derived serving index, not the source of truth.
The source of truth is `northwind.db` plus versioned semantic, mapping, and quality artifacts.

## Target Flow

```text
northwind.db
    -> DataSync domain validation
    -> deterministic OrderLineDocumentDto stream
    -> versioned Elasticsearch index
    -> count/sample validation
    -> alias cutover
```

## Versioned Index Policy

Use versioned physical indexes and stable aliases:

```text
northwind-orders-v001
northwind-orders-v002

northwind-orders-read -> northwind-orders-vNNN
```

The current seed mapping lives at:

```text
mapping/northwind-orders-v001.json
```

## Deterministic ID Policy

Use the existing line-level document identity:

```text
{OrderId}_{ProductId}
```

If multiple order lines can share the same product within one order, extend the identity with row ordinal or source detail key before production use.

## Rebuild Steps

1. Create the next versioned index with explicit settings and mappings.
2. Run `DataSync` against `northwind.db`.
3. Bulk load documents into the versioned index.
4. Verify document count against source line count.
5. Fetch golden sample IDs and compare important fields.
6. Run search smoke tests for country, category, employee last name, and line sales range.
7. Move the read alias atomically.
8. Keep the previous index until signoff.

## Required Load Report

Record:

- run id
- semantic model version
- mapping file hash
- source database path
- source line count
- attempted document count
- successful document count
- failed document count
- rejected documents by reason
- final Elasticsearch `_count`
- alias cutover status

## Known Gap

The current `DataSync` implementation deletes and recreates the `orders` index directly. That should be replaced with versioned index creation and alias cutover before production-style use.
