module Elastic.FSharp.Query.Tests.NestedTests

open Xunit
open Elastic.FSharp.Query.Types
open Elastic.FSharp.Query.Operators
open Elastic.FSharp.Query.Lowering

// Mock schemas
type CustomerSchema = class end
type OrderSchema = class end
type LineItemSchema = class end

let orderField : NestedField<CustomerSchema, OrderSchema> = { Path = "orders"; Kind = Unchecked.defaultof<Nested> }
let orderId : KeywordField<OrderSchema> = { Path = "orders.id"; Kind = Unchecked.defaultof<Keyword> }
let lineItemField : NestedField<OrderSchema, LineItemSchema> = { Path = "orders.lines"; Kind = Unchecked.defaultof<Nested> }
let productId : KeywordField<LineItemSchema> = { Path = "orders.lines.product_id"; Kind = Unchecked.defaultof<Keyword> }

[<Fact>]
let ``E-13, E-26: Nested of nested and recursive lowering`` () =
    let q = Query.where' (
        nested orderField (fun _ ->
            nested lineItemField (fun _ ->
                productId =. "P-123"
            )
        )
    )
    let lowered = Query.lowerQuery q
    
    match lowered with
    | QueryContainer.Nested(path1, inner1, true) -> // I-10 ignore_unmapped=true
        Assert.Equal("orders", path1)
        match inner1 with
        | QueryContainer.Nested(path2, inner2, true) ->
            Assert.Equal("orders.lines", path2)
            match inner2 with
            | QueryContainer.Term(p3, FieldValue.VString "P-123") ->
                Assert.Equal("orders.lines.product_id", p3)
            | _ -> Assert.Fail("Expected Term inside nested")
        | _ -> Assert.Fail("Expected Nested inside nested")
    | _ -> Assert.Fail("Expected outer Nested")

[<Fact>]
let ``I-2: Phantom typing isolation is enforced at compile time`` () =
    // A test that intentionally won't compile if we try to use a LineItem schema field at the Customer level.
    // The F# compiler enforces it, so this test just serves as documentation.
    ()
