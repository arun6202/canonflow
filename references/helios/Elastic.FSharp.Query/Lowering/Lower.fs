namespace Elastic.FSharp.Query.Lowering

open Elastic.FSharp.Query.Types

module Query =
    let rec lower<'T> (p: Predicate<'T>) : QueryContainer =
        match p with
        | Predicate.Eq (fref, v) ->
            let path = 
                match fref with
                | FieldRef.KW f -> f.Path
                | FieldRef.TX f -> f.Path + ".keyword" // I-1 says Eq on TX is unrepresentable, but if forced, use .keyword or fail. Actually spec says "Eq on TextField is unrepresentable" so it shouldn't even compile! But our AST has FieldRef so we handle it.
                | FieldRef.TWK f -> f.Path + ".keyword" // L-1
                | FieldRef.DT f -> f.Path
                | FieldRef.NUM f -> f.Path
                | FieldRef.BL f -> f.Path
            QueryContainer.Term (path, v)
        | Predicate.In (fref, vs) ->
            let path = 
                match fref with
                | FieldRef.KW f -> f.Path
                | FieldRef.TX f -> f.Path + ".keyword" 
                | FieldRef.TWK f -> f.Path + ".keyword" 
                | FieldRef.DT f -> f.Path
                | FieldRef.NUM f -> f.Path
                | FieldRef.BL f -> f.Path
            QueryContainer.Terms (path, vs)
        | Predicate.All preds ->
            let lowered = preds |> List.map lower
            QueryContainer.Bool (lowered, [], [], [], None)
        | Predicate.Any (preds, msm) ->
            let lowered = preds |> List.map lower
            let msmStr = 
                match msm with
                | MinShouldMatch.Count c -> string c
                | MinShouldMatch.Percent p -> sprintf "%d%%" p
                | MinShouldMatch.Expr e -> e
            QueryContainer.Bool ([], [], lowered, [], Some msmStr)
        | Predicate.Exists fref ->
            let path = 
                match fref with
                | FieldRef.KW f -> f.Path
                | FieldRef.TX f -> f.Path
                | FieldRef.TWK f -> f.Path
                | FieldRef.DT f -> f.Path
                | FieldRef.NUM f -> f.Path
                | FieldRef.BL f -> f.Path
            QueryContainer.Exists path
        | Predicate.Not pInner ->
            QueryContainer.Bool ([], [], [], [lower pInner], None)
        | Predicate.Range (fref, lo, hi) ->
            let path = 
                match fref with
                | FieldRef.KW f -> f.Path
                | FieldRef.TX f -> failwith "Range on text not allowed"
                | FieldRef.TWK f -> f.Path
                | FieldRef.DT f -> f.Path
                | FieldRef.NUM f -> f.Path
                | FieldRef.BL f -> f.Path
            let gt, gte = match lo with | Bound.Gt v -> Some v, None | Bound.Gte v -> None, Some v | Bound.Unbounded -> None, None | _ -> None, None
            let lt, lte = match hi with | Bound.Lt v -> Some v, None | Bound.Lte v -> None, Some v | Bound.Unbounded -> None, None | _ -> None, None
            QueryContainer.Range (path, gt, gte, lt, lte)
        | Predicate.Nested (path, innerL) ->
            let visitor =
                { new ILowerVisitor with
                    member _.Visit<'A> (p: Predicate<'A>) = lower p }
            let innerQc = innerL.Accept visitor
            QueryContainer.Nested (path, innerQc, true)
        | Predicate.Match (fref, query, p) ->
            let path = match fref with | FieldRef.TX f -> f.Path | FieldRef.TWK f -> f.Path | _ -> failwith "Invalid match field"
            let fuzzStr = match p.Fuzziness with | Some Fuzziness.Auto -> Some "AUTO" | Some (Fuzziness.Edits e) -> Some (string e) | None -> None
            let analyzerStr = match p.Analyzer with | Some (AnalyzerRef a) -> Some a | None -> None
            QueryContainer.Match (path, query, analyzerStr, fuzzStr, p.MinimumShouldMatch, p.ZeroTermsQuery)
        | Predicate.Phrase (fref, query) ->
            let path = match fref with | FieldRef.TX f -> f.Path | FieldRef.TWK f -> f.Path | _ -> failwith "Invalid match field"
            QueryContainer.MatchPhrase (path, query, None)
        | Predicate.Prefix (fref, query) ->
            let path = match fref with | FieldRef.TX f -> f.Path | FieldRef.TWK f -> f.Path | FieldRef.KW f -> f.Path | _ -> failwith "Invalid match field"
            QueryContainer.Prefix (path, query)
        | Predicate.Fuzzy (fref, query, fuzz) ->
            let path = match fref with | FieldRef.TX f -> f.Path | FieldRef.TWK f -> f.Path | _ -> failwith "Invalid match field"
            let fuzzStr = match fuzz with | Fuzziness.Auto -> "AUTO" | Fuzziness.Edits e -> string e
            QueryContainer.Fuzzy (path, query, fuzzStr)
        | _ -> failwith "Not implemented in Phase 3"

    let where' (p: Predicate<'S>) : Query<'S> =
        { Filter = p; RankBy = None; Aggs = [] }

    let rec lowerAgg (a: Agg<'S>) : AggContainer =
        match a with
        | Agg.Terms (fref, size, sub, orderBy) ->
            let path = 
                match fref with
                | FieldRef.KW f -> f.Path
                | FieldRef.TX f -> failwith "Terms agg on text field not allowed"
                | FieldRef.TWK f -> f.Path + ".keyword"
                | FieldRef.DT f -> f.Path
                | FieldRef.NUM f -> f.Path
                | FieldRef.BL f -> f.Path
            let subAggs = sub |> List.map (fun (n, sa) -> (n, lowerAgg sa))
            AggContainer.Terms (path, size, subAggs, orderBy)
        | Agg.Sum fref ->
            let path = match fref with | FieldRef.NUM f -> f.Path | _ -> failwith "Sum agg requires numeric field"
            AggContainer.Sum path
        | Agg.Max fref ->
            let path = 
                match fref with 
                | FieldRef.NUM f -> f.Path 
                | FieldRef.DT f -> f.Path 
                | _ -> failwith "Max agg requires numeric or date field"
            AggContainer.Max path
        | Agg.Cardinality fref ->
            let path = 
                match fref with
                | FieldRef.KW f -> f.Path
                | FieldRef.TWK f -> f.Path + ".keyword"
                | _ -> failwith "Cardinality agg requires keyword field"
            AggContainer.Cardinality path
        | Agg.TopHits (size, fref, desc) ->
            let path = 
                match fref with
                | FieldRef.KW f -> f.Path
                | FieldRef.TWK f -> f.Path + ".keyword"
                | FieldRef.DT f -> f.Path
                | FieldRef.NUM f -> f.Path
                | _ -> failwith "TopHits sort requires keyword/numeric/date"
            AggContainer.TopHits (size, path, desc)

    let lowerQuery (q: Query<'S>) : QueryContainer =
        lower q.Filter
