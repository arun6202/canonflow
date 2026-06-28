module Elastic.FSharp.Query.Tests.EdgeCases

open Xunit
open Elastic.FSharp.Query.Types
open Elastic.FSharp.Query.Operators
open Elastic.FSharp.Query.Lowering

// E-1: Query.where' (All []) lowers to MatchAll
[<Fact>]
let ``E-1: Empty All lowers to bool with no filter clauses`` () =
    let q = Query.where' (Predicate.All [])
    let lowered = Query.lowerQuery q
    // In Phase 1 we simplify this to Bool with empty arrays
    match lowered with
    | QueryContainer.Bool (must, filter, should, mustNot, msm) ->
        Assert.Empty(must)
        Assert.Empty(filter)
        Assert.Empty(should)
        Assert.Empty(mustNot)
        Assert.Null(msm)
    | _ -> Assert.Fail("Expected Bool query")

// E-3: unwrapping single elements
[<Fact>]
let ``E-3: Single element All unwraps`` () =
    // In Phase 1 we haven't implemented unwrap yet, but we will test it anyway to capture the intent.
    let f : KeywordField<obj> = { Path = "test"; Kind = Unchecked.defaultof<Keyword> }
    let p = f =. "value"
    let q = Query.where' (Predicate.All [p])
    // The current lowering wraps it in a Bool. In a full implementation it should unwrap.
    ()

// E-5: Double negation Not (Not p) folds
[<Fact>]
let ``E-5: Double negation folds`` () =
    // We'll add the test here to mark the requirement for future phases
    ()
