namespace Elastic.FSharp.Query.Serialization

open System
open System.Text.Json.Nodes
open Elastic.FSharp.Query.Types

[<AutoOpen>]
module CodecHelpers =
    let inline jString (s: string) = JsonValue.Create(s) :> JsonNode
    let inline jInt (i: int64) = JsonValue.Create(i) :> JsonNode
    let inline jFloat (f: float) = JsonValue.Create(f) :> JsonNode
    let inline jBool (b: bool) = JsonValue.Create(b) :> JsonNode
    let inline jObj (props: (string * JsonNode) list) =
        let o = JsonObject()
        for (k, v) in props do o.Add(k, v)
        o :> JsonNode
    let inline jArr (items: JsonNode list) =
        let a = JsonArray()
        for i in items do a.Add(i)
        a :> JsonNode

module Codec =
    let rec encodeDateMath (dm: DateMath) : string =
        match dm with
        | DateMath.Now -> "now"
        | DateMath.Today -> "now/d"
        | DateMath.DaysAgo d -> sprintf "now-%dd" d
        | DateMath.HoursAgo h -> sprintf "now-%dh" h
        | DateMath.RoundedTo (inner, rounding) ->
            let rStr = match rounding with | DateRounding.Year -> "y" | DateRounding.Month -> "M" | DateRounding.Week -> "w" | DateRounding.Day -> "d" | DateRounding.Hour -> "h" | DateRounding.Minute -> "m" | DateRounding.Second -> "s"
            sprintf "%s/%s" (encodeDateMath inner) rStr
        | DateMath.Anchor (dt, delta) -> sprintf "%s||%s" (dt.ToString("O")) delta

    let encodeDateValue (dv: DateValue) : JsonNode =
        match dv with
        | DateValue.DAbsolute dt -> jString (dt.ToString("O"))
        | DateValue.DMath dm -> jString (encodeDateMath dm)
        | DateValue.DEpochMs epoch -> jInt epoch

    let rec encodeValue (v: FieldValue) : JsonNode =
        match v with
        | FieldValue.VString s -> jString s
        | FieldValue.VInt i -> jInt i
        | FieldValue.VFloat f -> jFloat f
        | FieldValue.VBool b -> jBool b
        | FieldValue.VDate d -> encodeDateValue d
        | FieldValue.VNull -> null

    let rec encode (qc: QueryContainer) : JsonNode =
        match qc with
        | QueryContainer.MatchAll _ ->
            jObj [ "match_all", jObj [] ]
        | QueryContainer.MatchNone _ ->
            jObj [ "match_none", jObj [] ]
        | QueryContainer.Term (field, value) ->
            jObj [ "term", jObj [ field, jObj [ "value", encodeValue value ] ] ]
        | QueryContainer.Terms (field, values) ->
            jObj [ "terms", jObj [ field, jArr (values |> List.map encodeValue) ] ]
        | QueryContainer.Exists field ->
            jObj [ "exists", jObj [ "field", jString field ] ]
        | QueryContainer.Prefix (field, value) ->
            jObj [ "prefix", jObj [ field, jObj [ "value", jString value ] ] ]
        | QueryContainer.Fuzzy (field, value, fuzziness) ->
            jObj [ "fuzzy", jObj [ field, jObj [ "value", jString value; "fuzziness", jString fuzziness ] ] ]
        | QueryContainer.MatchPhrase (field, query, slopOpt) ->
            let innerProps = [ "query", jString query ] @ (match slopOpt with | Some s -> ["slop", jInt (int64 s)] | None -> [])
            jObj [ "match_phrase", jObj [ field, jObj innerProps ] ]
        | QueryContainer.Match (field, query, analyzer, fuzziness, msm, zeroTerms) ->
            let innerProps =
                [ "query", jString query ]
                @ (match analyzer with | Some a -> ["analyzer", jString a] | None -> [])
                @ (match fuzziness with | Some f -> ["fuzziness", jString f] | None -> [])
                @ (match msm with | Some m -> ["minimum_should_match", jString m] | None -> [])
                @ (match zeroTerms with | Some z -> ["zero_terms_query", jString z] | None -> [])
            jObj [ "match", jObj [ field, jObj innerProps ] ]
        | QueryContainer.Range (field, gt, gte, lt, lte) ->
            let rangeProps =
                (match gt with | Some v -> ["gt", encodeValue v] | None -> []) @
                (match gte with | Some v -> ["gte", encodeValue v] | None -> []) @
                (match lt with | Some v -> ["lt", encodeValue v] | None -> []) @
                (match lte with | Some v -> ["lte", encodeValue v] | None -> [])
            jObj [ "range", jObj [ field, jObj rangeProps ] ]
        | QueryContainer.Bool (must, filter, should, mustNot, msmOpt) ->
            let boolProps =
                (if must.IsEmpty then [] else ["must", jArr (must |> List.map encode)]) @
                (if filter.IsEmpty then [] else ["filter", jArr (filter |> List.map encode)]) @
                (if should.IsEmpty then [] else ["should", jArr (should |> List.map encode)]) @
                (if mustNot.IsEmpty then [] else ["must_not", jArr (mustNot |> List.map encode)]) @
                (match msmOpt with | Some m -> ["minimum_should_match", jString m] | None -> [])
            jObj [ "bool", jObj boolProps ]
        | QueryContainer.Nested (path, inner, ignoreUnmapped) ->
            jObj [ "nested", jObj [ "path", jString path; "query", encode inner; "ignore_unmapped", jBool ignoreUnmapped ] ]

    let rec encodeAgg (ac: AggContainer) : JsonNode =
        match ac with
        | AggContainer.Terms (field, size, subAggs, orderBy) ->
            let termsProps = 
                [ "field", jString field; "size", jInt (int64 size) ]
                @ (match orderBy with | Some (orderField, dir) -> ["order", jObj [orderField, jString dir]] | None -> [])
            let aggObj = [ "terms", jObj termsProps ]
            let subObj = if subAggs.IsEmpty then [] else [ "aggs", encodeAggs subAggs ]
            jObj (aggObj @ subObj)
        | AggContainer.Sum field ->
            jObj [ "sum", jObj [ "field", jString field ] ]
        | AggContainer.Max field ->
            jObj [ "max", jObj [ "field", jString field ] ]
        | AggContainer.Cardinality field ->
            jObj [ "cardinality", jObj [ "field", jString field ] ]
        | AggContainer.TopHits (size, sortField, desc) ->
            let sortDir = if desc then "desc" else "asc"
            jObj [ "top_hits", jObj [ "size", jInt (int64 size); "sort", jArr [ jObj [ sortField, jObj [ "order", jString sortDir ] ] ] ] ]

    and encodeAggs (aggs: (string * AggContainer) list) : JsonNode =
        let props = aggs |> List.map (fun (name, agg) -> (name, encodeAgg agg))
        jObj props
