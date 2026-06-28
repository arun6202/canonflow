namespace Elastic.FSharp.Query.Operators

open Elastic.FSharp.Query.Types

[<AutoOpen>]
module Composition =
    /// <summary>
    /// Boolean AND (`must` clause in Elasticsearch). Flattens adjacent ANDs into a single array for efficiency.
    /// </summary>
    let (&&.) (p: Predicate<'S>) (q: Predicate<'S>) : Predicate<'S> =
        match p, q with
        | Predicate.All l1, Predicate.All l2 -> Predicate.All (l1 @ l2)
        | Predicate.All l1, _ -> Predicate.All (l1 @ [q])
        | _, Predicate.All l2 -> Predicate.All (p :: l2)
        | _ -> Predicate.All [p; q]

    /// <summary>
    /// Boolean OR (`should` clause in Elasticsearch). Defaults to minimum_should_match = 1.
    /// </summary>
    let (||.) (p: Predicate<'S>) (q: Predicate<'S>) : Predicate<'S> =
        // We only flatten if MinShouldMatch is default (Count 1)
        match p, q with
        | Predicate.Any (l1, MinShouldMatch.Count 1), Predicate.Any (l2, MinShouldMatch.Count 1) -> Predicate.Any (l1 @ l2, MinShouldMatch.Count 1)
        | Predicate.Any (l1, MinShouldMatch.Count 1), _ -> Predicate.Any (l1 @ [q], MinShouldMatch.Count 1)
        | _, Predicate.Any (l2, MinShouldMatch.Count 1) -> Predicate.Any (p :: l2, MinShouldMatch.Count 1)
        | _ -> Predicate.Any ([p; q], MinShouldMatch.Count 1)

    /// <summary>
    /// Boolean NOT (`must_not` clause in Elasticsearch).
    /// </summary>
    let not' (p: Predicate<'S>) : Predicate<'S> =
        Predicate.Not p

    /// <summary>
    /// Ensures a document has a non-null value for the given field (`exists` query).
    /// </summary>
    let exists (f: FieldRef<'S>) : Predicate<'S> =
        Predicate.Exists f
