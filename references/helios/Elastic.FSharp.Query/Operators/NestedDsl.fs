namespace Elastic.FSharp.Query.Operators

open Elastic.FSharp.Query.Types

type private NestedCarrier<'NS>(pred: Predicate<'NS>) =
    interface INestedLower with
        member _.Accept visitor = visitor.Visit<'NS> pred

[<AutoOpen>]
module NestedDsl =
    let nested (f: NestedField<'S, 'NS>) (build: NestedScope<'NS> -> Predicate<'NS>) : Predicate<'S> =
        let scope = NestedScope.create()
        let innerPred = build scope
        let carrier = NestedCarrier<'NS>(innerPred)
        Predicate.Nested (f.Path, carrier)
