module Elastic.FSharp.Query.Tests.SchemaBridgeTests

open Xunit
open Elastic.FSharp.Query
open Elastic.FSharp.Query.Types
open Elastic.FSharp.Query.Operators
open Elastic.FSharp.Query.Lowering
open NorthwindSchema

[<Fact>]
let ``Generated Schema works seamlessly with DSL`` () =
    let q = Query.where' (
        CustomerId =. "C-100" &&.
        nested Orders (fun _ ->
            OrderId =. "O-555" &&.
            (Amount >. 100.0)
        ) &&.
        (matchWithWK Name "John" { MatchParams.empty with Analyzer = Some (AnalyzerRef Analyzer.StandardEnglish.Value) })
    )

    let lowered = Query.lowerQuery q
    printfn "LOWERED: %A" lowered

    // Assert overall bool logic
    match lowered with
    | QueryContainer.Bool (must, _, _, _, _) ->
        Assert.Equal(3, must.Length)
        
        match must.[0] with
        | QueryContainer.Term (path, FieldValue.VString v) ->
            Assert.Equal("customer_id", path)
            Assert.Equal("C-100", v)
        | _ -> Assert.Fail("Expected Term")

        match must.[1] with
        | QueryContainer.Nested (path, inner, true) ->
            Assert.Equal("orders", path)
        | _ -> Assert.Fail("Expected Nested")

        match must.[2] with
        | QueryContainer.Match (path, qStr, Some analyzer, None, None, None) ->
            Assert.Equal("name", path)
            Assert.Equal("John", qStr)
            Assert.Equal("standard_english", analyzer)
        | _ -> Assert.Fail("Expected Match with Analyzer")

    | _ -> Assert.Fail("Expected Bool Must")
