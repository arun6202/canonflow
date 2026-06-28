# AdventureWorks Relationship Planning

Generated: 2026-06-20

## Current State

The SQLite provider currently serves AdventureWorks from `AdventureWorksFlat`.
That view joins:

```text
FactInternetSales -> DimCustomer
FactInternetSales -> DimProduct
```

and exposes a small denormalized shape:

```text
Id
OrderId
OrderDate
CustomerId
Country
EmployeeLastName
ProductCategory
LineSales
```

This is enough for search and first-pass analytics, but it is not yet a generic relational planner.

## Relationship Contract Seed

The OSI model now records:

```text
internet_sales_to_customers:
  internet_sales.customer_key -> customers.customer_key

internet_sales_to_products:
  internet_sales.product_key -> products.product_key
```

## Planner Constraints

The first planner should stay deliberately small:

1. Only allow joins declared in the semantic model.
2. Only plan from a known fact/root dataset.
3. Require one unambiguous path from root dataset to requested field dataset.
4. Reject many-path or cyclic plans until a tie-breaker policy exists.
5. Report join quality before trusting output counts.

## Required Join Report

For each relationship used in a planned query:

- left dataset
- right dataset
- join keys
- left row count
- right row count
- output row count
- unmatched left rows
- unmatched right rows
- duplicate right keys
- max child rows per root

## Next Slice

Build a relationship graph from the parsed catalog and expose a debug endpoint:

```text
GET /api/catalog/AdventureWorks/relationships
```

Then use that graph to generate the current `AdventureWorksFlat` SQL from the semantic model instead of relying on a handwritten view.
