namespace PlatformApi.Engine

open System
open SharedDomain.Dtos
open ElasticDsl.Engine
open Elastic.Clients.Elasticsearch.QueryDsl
open Elastic.FSharp.Query
open Elastic.FSharp.Query.Types
open Elastic.FSharp.Query.Operators
open Elastic.FSharp.Query.Lowering
open Elastic.FSharp.Query.Serialization

type OrdersIndex = class end

module ElasticMappings =
    let fCustomerId : KeywordField<OrdersIndex> = { Path = "customer.customerId.keyword"; Kind = Unchecked.defaultof<_> }
    let fCustomerCountry : KeywordField<OrdersIndex> = { Path = "customer.country.keyword"; Kind = Unchecked.defaultof<_> }
    let fLineSales : NumericField<OrdersIndex, unit> = { Path = "lineSales"; Kind = Unchecked.defaultof<_> }
    let fCategoryName : KeywordField<OrdersIndex> = { Path = "product.categoryName.keyword"; Kind = Unchecked.defaultof<_> }
    let fEmployeeLastName : TextField<OrdersIndex> = { Path = "employee.lastName"; Kind = Unchecked.defaultof<_> }
    let fOrderId : KeywordField<OrdersIndex> = { Path = "orderId"; Kind = Unchecked.defaultof<_> }
    let fOrderDate : DateField<OrdersIndex> = { Path = "orderDate"; Kind = Unchecked.defaultof<_> }

    let rec mapClientPredicate (p: ClientPredicate) : Result<Predicate<OrdersIndex>, string> =
        match p with
        | ClientPredicate.Term (f, v) ->
            match f with
            | "Country" -> Ok (fCustomerCountry =. v)
            | "CustomerId" -> Ok (fCustomerId =. v)
            | "ProductCategory" -> Ok (fCategoryName =. v)
            | "OrderId" -> Ok (fOrderId =. v)
            | _ -> Error $"Field '{f}' does not support exact Term matching."
        | ClientPredicate.Terms (f, vs) ->
            match f with
            | "Country" -> Ok (in' fCustomerCountry vs)
            | "CustomerId" -> Ok (in' fCustomerId vs)
            | "ProductCategory" -> Ok (in' fCategoryName vs)
            | "OrderId" -> Ok (in' fOrderId vs)
            | _ -> Error $"Field '{f}' does not support exact Terms matching."
        | ClientPredicate.Match (f, v) ->
            match f with
            | "EmployeeLastName" -> Ok (phrase fEmployeeLastName v)
            | _ -> Error $"Field '{f}' does not support FullText Match."
        | ClientPredicate.Prefix (f, v) ->
            match f with
            | "CustomerId" -> Ok (Predicate.Prefix (FieldRef.KW fCustomerId, v))
            | _ -> Error $"Field '{f}' does not support Prefix matching."
        | ClientPredicate.Range (f, min, max) ->
            match f with
            | "LineSales" -> 
                match min, max with
                | Some m, None -> Ok (fLineSales >. m)
                | None, Some m -> Ok (fLineSales <. m)
                | Some m, Some x -> Ok (Predicate.All [fLineSales >. m; fLineSales <. x])
                | _ -> Error "Range must have min or max."
            | _ -> Error $"Field '{f}' does not support Range queries."
        | ClientPredicate.And ps ->
            let results = ps |> List.map mapClientPredicate
            let errors = results |> List.choose (function Error e -> Some e | _ -> None)
            if not errors.IsEmpty then Error (String.Join(", ", errors))
            else Ok (Predicate.All (results |> List.choose (function Ok q -> Some q | _ -> None)))
        | ClientPredicate.Or ps ->
            let results = ps |> List.map mapClientPredicate
            let errors = results |> List.choose (function Error e -> Some e | _ -> None)
            if not errors.IsEmpty then Error (String.Join(", ", errors))
            else Ok (Predicate.Any ((results |> List.choose (function Ok q -> Some q | _ -> None)), MinShouldMatch.Count 1))
        | ClientPredicate.Not p ->
            mapClientPredicate p |> Result.map not'

    let mapClientAggregation (agg: ClientAggregation) : Result<(string * Agg<OrdersIndex>), string> =
        match agg with
        | ClientAggregation.Terms (name, f, size) ->
            match f with
            | "Country" -> Ok (name, Aggregations.terms (FieldRef.KW fCustomerCountry) size)
            | "ProductCategory" -> Ok (name, Aggregations.terms (FieldRef.KW fCategoryName) size)
            | _ -> Error $"Terms aggregation on field '{f}' is not supported."
        | ClientAggregation.Sum (name, f) ->
            match f with
            | "LineSales" -> 
                let fLineSalesObj : Field<OrdersIndex, Numeric, obj> = { Path = fLineSales.Path; Kind = Unchecked.defaultof<_> }
                Ok (name, Aggregations.sum (FieldRef.NUM fLineSalesObj))
            | _ -> Error $"Sum aggregation on field '{f}' is not supported."

type ElasticQueryProvider () =
    
    let executeAggQuery (q: Query<'S>) =
        async {
            let jsonQueryNode = Codec.encode (Query.lowerQuery q)
            let payloadNode = System.Text.Json.Nodes.JsonObject()
            payloadNode.Add("query", jsonQueryNode)
            payloadNode.Add("size", System.Text.Json.Nodes.JsonValue.Create(0))
            
            if not q.Aggs.IsEmpty then
                let aggsLowered = q.Aggs |> List.map (fun (n, a) -> (n, Query.lowerAgg a))
                payloadNode.Add("aggs", Codec.encodeAggs aggsLowered)

            let jsonString = payloadNode.ToJsonString()

            use hc = new System.Net.Http.HttpClient()
            hc.BaseAddress <- System.Uri("http://localhost:9200")
            let content = new System.Net.Http.StringContent(jsonString, System.Text.Encoding.UTF8, "application/json")
            
            let! res = hc.PostAsync("orders/_search", content) |> Async.AwaitTask
            let! str = res.Content.ReadAsStringAsync() |> Async.AwaitTask

            if res.IsSuccessStatusCode then
                let doc = System.Text.Json.JsonDocument.Parse(str)
                return Ok (doc.RootElement.GetProperty("aggregations"))
            else
                return Error str
        }

    interface IQueryProvider with
        member this.SearchDocuments (predicate: ClientPredicate) =
            async {
                match ElasticMappings.mapClientPredicate predicate with
                | Error errMsg -> return Error errMsg
                | Ok safePredicate ->
                    let q = { Filter = safePredicate; RankBy = None; Aggs = [] }
                    let jsonQueryNode = Codec.encode (Query.lowerQuery q)
                    let payloadNode = System.Text.Json.Nodes.JsonObject()
                    payloadNode.Add("query", jsonQueryNode)
                    payloadNode.Add("size", System.Text.Json.Nodes.JsonValue.Create(50))
                    let jsonString = payloadNode.ToJsonString()

                    try
                        use hc = new System.Net.Http.HttpClient()
                        hc.BaseAddress <- System.Uri("http://localhost:9200")
                        let content = new System.Net.Http.StringContent(jsonString, System.Text.Encoding.UTF8, "application/json")
                        let! res = hc.PostAsync("orders/_search", content) |> Async.AwaitTask
                        let! str = res.Content.ReadAsStringAsync() |> Async.AwaitTask
                        
                        if res.IsSuccessStatusCode then
                            let doc = System.Text.Json.JsonDocument.Parse(str)
                            let hitsArray = doc.RootElement.GetProperty("hits").GetProperty("hits").EnumerateArray() |> Seq.toList
                            let opts = System.Text.Json.JsonSerializerOptions(PropertyNameCaseInsensitive = true)
                            let decoded = 
                                hitsArray 
                                |> List.collect (fun h -> 
                                    try
                                        let source = h.GetProperty("_source")
                                        let id = if source.TryGetProperty("id") |> fst then source.GetProperty("id").ToString() else ""
                                        let orderDate = if source.TryGetProperty("orderDate") |> fst then source.GetProperty("orderDate").ToString() else ""
                                        let customer = System.Text.Json.JsonSerializer.Deserialize<CustomerDto>(source.GetProperty("customer").GetRawText(), opts)
                                        let employee = System.Text.Json.JsonSerializer.Deserialize<EmployeeDto>(source.GetProperty("employee").GetRawText(), opts)
                                        
                                        if source.TryGetProperty("lines") |> fst then
                                            let linesArray = source.GetProperty("lines").EnumerateArray() |> Seq.toList
                                            linesArray |> List.map (fun lineElem ->
                                                let product = System.Text.Json.JsonSerializer.Deserialize<ProductDto>(lineElem.GetProperty("product").GetRawText(), opts)
                                                let unitPrice = lineElem.GetProperty("unitPrice").GetDecimal()
                                                let quantity = lineElem.GetProperty("quantity").GetInt32()
                                                let discount = lineElem.GetProperty("discount").GetDecimal()
                                                let lineSales = unitPrice * decimal quantity * (1m - discount)
                                                { 
                                                    OrderLineDocumentDto.Id = id
                                                    OrderId = match System.Int32.TryParse(id) with | true, v -> v | _ -> 0
                                                    OrderDate = orderDate
                                                    Customer = customer
                                                    Employee = employee
                                                    Product = product
                                                    UnitPrice = unitPrice
                                                    Quantity = quantity
                                                    Discount = discount
                                                    LineSales = lineSales
                                                }
                                            )
                                        else []
                                    with _ -> []
                                )
                            return Ok (decoded |> Seq.ofList)
                        else
                            return Error str
                    with e ->
                        return Error e.Message
            }

        member this.ExecuteAnalytics (filter: ClientPredicate option, aggs: ClientAggregation list) =
            async {
                let filterRes = 
                    match filter with
                    | Some p -> ElasticMappings.mapClientPredicate p
                    | None -> Ok (Predicate.All [])
                
                match filterRes with
                | Error errMsg -> return Error errMsg
                | Ok safePredicate ->
                    let mappedAggs = aggs |> List.map ElasticMappings.mapClientAggregation
                    let errors = mappedAggs |> List.choose (function Error e -> Some e | _ -> None)
                    if not errors.IsEmpty then 
                        return Error (String.Join(", ", errors))
                    else
                        let loweredAggs = mappedAggs |> List.choose (function Ok a -> Some a | _ -> None)
                        let q = { Filter = safePredicate; RankBy = None; Aggs = loweredAggs }
                        
                        let! aggResult = executeAggQuery q
                        match aggResult with
                        | Error e -> return Error e
                        | Ok jsonElement ->
                            let dtos = 
                                loweredAggs |> List.choose (fun (name, agg) ->
                                    if jsonElement.TryGetProperty(name) |> fst then
                                        let aggElem = jsonElement.GetProperty(name)
                                        let buckets = 
                                            match agg with
                                            | Agg.Terms _ ->
                                                let bElems = aggElem.GetProperty("buckets").EnumerateArray() |> Seq.toList
                                                bElems |> List.map (fun b -> 
                                                    { Key = b.GetProperty("key").ToString()
                                                      DocCount = b.GetProperty("doc_count").GetInt64()
                                                      SubValue = None })
                                            | Agg.Sum _ ->
                                                [ { Key = "Sum"; DocCount = 1L; SubValue = Some (aggElem.GetProperty("value").GetDouble()) } ]
                                            | _ -> []
                                        Some { AggName = name; Buckets = buckets }
                                    else None
                                )
                            return Ok (dtos |> Seq.ofList)
            }
