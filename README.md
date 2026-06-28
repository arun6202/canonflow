# Symphony

Symphony is an enterprise-grade data bridge designed to provide "Oracle-level strictness" to data migrations and synchronizations between heterogeneous databases (e.g., SQLite, DuckDB) and Elasticsearch. 

## Features

- **Strict Schema Enforcement:** Powered by F# `Refined` types and predicates to ensure data strictly adheres to database constraints at the application layer.
- **Embedded SqlHydra Generator:** Heavily enhanced, embedded integration of `SqlHydra` to parse raw `sqlite_master` definitions (like `CHECK` constraints) and emit strongly-typed F# record models.
- **Data Bridging:** Safely bridges validated data into Elasticsearch with deep semantic verification.

## Architecture

Symphony leverages F#'s type system to create lossless and lossy specifications using `Symphony.Bridge.Spec`. The core data ingestion pipelines translate SQL constraints (e.g., `UnitPrice >= 0`) directly into F# types like `Refined<decimal, GreaterThanOrEqualToZero>`. 

## Development

The project consists of the core bridge specification and an embedded, modified version of the `SqlHydra` CLI generator.

### Building

To build the Symphony project:

```bash
dotnet build Symphony/Bridge.Spec
```

To regenerate the schema models based on `SqlHydra.Source`:

```bash
cd Symphony/Bridge.Spec
dotnet run --project ../SqlHydra.Source/src/SqlHydra.Cli/SqlHydra.Cli.fsproj --framework net8.0 -- sqlite
```

## License

This project is licensed under the Apache License 2.0 - see the [LICENSE](LICENSE) file for details.
