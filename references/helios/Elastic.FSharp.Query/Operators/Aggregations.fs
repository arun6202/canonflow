namespace Elastic.FSharp.Query

open Elastic.FSharp.Query.Types

module Aggregations =

    /// <summary>
    /// Creates a terms aggregation that buckets by the given field, limited by `size`.
    /// </summary>
    let terms (fref: FieldRef<'S>) (size: int) : Agg<'S> =
        Agg.Terms (fref, size, [], None)

    /// <summary>
    /// Creates a terms aggregation with nested sub-aggregations.
    /// </summary>
    let termsWithSub (fref: FieldRef<'S>) (size: int) (subAggs: (string * Agg<'S>) list) : Agg<'S> =
        Agg.Terms (fref, size, subAggs, None)

    /// <summary>
    /// Creates an ordered terms aggregation.
    /// </summary>
    let termsOrdered (fref: FieldRef<'S>) (size: int) (orderField: string) (dir: string) : Agg<'S> =
        Agg.Terms (fref, size, [], Some (orderField, dir))

    let termsWithSubOrdered (fref: FieldRef<'S>) (size: int) (orderField: string) (dir: string) (subAggs: (string * Agg<'S>) list) : Agg<'S> =
        Agg.Terms (fref, size, subAggs, Some (orderField, dir))

    /// <summary>
    /// Computes the sum of a numeric field.
    /// </summary>
    let sum (fref: FieldRef<'S>) : Agg<'S> =
        Agg.Sum fref

    /// <summary>
    /// Computes the max value of a numeric or date field.
    /// </summary>
    let max (fref: FieldRef<'S>) : Agg<'S> =
        Agg.Max fref

    /// <summary>
    /// Computes the approximate distinct count of a field.
    /// </summary>
    let cardinality (fref: FieldRef<'S>) : Agg<'S> =
        Agg.Cardinality fref

    /// <summary>
    /// Returns the top matching documents per bucket.
    /// </summary>
    let topHits (size: int) (sortField: FieldRef<'S>) (desc: bool) : Agg<'S> =
        Agg.TopHits (size, sortField, desc)

    // Optional builder helper to compose agg lists
    let withAggs (aggs: (string * Agg<'S>) list) (q: Query<'S>) : Query<'S> =
        { q with Aggs = aggs }
