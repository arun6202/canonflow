# Symphony Project Status & Roadmap

## 🎯 What We Did
- **Defined Core Abstractions:** Created the `Refined<'T, 'P>` generic wrapper and `IPredicate<'T>` interface in `Bridge.Spec` to introduce Oracle-level strictness into F#.
- **Embedded SqlHydra:** Cloned the `SqlHydra` codebase into `Symphony/SqlHydra.Source`, bypassing generic Nuget restrictions and granting us deep architectural access.
- **Augmented Constraint Parsing:** Enhanced the SQLite provider to actively interrogate `sqlite_master` for SQL-level `CHECK` constraints (e.g. `UnitPrice >= 0`).
- **AST Generation Hooks:** Mapped those SQLite string constraints directly into the Fantomas code generation pipeline, successfully generating strictly typed constraints in `Northwind.fs` (e.g., `Refined<decimal, GreaterThanOrEqualToZero>`).
- **Established Documentation:** Built a rich HTML/CSS Material 3 site showcasing the architecture and deployed it seamlessly to GitHub pages via a custom GitHub Action workflow.
- **Repository Cleanup:** Cleaned up the repository by stripping out large `bin`/`obj` folders, established a solid `.gitignore`, and successfully pushed to main.

## 🚀 What is Pending / What's Next
- **Dynamic String Constraints:** Add custom constraint mappers for things like `StartsWithP` (`CHECK (CustomerID LIKE 'P%')`) and other business-logic specific string validations.
- **DuckDB Staging:** We scaffolded `SqlHydra.DuckDb` during the MVP phase, but it isn't wired into the live CLI bridging pipeline yet. We need to finalize how data will flow from SQLite -> DuckDB.
- **Elasticsearch Pipeline:** The final goal of "Symphony" is pushing these rigid types into Elasticsearch. We still need to write and test the translation layer mapping F# `Refined` structs to Elasticsearch JSON indices.
- **Expanded Refined Types:** Currently, we only mapped basic numeric constraints (`GreaterThanZero`, `BetweenZeroAndOne`). We need to map `string` length limits, regex, and date boundaries.

## ⚠️ Blindspots & Risks (What You Might Not Be Aware Of)
- **Upstream Disconnection:** By absorbing `SqlHydra` directly into the monorepo and patching its internals, we can no longer simply run `dotnet add package SqlHydra.Cli`. Any future bug fixes or updates released by the original author will have to be manually ported over.
- **Data Precision Leaks (Lossiness):** SQLite is dynamically typed at the column level (e.g., `NUMERIC` affinity). When bridging data into DuckDB and then into Elasticsearch (which enforces strict typings), we may face silent truncations if precision thresholds mismatch between the three engines.
- **F# Compilation Order Sensitivity:** F# project files (`.fsproj`) require files to be explicitly ordered. Since `Northwind.fs` relies on `Spec.fs`, as we add more databases and domains, managing the `.fsproj` compilation hierarchy will require careful attention.
- **Multi-Column Constraints:** Our current AST parser assumes single-column constraints (e.g. `Quantity > 0`). Complex table-wide rules like `CHECK (EndDate > StartDate)` are completely unhandled right now.
