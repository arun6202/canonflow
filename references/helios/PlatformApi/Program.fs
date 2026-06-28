namespace PlatformApi

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Cors
open Elastic.Clients.Elasticsearch
open Elastic.Clients.Elasticsearch.Aggregations
open SharedDomain.Dtos

// ----------------------------------------------------
// Handlers
// ----------------------------------------------------
module Handlers =
    open ElasticDsl.Engine

    open Elastic.Clients.Elasticsearch.QueryDsl
    open System.Text.Json

    open Elastic.FSharp.Query
    open Elastic.FSharp.Query.Types
    open Elastic.FSharp.Query.Operators
    open Elastic.FSharp.Query.Lowering
    open Elastic.FSharp.Query.Serialization

    type OrdersIndex = class end
    let fCustomerId : KeywordField<OrdersIndex> = { Path = "customer.customerId.keyword"; Kind = Unchecked.defaultof<_> }
    let fCustomerCountry : KeywordField<OrdersIndex> = { Path = "customer.country.keyword"; Kind = Unchecked.defaultof<_> }
    let fLineSales : NumericField<OrdersIndex, unit> = { Path = "lineSales"; Kind = Unchecked.defaultof<_> }
    let fCategoryName : KeywordField<OrdersIndex> = { Path = "product.categoryName.keyword"; Kind = Unchecked.defaultof<_> }
    let fEmployeeLastName : TextField<OrdersIndex> = { Path = "employee.lastName"; Kind = Unchecked.defaultof<_> }
    let fOrderId : KeywordField<OrdersIndex> = { Path = "orderId"; Kind = Unchecked.defaultof<_> }
    let fOrderDate : DateField<OrdersIndex> = { Path = "orderDate"; Kind = Unchecked.defaultof<_> }
    
    let getClient () =
        let settings = new ElasticsearchClientSettings(Uri("http://localhost:9200"))
        settings.DefaultIndex("orders") |> ignore
        new ElasticsearchClient(settings)

    let northwindConfig : DomainConfig = {
        DomainId = "Northwind"
        DisplayName = "Northwind Orders (Elasticsearch)"
        Fields = [
            { Name = "Country"; DisplayName = "Customer Country"; Type = "string"; SupportsTerms = true; SupportsPrefix = false; SupportsRange = false; SupportsMatch = false }
            { Name = "CustomerId"; DisplayName = "Customer ID"; Type = "string"; SupportsTerms = true; SupportsPrefix = true; SupportsRange = false; SupportsMatch = false }
            { Name = "ProductCategory"; DisplayName = "Product Category"; Type = "string"; SupportsTerms = true; SupportsPrefix = false; SupportsRange = false; SupportsMatch = false }
            { Name = "EmployeeLastName"; DisplayName = "Employee Last Name"; Type = "string"; SupportsTerms = false; SupportsPrefix = false; SupportsRange = false; SupportsMatch = true }
            { Name = "LineSales"; DisplayName = "Line Sales"; Type = "number"; SupportsTerms = false; SupportsPrefix = false; SupportsRange = true; SupportsMatch = false }
            { Name = "OrderId"; DisplayName = "Order ID"; Type = "string"; SupportsTerms = true; SupportsPrefix = false; SupportsRange = false; SupportsMatch = false }
        ]
    }

    let adventureworksConfig : DomainConfig = {
        DomainId = "AdventureWorks"
        DisplayName = "AdventureWorks (SQLite)"
        Fields = [
            { Name = "Country"; DisplayName = "Customer Country"; Type = "string"; SupportsTerms = true; SupportsPrefix = false; SupportsRange = false; SupportsMatch = false }
            { Name = "CustomerId"; DisplayName = "Customer ID"; Type = "string"; SupportsTerms = true; SupportsPrefix = true; SupportsRange = false; SupportsMatch = false }
            { Name = "ProductCategory"; DisplayName = "Product Category"; Type = "string"; SupportsTerms = true; SupportsPrefix = false; SupportsRange = false; SupportsMatch = false }
            { Name = "EmployeeLastName"; DisplayName = "Employee Last Name"; Type = "string"; SupportsTerms = false; SupportsPrefix = false; SupportsRange = false; SupportsMatch = true }
            { Name = "LineSales"; DisplayName = "Line Sales"; Type = "number"; SupportsTerms = false; SupportsPrefix = false; SupportsRange = true; SupportsMatch = false }
            { Name = "OrderId"; DisplayName = "Order ID"; Type = "string"; SupportsTerms = true; SupportsPrefix = false; SupportsRange = false; SupportsMatch = false }
        ]
    }

    let catalogNorthwindConfig =
        Catalog.loadDomainConfigOrFallback
            "Northwind"
            "Northwind Orders (Elasticsearch)"
            northwindConfig

    let catalogAdventureworksConfig =
        Catalog.loadDomainConfigOrFallback
            "AdventureWorks"
            "AdventureWorks (SQLite)"
            adventureworksConfig

    let domainConfigs = dict [
        ("Northwind", catalogNorthwindConfig)
        ("AdventureWorks", catalogAdventureworksConfig)
    ]

    let getSchema (domain: string) : IResult =
        match domainConfigs.TryGetValue(domain) with
        | true, config -> 
            let json = Thoth.Json.Net.Encode.Auto.toString(0, config)
            Results.Content(json, "application/json")
        | false, _ -> Results.NotFound({| Message = $"Domain '{domain}' not found." |})

    let validateCatalog (domain: string) : IResult =
        match Catalog.validateDomain domain with
        | Ok summary -> Results.Ok(summary)
        | Error message -> Results.BadRequest({| Message = message |})

    let elasticProvider = new Engine.ElasticQueryProvider() :> Engine.IQueryProvider
    let sqliteProvider = new Engine.SqliteQueryProvider("Data Source=E:/github/Adventureworks/gem/northwind/adventureworks.db") :> Engine.IQueryProvider

    let providerRegistry = dict [
        ("Northwind", elasticProvider)
        ("AdventureWorks", sqliteProvider)
    ]

    let getProvider (req: Microsoft.AspNetCore.Http.HttpRequest) =
        let domain = if req.Query.ContainsKey("domain") then req.Query.["domain"].ToString() else "Northwind"
        match providerRegistry.TryGetValue(domain) with
        | true, p -> p
        | false, _ -> elasticProvider

    let searchCustomDsl (req: Microsoft.AspNetCore.Http.HttpRequest) : Task<IResult> =
        async {
            use reader = new System.IO.StreamReader(req.Body)
            let! bodyStr = reader.ReadToEndAsync() |> Async.AwaitTask
            let extra = Thoth.Json.Net.Extra.empty |> Thoth.Json.Net.Extra.withDecimal |> Thoth.Json.Net.Extra.withInt64
            
            match Thoth.Json.Net.Decode.Auto.fromString<ClientPredicate>(bodyStr, extra=extra) with
            | Error decodeErr ->
                return Results.BadRequest({ Message = "Thoth Decoding Failed: " + decodeErr })
            | Ok payload ->
                let provider = getProvider req
                let! searchRes = provider.SearchDocuments payload
                match searchRes with
                | Error errMsg ->
                    return Results.BadRequest({ Message = "Validation Failed: " + errMsg })
                | Ok dtos ->
                    let jsonResponse = Thoth.Json.Net.Encode.Auto.toString(0, dtos |> Seq.toList, extra=extra)
                    return Results.Content(jsonResponse, "application/json")
        } |> Async.StartAsTask

    let searchAnalyticsDsl (req: Microsoft.AspNetCore.Http.HttpRequest) : Task<IResult> =
        async {
            use reader = new System.IO.StreamReader(req.Body)
            let! bodyStr = reader.ReadToEndAsync() |> Async.AwaitTask
            let extra = Thoth.Json.Net.Extra.empty |> Thoth.Json.Net.Extra.withDecimal |> Thoth.Json.Net.Extra.withInt64
            
            match Thoth.Json.Net.Decode.Auto.fromString<AnalyticsRequestDto>(bodyStr, extra=extra) with
            | Error decodeErr ->
                return Results.BadRequest({ Message = "Thoth Decoding Failed: " + decodeErr })
            | Ok payload ->
                let provider = getProvider req
                let! aggResult = provider.ExecuteAnalytics(payload.Filter, payload.Aggregations)
                match aggResult with
                | Error errMsg ->
                    return Results.BadRequest({ Message = "Analytics Failed: " + errMsg })
                | Ok dtos ->
                    let jsonResponse = Thoth.Json.Net.Encode.Auto.toString(0, dtos |> Seq.toList, extra=extra)
                    return Results.Content(jsonResponse, "application/json")
        } |> Async.StartAsTask

    // End of Handlers

// ----------------------------------------------------
// Program Setup
// ----------------------------------------------------
module Program =
    [<EntryPoint>]
    let main args =
        let builder = WebApplication.CreateBuilder(args)

        builder.Services.AddCors(fun options ->
            options.AddPolicy("AllowFrontend", fun policy ->
                policy.WithOrigins("http://localhost:8085")
                      .AllowAnyHeader()
                      .AllowAnyMethod() |> ignore
            )
        ) |> ignore
        
        builder.Services.AddEndpointsApiExplorer() |> ignore
        builder.Services.AddSwaggerGen() |> ignore

        // Add F# System.Text.Json support for Discriminated Unions!
        builder.Services.ConfigureHttpJsonOptions(Action<Microsoft.AspNetCore.Http.Json.JsonOptions>(fun options ->
            options.SerializerOptions.Converters.Add(System.Text.Json.Serialization.JsonFSharpConverter())
        )) |> ignore

        let app = builder.Build()

        app.UseCors("AllowFrontend") |> ignore
        app.UseSwagger() |> ignore
        app.UseSwaggerUI() |> ignore

        // Register endpoints
        app.MapGet("/api/schema/{domain}", Func<string, IResult>(Handlers.getSchema)) |> ignore
        app.MapGet("/api/catalog/{domain}/validate", Func<string, IResult>(Handlers.validateCatalog)) |> ignore
        app.MapPost("/api/orders/custom-dsl", Func<HttpRequest, Task<IResult>>(Handlers.searchCustomDsl))
            .Produces<OrderLineDocumentDto seq>(200) |> ignore

        app.MapPost("/api/orders/analytics-dsl", Func<Microsoft.AspNetCore.Http.HttpRequest, Task<IResult>>(Handlers.searchAnalyticsDsl))
            .Produces<AnalyticsResponseDto seq>(200) |> ignore

        app.Run()
        0
