namespace ElasticDsl

open System
open Elastic.Clients.Elasticsearch
open Elastic.Clients.Elasticsearch.QueryDsl
open Elastic.Clients.Elasticsearch.Aggregations

// ----------------------------------------------------
// State-Of-The-Art F# Elastic DSL Engine
// ----------------------------------------------------
module Engine =

    // The state carried through the computation expression
    type QueryContext<'T> = {
        MustClauses: Action<QueryDescriptor<'T>> list
        ShouldClauses: Action<QueryDescriptor<'T>> list
        FilterClauses: Action<QueryDescriptor<'T>> list
        Size: int option
        From: int option
    }

    type ElasticQueryBuilder<'T>() =
        
        member _.Yield(_) = 
            { MustClauses = []; ShouldClauses = []; FilterClauses = []; Size = None; From = None }

        // --- MATCH operations ---
        
        [<CustomOperation("whereMatch")>]
        member _.WhereMatch(state: QueryContext<'T>, field: string, value: string) =
            let clause = Action<QueryDescriptor<'T>>(fun q ->
                q.Match(Action<MatchQueryDescriptor<'T>>(fun m ->
                    m.Field(Field.op_Implicit(field)).Query(value) |> ignore
                )) |> ignore
            )
            { state with MustClauses = state.MustClauses @ [clause] }

        [<CustomOperation("andWhereMatch")>]
        member this.AndWhereMatch(state: QueryContext<'T>, field: string, value: string) =
            this.WhereMatch(state, field, value)

        [<CustomOperation("orWhereMatch")>]
        member _.OrWhereMatch(state: QueryContext<'T>, field: string, value: string) =
            let clause = Action<QueryDescriptor<'T>>(fun q ->
                q.Match(Action<MatchQueryDescriptor<'T>>(fun m ->
                    m.Field(Field.op_Implicit(field)).Query(value) |> ignore
                )) |> ignore
            )
            { state with ShouldClauses = state.ShouldClauses @ [clause] }

        [<CustomOperation("whereMatchIf")>]
        member _.WhereMatchIf(state: QueryContext<'T>, condition: bool, field: string, value: string) =
            if condition then
                let clause = Action<QueryDescriptor<'T>>(fun q ->
                    q.Match(Action<MatchQueryDescriptor<'T>>(fun m ->
                        m.Field(Field.op_Implicit(field)).Query(value) |> ignore
                    )) |> ignore
                )
                { state with MustClauses = state.MustClauses @ [clause] }
            else state

        // --- TERM operations ---
        
        [<CustomOperation("whereTerm")>]
        member _.WhereTerm(state: QueryContext<'T>, field: string, value: string) =
            let clause = Action<QueryDescriptor<'T>>(fun q ->
                q.Term(Action<TermQueryDescriptor<'T>>(fun m ->
                    m.Field(Field.op_Implicit(field)).Value(FieldValue.op_Implicit(value)) |> ignore
                )) |> ignore
            )
            { state with MustClauses = state.MustClauses @ [clause] }

        [<CustomOperation("andWhereTerm")>]
        member this.AndWhereTerm(state: QueryContext<'T>, field: string, value: string) =
            this.WhereTerm(state, field, value)

        [<CustomOperation("andWhereTermIf")>]
        member _.AndWhereTermIf(state: QueryContext<'T>, condition: bool, field: string, value: string) =
            if condition then
                let clause = Action<QueryDescriptor<'T>>(fun q ->
                    q.Term(Action<TermQueryDescriptor<'T>>(fun m ->
                        m.Field(Field.op_Implicit(field)).Value(FieldValue.op_Implicit(value)) |> ignore
                    )) |> ignore
                )
                { state with MustClauses = state.MustClauses @ [clause] }
            else state

        // --- FILTER operations ---

        [<CustomOperation("filterTerm")>]
        member _.FilterTerm(state: QueryContext<'T>, field: string, value: int) =
            let clause = Action<QueryDescriptor<'T>>(fun q ->
                q.Term(Action<TermQueryDescriptor<'T>>(fun m ->
                    m.Field(Field.op_Implicit(field)).Value(FieldValue.op_Implicit(value)) |> ignore
                )) |> ignore
            )
            { state with FilterClauses = state.FilterClauses @ [clause] }

        [<CustomOperation("andFilterTermIf")>]
        member _.AndFilterTermIf(state: QueryContext<'T>, condition: bool, field: string, value: int) =
            if condition then
                let clause = Action<QueryDescriptor<'T>>(fun q ->
                    q.Term(Action<TermQueryDescriptor<'T>>(fun m ->
                        m.Field(Field.op_Implicit(field)).Value(FieldValue.op_Implicit(value)) |> ignore
                    )) |> ignore
                )
                { state with FilterClauses = state.FilterClauses @ [clause] }
            else state

        // --- PAGINATION operations ---
        
        [<CustomOperation("take")>]
        member _.Take(state: QueryContext<'T>, count: int) =
            { state with Size = Some count }
            
        [<CustomOperation("skip")>]
        member _.Skip(state: QueryContext<'T>, count: int) =
            { state with From = Some count }

        // --- Compilation ---

        member _.Run(state: QueryContext<'T>) =
            // Return a configured SearchRequestDescriptor builder
            Action<SearchRequestDescriptor<'T>>(fun req ->
                
                // Set pagination
                if state.Size.IsSome then req.Size(state.Size.Value) |> ignore
                if state.From.IsSome then req.From(state.From.Value) |> ignore

                // Set query
                req.Query(Action<QueryDescriptor<'T>>(fun q ->
                    q.Bool(Action<BoolQueryDescriptor<'T>>(fun bq ->
                        if not (List.isEmpty state.MustClauses) then 
                            bq.Must(state.MustClauses |> Seq.toArray) |> ignore
                        if not (List.isEmpty state.ShouldClauses) then 
                            bq.Should(state.ShouldClauses |> Seq.toArray).MinimumShouldMatch(MinimumShouldMatch.op_Implicit(1)) |> ignore
                        if not (List.isEmpty state.FilterClauses) then 
                            bq.Filter(state.FilterClauses |> Seq.toArray) |> ignore
                    )) |> ignore
                )) |> ignore
            )

    // The generic, global DSL builder
    let elasticQuery<'T> = ElasticQueryBuilder<'T>()

    // ----------------------------------------------------
    // Analytics & Aggregations DSL
    // ----------------------------------------------------

    type AnalyticsContext<'T> = {
        GroupByField: string option
        GroupByName: string option
        GroupByLimit: int
        SubAggs: (string * Action<AggregationDescriptor<'T>>) list
    }

    type ElasticAnalyticsBuilder<'T>() =
        member _.Yield(_) = 
            { GroupByField = None; GroupByName = None; GroupByLimit = 1000; SubAggs = [] }

        [<CustomOperation("groupBy")>]
        member _.GroupBy(state: AnalyticsContext<'T>, name: string, field: string, limit: int) =
            { state with GroupByName = Some name; GroupByField = Some field; GroupByLimit = limit }

        [<CustomOperation("sum")>]
        member _.Sum(state: AnalyticsContext<'T>, name: string, field: string) =
            let agg = Action<AggregationDescriptor<'T>>(fun a ->
                a.Sum(Action<SumAggregationDescriptor<'T>>(fun s -> s.Field(Field.op_Implicit(field)) |> ignore)) |> ignore
            )
            { state with SubAggs = state.SubAggs @ [(name, agg)] }

        [<CustomOperation("avg")>]
        member _.Avg(state: AnalyticsContext<'T>, name: string, field: string) =
            let agg = Action<AggregationDescriptor<'T>>(fun a ->
                a.Avg(Action<AverageAggregationDescriptor<'T>>(fun s -> s.Field(Field.op_Implicit(field)) |> ignore)) |> ignore
            )
            { state with SubAggs = state.SubAggs @ [(name, agg)] }

        [<CustomOperation("max")>]
        member _.Max(state: AnalyticsContext<'T>, name: string, field: string) =
            let agg = Action<AggregationDescriptor<'T>>(fun a ->
                a.Max(Action<MaxAggregationDescriptor<'T>>(fun s -> s.Field(Field.op_Implicit(field)) |> ignore)) |> ignore
            )
            { state with SubAggs = state.SubAggs @ [(name, agg)] }

        [<CustomOperation("cardinality")>]
        member _.Cardinality(state: AnalyticsContext<'T>, name: string, field: string) =
            let agg = Action<AggregationDescriptor<'T>>(fun a ->
                a.Cardinality(Action<CardinalityAggregationDescriptor<'T>>(fun s -> s.Field(Field.op_Implicit(field)) |> ignore)) |> ignore
            )
            { state with SubAggs = state.SubAggs @ [(name, agg)] }

        [<CustomOperation("topHits")>]
        member _.TopHits(state: AnalyticsContext<'T>, name: string, size: int, sortField: string) =
            let agg = Action<AggregationDescriptor<'T>>(fun a ->
                let sort = Elastic.Clients.Elasticsearch.FieldSort()
                sort.Order <- Nullable(SortOrder.Desc)
                a.TopHits(Action<TopHitsAggregationDescriptor<'T>>(fun th ->
                    th.Size(size).Sort(Action<Elastic.Clients.Elasticsearch.SortOptionsDescriptor<'T>>(fun srt -> 
                        srt.Field(Field.op_Implicit(sortField), sort) |> ignore
                    )) |> ignore
                )) |> ignore
            )
            { state with SubAggs = state.SubAggs @ [(name, agg)] }

        member _.Run(state: AnalyticsContext<'T>) =
            Action<SearchRequestDescriptor<'T>>(fun req ->
                req.Size(0) |> ignore // We only want aggregation results, not documents

                if state.GroupByField.IsSome && state.GroupByName.IsSome then
                    req.Aggregations(
                        Func<Elastic.Clients.Elasticsearch.Fluent.FluentDescriptorDictionary<string, AggregationDescriptor<'T>>, Elastic.Clients.Elasticsearch.Fluent.FluentDescriptorDictionary<string, AggregationDescriptor<'T>>>(fun d ->
                            let tAgg = Action<AggregationDescriptor<'T>>(fun a ->
                                // Configure the Terms aggregation itself
                                a.Terms(Action<TermsAggregationDescriptor<'T>>(fun t ->
                                    t.Field(Field.op_Implicit(state.GroupByField.Value))
                                     .Size(state.GroupByLimit) |> ignore
                                )) |> ignore
                                    
                                // Configure SubAggregations on the SAME level
                                if not (List.isEmpty state.SubAggs) then
                                    a.Aggregations(
                                        Func<Elastic.Clients.Elasticsearch.Fluent.FluentDescriptorDictionary<string, AggregationDescriptor<'T>>, Elastic.Clients.Elasticsearch.Fluent.FluentDescriptorDictionary<string, AggregationDescriptor<'T>>>(fun subD ->
                                            let mutable currentSubD = subD
                                            for (n, subAgg) in state.SubAggs do
                                                currentSubD <- currentSubD.Add(n, subAgg)
                                            currentSubD
                                        )
                                    ) |> ignore
                            )
                            d.Add(state.GroupByName.Value, tAgg)
                        )
                    ) |> ignore
            )

    let elasticAnalytics<'T> = ElasticAnalyticsBuilder<'T>()
