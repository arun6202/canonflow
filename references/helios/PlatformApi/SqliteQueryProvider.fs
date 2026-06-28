namespace PlatformApi.Engine

open System
open Microsoft.Data.Sqlite
open SharedDomain.Dtos

type SqliteQueryProvider (connectionString: string) =
    
    let tryMapField (f: string) =
        match f with
        | "customer.country" | "customer.country.keyword" | "Country" -> Ok "Country"
        | "customer.customerId" | "customer.customerId.keyword" | "CustomerId" -> Ok "CustomerId"
        | "lines.product.categoryName" | "lines.product.categoryName.keyword" | "ProductCategory" -> Ok "ProductCategory"
        | "employee.lastName" | "employee.lastName.keyword" | "EmployeeLastName" -> Ok "EmployeeLastName"
        | "lines.lineSales" | "LineSales" -> Ok "LineSales"
        | "orderId" | "OrderId" -> Ok "OrderId"
        | _ -> Error $"Field '{f}' is not supported by the SQLite provider."

    let numericFields = set [ "LineSales" ]

    // Compiles the ClientPredicate AST into a SQL string and a list of parameters
    let rec compilePredicate (p: ClientPredicate) (paramIndex: int ref) =
        match p with
        | ClientPredicate.Term (f, v) ->
            match tryMapField f with
            | Error e -> Error e
            | Ok mappedF ->
                let pName = $"@p{paramIndex.Value}"
                paramIndex.Value <- paramIndex.Value + 1
                Ok ($"{mappedF} = {pName}", [(pName, box v)])
        | ClientPredicate.Terms (f, vs) ->
            match tryMapField f with
            | Error e -> Error e
            | Ok mappedF ->
                let pNames = vs |> List.mapi (fun i v -> 
                    let pName = $"@p{paramIndex.Value + i}"
                    (pName, box v))
                paramIndex.Value <- paramIndex.Value + vs.Length
                let inClause = String.Join(", ", pNames |> List.map fst)
                Ok ($"{mappedF} IN ({inClause})", pNames)
        | ClientPredicate.Match (f, v) ->
            match tryMapField f with
            | Error e -> Error e
            | Ok mappedF ->
                let pName = $"@p{paramIndex.Value}"
                paramIndex.Value <- paramIndex.Value + 1
                Ok ($"{mappedF} LIKE {pName}", [(pName, box $"%%{v}%%")])
        | ClientPredicate.Prefix (f, v) ->
            match tryMapField f with
            | Error e -> Error e
            | Ok mappedF ->
                let pName = $"@p{paramIndex.Value}"
                paramIndex.Value <- paramIndex.Value + 1
                Ok ($"{mappedF} LIKE {pName}", [(pName, box $"{v}%%")])
        | ClientPredicate.Range (f, min, max) ->
            match tryMapField f with
            | Error e -> Error e
            | Ok mappedF when not (numericFields.Contains mappedF) ->
                Error $"Field '{f}' does not support range queries in SQLite."
            | Ok mappedF ->
                match min, max with
                | Some m, None -> 
                    let pName = $"@p{paramIndex.Value}"
                    paramIndex.Value <- paramIndex.Value + 1
                    Ok ($"{mappedF} >= {pName}", [(pName, box m)])
                | None, Some m -> 
                    let pName = $"@p{paramIndex.Value}"
                    paramIndex.Value <- paramIndex.Value + 1
                    Ok ($"{mappedF} <= {pName}", [(pName, box m)])
                | Some m1, Some m2 -> 
                    let p1 = $"@p{paramIndex.Value}"
                    let p2 = $"@p{paramIndex.Value + 1}"
                    paramIndex.Value <- paramIndex.Value + 2
                    Ok ($"{mappedF} BETWEEN {p1} AND {p2}", [(p1, box m1); (p2, box m2)])
                | _ -> Error "Range must have min or max"
        | ClientPredicate.And ps ->
            let results = ps |> List.map (fun p -> compilePredicate p paramIndex)
            let errors = results |> List.choose (function Error e -> Some e | _ -> None)
            if not errors.IsEmpty then Error (String.Join(", ", errors))
            else
                let clauses = results |> List.choose (function Ok (c, _) -> Some c | _ -> None)
                let allParams = results |> List.collect (function Ok (_, p) -> p | _ -> [])
                let joinedClauses = String.Join(" AND ", clauses)
                Ok ($"({joinedClauses})", allParams)
        | ClientPredicate.Or ps ->
            let results = ps |> List.map (fun p -> compilePredicate p paramIndex)
            let errors = results |> List.choose (function Error e -> Some e | _ -> None)
            if not errors.IsEmpty then Error (String.Join(", ", errors))
            else
                let clauses = results |> List.choose (function Ok (c, _) -> Some c | _ -> None)
                let allParams = results |> List.collect (function Ok (_, p) -> p | _ -> [])
                let joinedClauses = String.Join(" OR ", clauses)
                Ok ($"({joinedClauses})", allParams)
        | ClientPredicate.Not p ->
            match compilePredicate p paramIndex with
            | Ok (c, prms) -> Ok ($"NOT ({c})", prms)
            | Error e -> Error e

    let executeReader (sql: string) (parameters: (string * obj) list) =
        async {
            use connection = new SqliteConnection(connectionString)
            do! connection.OpenAsync() |> Async.AwaitTask
            
            use command = connection.CreateCommand()
            command.CommandText <- sql
            for (pName, pValue) in parameters do
                command.Parameters.AddWithValue(pName, pValue) |> ignore
            
            use! reader = command.ExecuteReaderAsync() |> Async.AwaitTask
            
            let dtos = System.Collections.Generic.List<OrderLineDocumentDto>()
            while reader.Read() do
                let dto = {
                    OrderLineDocumentDto.Id = reader.GetString(0)
                    OrderId = reader.GetInt32(1)
                    OrderDate = reader.GetString(2)
                    Customer = { 
                        CustomerId = reader.GetString(3)
                        CompanyName = "N/A"
                        ContactName = "N/A"
                        Country = reader.GetString(4)
                    }
                    Employee = {
                        EmployeeId = 0
                        LastName = reader.GetString(5)
                        FirstName = "N/A"
                        Title = "N/A"
                    }
                    Product = {
                        ProductId = 0
                        ProductName = "N/A"
                        CategoryName = reader.GetString(6)
                    }
                    UnitPrice = 0m
                    Quantity = 0
                    Discount = 0m
                    LineSales = reader.GetDecimal(7)
                }
                dtos.Add(dto)

            return Ok (dtos :> seq<OrderLineDocumentDto>)
        }

    let privateWhere filter =
        match filter with
        | None -> Ok ("", [])
        | Some predicate ->
            let paramIndex = ref 0
            compilePredicate predicate paramIndex
            |> Result.map (fun (whereClause, parameters) -> $" WHERE {whereClause}", parameters)

    let compileAnalyticsQuery filter agg =
        match privateWhere filter, agg with
        | Error e, _ -> Error e
        | Ok (whereSql, parameters), ClientAggregation.Terms (name, field, size) ->
            match tryMapField field with
            | Error e -> Error e
            | Ok mappedField ->
                let limit = max 1 (min size 100)
                let sql =
                    $"SELECT CAST({mappedField} AS TEXT) AS BucketKey, COUNT(*) AS DocCount, NULL AS SubValue FROM AdventureWorksFlat{whereSql} GROUP BY {mappedField} ORDER BY DocCount DESC LIMIT {limit}"

                Ok (name, sql, parameters)
        | Ok (whereSql, parameters), ClientAggregation.Sum (name, field) ->
            match tryMapField field with
            | Error e -> Error e
            | Ok mappedField when not (numericFields.Contains mappedField) ->
                Error $"Sum aggregation on field '{field}' is not supported by the SQLite provider."
            | Ok mappedField ->
                let sql =
                    $"SELECT 'Sum' AS BucketKey, COUNT(*) AS DocCount, SUM({mappedField}) AS SubValue FROM AdventureWorksFlat{whereSql}"

                Ok (name, sql, parameters)

    let executeAnalyticsQuery (aggName: string) (sql: string) (parameters: (string * obj) list) =
        async {
            use connection = new SqliteConnection(connectionString)
            do! connection.OpenAsync() |> Async.AwaitTask

            use command = connection.CreateCommand()
            command.CommandText <- sql
            for (pName, pValue) in parameters do
                command.Parameters.AddWithValue(pName, pValue) |> ignore

            use! reader = command.ExecuteReaderAsync() |> Async.AwaitTask

            let buckets = System.Collections.Generic.List<BucketDto>()
            while reader.Read() do
                let subValue =
                    if reader.IsDBNull(2) then None
                    else Some (reader.GetDouble(2))

                buckets.Add({
                    Key = reader.GetString(0)
                    DocCount = reader.GetInt64(1)
                    SubValue = subValue
                })

            return { AggName = aggName; Buckets = buckets |> Seq.toList }
        }

    interface IQueryProvider with
        member this.SearchDocuments (predicate: ClientPredicate) =
            let paramIndex = ref 0
            match compilePredicate predicate paramIndex with
            | Error e -> async { return Error e }
            | Ok (whereClause, parameters) ->
                let sql = $"SELECT Id, OrderId, OrderDate, CustomerId, Country, EmployeeLastName, ProductCategory, LineSales FROM AdventureWorksFlat WHERE {whereClause} LIMIT 50"
                executeReader sql parameters

        member this.ExecuteAnalytics (filter: ClientPredicate option, aggs: ClientAggregation list) =
            async {
                let compiled =
                    aggs
                    |> List.map (compileAnalyticsQuery filter)

                let errors =
                    compiled
                    |> List.choose (function | Error e -> Some e | Ok _ -> None)

                if not errors.IsEmpty then
                    return Error (String.Join(", ", errors))
                else
                    let! responses =
                        compiled
                        |> List.choose (function | Ok query -> Some query | Error _ -> None)
                        |> List.map (fun (name, sql, parameters) -> executeAnalyticsQuery name sql parameters)
                        |> Async.Sequential

                    return Ok (responses :> seq<AnalyticsResponseDto>)
            }
