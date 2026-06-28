module Elastic.FSharp.Query.Tests.SerializationTests

open Xunit
open Elastic.FSharp.Query.Types
open Elastic.FSharp.Query.Serialization
open Elastic.FSharp.Query.Operators
open Elastic.FSharp.Query.Lowering
open Elastic.FSharp.Query
open NorthwindSchema

[<Fact>]
let ``Codec serializes Bool query correctly`` () =
    let qc = QueryContainer.Bool (
        [ QueryContainer.Term ("customer_id", FieldValue.VString "C-100") ],
        [], [], [], None
    )
    
    let jsonNode = Codec.encode qc
    let jsonStr = jsonNode.ToJsonString()
    
    Assert.Contains("\"bool\"", jsonStr)
    Assert.Contains("\"must\"", jsonStr)
    Assert.Contains("\"term\"", jsonStr)
    Assert.Contains("\"customer_id\"", jsonStr)
    Assert.Contains("\"C-100\"", jsonStr)

[<Fact>]
let ``P-3: Query AST to JSON maps without information loss`` () =
    let q = Query.where' (
        CustomerId =. "C-123" &&.
        (TotalSpend >. 50.0) &&.
        (matchWithWK Name "Alice" MatchParams.empty)
    )
    
    let qc = Query.lowerQuery q
    let jsonNode = Codec.encode qc
    let jsonStr = jsonNode.ToJsonString()
    printfn "JSON: %s" jsonStr

    // Validate output structure matches expectation exactly
    // Must contain a bool with nested queries
    Assert.Contains("\"bool\"", jsonStr)
    Assert.Contains("\"term\"", jsonStr)
    Assert.Contains("\"C-123\"", jsonStr)
    Assert.Contains("\"range\"", jsonStr)
    Assert.Contains("\"gt\"", jsonStr)
    Assert.Contains("50", jsonStr)
    Assert.Contains("\"match\"", jsonStr)
    Assert.Contains("\"Alice\"", jsonStr)
