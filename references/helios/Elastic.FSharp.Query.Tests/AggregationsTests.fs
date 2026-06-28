module Elastic.FSharp.Query.Tests.AggregationsTests

open Xunit
open Elastic.FSharp.Query
open Elastic.FSharp.Query.Types
open Elastic.FSharp.Query.Operators
open Elastic.FSharp.Query.Serialization

type TestIdx = class end

let fKeyword : KeywordField<TestIdx> = { Path = "myKw"; Kind = Unchecked.defaultof<_> }
let fNumeric : Field<TestIdx, Numeric, obj> = { Path = "myNum"; Kind = Unchecked.defaultof<_> }

[<Fact>]
let ``Can lower and serialize Terms aggregation with sub sum aggregation`` () =
    let q = 
        Lowering.Query.where' (Predicate.RawEs (QueryContainer.MatchAll ()))
        |> Aggregations.withAggs [
            "my_terms", Aggregations.termsWithSub (FieldRef.KW fKeyword) 10 [
                "my_sum", Aggregations.sum (FieldRef.NUM fNumeric)
            ]
        ]
        
    let lowerAggs = q.Aggs |> List.map (fun (n, a) -> (n, Lowering.Query.lowerAgg a))
    let json = Codec.encodeAggs lowerAggs |> fun n -> n.ToJsonString()
    
    Assert.Contains("\"terms\":{\"field\":\"myKw\",\"size\":10}", json)
    Assert.Contains("\"aggs\":{\"my_sum\":{\"sum\":{\"field\":\"myNum\"}}}", json)

[<Fact>]
let ``Can lower and serialize Max aggregation`` () =
    let a = Aggregations.max (FieldRef.NUM fNumeric)
    let lower = Lowering.Query.lowerAgg a
    let json = Codec.encodeAgg lower |> fun n -> n.ToJsonString()
    
    Assert.Equal("{\"max\":{\"field\":\"myNum\"}}", json)
