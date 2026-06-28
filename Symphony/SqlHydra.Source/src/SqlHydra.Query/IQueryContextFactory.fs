namespace SqlHydra.Query

open System.Threading.Tasks

/// Factory for creating QueryContext instances.
/// A QueryContextFactory implementation is generated for each supported database provider.
type IQueryContextFactory =
    abstract member OpenContextAsync: unit -> Task<QueryContext>
