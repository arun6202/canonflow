# Semantic Catalog Quality Rules

Generated: 2026-06-20

These rules are the first governance runway for moving Northwind and AdventureWorks toward an OSI-aligned semantic platform.

## Catalog Rules

1. Every semantic model must have a stable `name`, `description`, and `ai_context`.
2. Every dataset must declare a `source`.
3. Datasets that represent persisted tables or views should declare `primary_key`.
4. Every field must declare a scalar expression for at least `ANSI_SQL`.
5. UI-visible fields must include a `NORTHWIND_PLATFORM` custom extension with UI capability flags.
6. Provider-specific paths and columns must live in custom extensions, not in OSI core fields.
7. Metric expressions must reference semantic field names, not raw provider paths.
8. Relationships must use matching ordered key arrays.
9. No new hardcoded domain field list should be added once a catalog-backed equivalent exists.

## Runtime Rules

1. The Fable/Elmish UI must not need to know whether a domain runs on Elasticsearch, SQLite, or a future engine.
2. `ClientPredicate` remains the runtime query intent shape.
3. The API must validate logical field names against the catalog before provider compilation.
4. Provider compilers may reject unsupported semantic operations with precise errors.
5. SQLite and Elasticsearch should expose the same business-level field names wherever possible.

## Quality Evidence Rules

1. Each domain should have source row counts recorded in a run ledger.
2. Each metric added to the catalog should have at least one reconciliation SQL query.
3. Each provider mapping should have at least one golden sample proving DTO output shape.
4. Relationship-aware joins must report unmatched parents, unmatched children, duplicate keys, and output counts.
5. Analytics output should be checked against direct SQL for at least one grouped metric per domain.

## Initial Reconciliation Targets

Northwind local database snapshot:

- Customers: 93
- Orders: 16,282
- Order details: 609,283
- Products: 77
- Categories: 8
- Employees: 9
- Total line sales: 448,386,633.17

AdventureWorks local database snapshot:

- AdventureWorksFlat rows: 60,398
- DimCustomer rows: 18,484
- DimProduct rows: 606
- FactInternetSales rows: 60,398
- Total line sales: 29,358,677.22

These are not permanent truth. They are the current local baseline and should move into a machine-readable run ledger in a later milestone.
