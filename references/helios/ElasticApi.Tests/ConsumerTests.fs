namespace ElasticApi.Tests

open System
open System.Net
open System.Net.Http
open System.Threading.Tasks
open Xunit
open Xunit.Abstractions
open PactNet
open PactNet.Output.Xunit
open PactNet.Matchers
open ElasticApi.Dtos

type ConsumerTests(output: ITestOutputHelper) =
    
    let config = PactConfig()
    do config.Outputters <- seq [ XunitOutput(output) :> PactNet.Infrastructure.Outputters.IOutput ]
    do config.PactDir <- "../../../pacts/"

    let pact = Pact.V4("ElasticApiClient", "ElasticApi", config).WithHttpInteractions()

    [<Fact>]
    member this.``It can search orders`` () =
        pact.UponReceiving("A request to search orders")
            .WithRequest(HttpMethod.Get, "/api/orders/search")
            .WithQuery("q", "Tofu")
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody([|
                {|
                    Id = Match.Type("order-1")
                    OrderDate = Match.Type("1996-07-04T00:00:00")
                    TotalAmount = Match.Decimal(12.34M)
                    Customer = {|
                        CustomerId = Match.Type("VINET")
                        CompanyName = Match.Type("Vins et alcools Chevalier")
                        ContactName = Match.Type("Paul Henriot")
                        Country = Match.Type("France")
                    |}
                    Employee = {|
                        EmployeeId = Match.Type(5)
                        FirstName = Match.Type("Steven")
                        LastName = Match.Type("Buchanan")
                        Title = Match.Type("Sales Manager")
                    |}
                    Lines = [|
                        {|
                            UnitPrice = Match.Decimal(14.0M)
                            Quantity = Match.Type(12)
                            Discount = Match.Decimal(0.0M)
                            Product = {|
                                ProductId = Match.Type(11)
                                ProductName = Match.Type("Queso Cabrales")
                                CategoryName = Match.Type("Dairy Products")
                            |}
                        |}
                    |]
                |}
            |]) |> ignore

        pact.VerifyAsync(fun ctx ->
            async {
                let client = ElasticApiClient(ctx.MockServerUri.ToString())
                let! results = client.SearchOrdersAsync("Tofu") |> Async.AwaitTask
                Assert.NotEmpty(results)
                Assert.Equal("order-1", results.[0].Id)
            } |> Async.StartAsTask :> Task
        ) |> Async.AwaitTask |> Async.RunSynchronously

    [<Fact>]
    member this.``It can get top customers aggregation`` () =
        pact.UponReceiving("A request for top customers")
            .WithRequest(HttpMethod.Get, "/api/analytics/top-customers")
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody([|
                {|
                    Key = Match.Type("QUICK-Stop")
                    Revenue = Match.Decimal(117483.39M)
                |}
            |]) |> ignore

        pact.VerifyAsync(fun ctx ->
            async {
                let client = ElasticApiClient(ctx.MockServerUri.ToString())
                let! results = client.GetTopCustomersAsync() |> Async.AwaitTask
                Assert.NotEmpty(results)
                Assert.Equal("QUICK-Stop", results.[0].Key)
            } |> Async.StartAsTask :> Task
        ) |> Async.AwaitTask |> Async.RunSynchronously

    [<Fact>]
    member this.``It can filter orders by employee`` () =
        pact.UponReceiving("A request for orders by employee")
            .WithRequest(HttpMethod.Get, "/api/orders/employee/1")
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody([|
                {|
                    Id = Match.Type("order-2")
                    OrderDate = Match.Type("1996-07-04T00:00:00")
                    TotalAmount = Match.Decimal(10.00M)
                    Customer = {|
                        CustomerId = Match.Type("ALFKI")
                        CompanyName = Match.Type("Alfreds Futterkiste")
                        ContactName = Match.Type("Maria Anders")
                        Country = Match.Type("Germany")
                    |}
                    Employee = {|
                        EmployeeId = Match.Type(1)
                        FirstName = Match.Type("Nancy")
                        LastName = Match.Type("Davolio")
                        Title = Match.Type("Sales Representative")
                    |}
                    Lines = [||]
                |}
            |]) |> ignore

        pact.VerifyAsync(fun ctx ->
            async {
                let client = ElasticApiClient(ctx.MockServerUri.ToString())
                let! results = client.GetOrdersByEmployeeAsync(1) |> Async.AwaitTask
                Assert.NotEmpty(results)
                Assert.Equal("order-2", results.[0].Id)
            } |> Async.StartAsTask :> Task
        ) |> Async.AwaitTask |> Async.RunSynchronously

    [<Fact>]
    member this.``It can search orders using SQS`` () =
        pact.UponReceiving("A request for SQS search")
            .WithRequest(HttpMethod.Get, "/api/orders/sqs")
            .WithQuery("q", "chees~2")
            .WithQuery("defaultOperator", "AND")
            .WithQuery("fields", "lines.product.productName")
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody([|
                {|
                    Id = Match.Type("order-3")
                    OrderDate = Match.Type("1996-07-04T00:00:00")
                    TotalAmount = Match.Decimal(10.00M)
                    Customer = {|
                        CustomerId = Match.Type("ALFKI")
                        CompanyName = Match.Type("Alfreds Futterkiste")
                        ContactName = Match.Type("Maria Anders")
                        Country = Match.Type("Germany")
                    |}
                    Employee = {|
                        EmployeeId = Match.Type(1)
                        FirstName = Match.Type("Nancy")
                        LastName = Match.Type("Davolio")
                        Title = Match.Type("Sales Representative")
                    |}
                    Lines = [||]
                |}
            |]) |> ignore

        pact.VerifyAsync(fun ctx ->
            async {
                let client = ElasticApiClient(ctx.MockServerUri.ToString())
                let! results = client.SearchOrdersSqsAsync("chees~2", "AND", "lines.product.productName") |> Async.AwaitTask
                Assert.NotEmpty(results)
                Assert.Equal("order-3", results.[0].Id)
            } |> Async.StartAsTask :> Task
        ) |> Async.AwaitTask |> Async.RunSynchronously

    [<Fact>]
    member this.``It can search orders using SQS compound query`` () =
        pact.UponReceiving("A request for SQS compound search")
            .WithRequest(HttpMethod.Get, "/api/orders/sqs/compound")
            .WithQuery("q", "seafood -crab")
            .WithQuery("employeeId", "1")
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody([|
                {|
                    Id = Match.Type("order-4")
                    OrderDate = Match.Type("1996-07-04T00:00:00")
                    TotalAmount = Match.Decimal(20.00M)
                    Customer = {|
                        CustomerId = Match.Type("ANATR")
                        CompanyName = Match.Type("Ana Trujillo Emparedados y helados")
                        ContactName = Match.Type("Ana Trujillo")
                        Country = Match.Type("Mexico")
                    |}
                    Employee = {|
                        EmployeeId = Match.Type(1)
                        FirstName = Match.Type("Nancy")
                        LastName = Match.Type("Davolio")
                        Title = Match.Type("Sales Representative")
                    |}
                    Lines = [||]
                |}
            |]) |> ignore

        pact.VerifyAsync(fun ctx ->
            async {
                let client = ElasticApiClient(ctx.MockServerUri.ToString())
                let! results = client.SearchOrdersSqsCompoundAsync("seafood -crab", 1) |> Async.AwaitTask
                Assert.NotEmpty(results)
                Assert.Equal("order-4", results.[0].Id)
            } |> Async.StartAsTask :> Task
        ) |> Async.AwaitTask |> Async.RunSynchronously

    [<Fact>]
    member this.``It can search orders using Custom DSL`` () =
        let expectedPayload = {
            ElasticApi.Dtos.CustomDslQuery.Operator = "OR"
            Conditions = null
            Groups = [|
                {
                    ElasticApi.Dtos.CustomDslQuery.Operator = "AND"
                    Conditions = [|
                        { ElasticApi.Dtos.DslCondition.Field = "customer.country"; Value = "Germany"; IsExactMatch = true }
                        { ElasticApi.Dtos.DslCondition.Field = "employee.lastName"; Value = "Davolio"; IsExactMatch = false }
                    |]
                    Groups = null
                }
            |]
        }
        
        pact.UponReceiving("A request for Custom DSL search")
            .WithRequest(HttpMethod.Post, "/api/orders/custom-dsl")
            .WithJsonBody(expectedPayload)
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody([|
                {|
                    Id = Match.Type("order-5")
                    OrderDate = Match.Type("1996-07-04T00:00:00")
                    TotalAmount = Match.Decimal(30.00M)
                    Customer = {|
                        CustomerId = Match.Type("ALFKI")
                        CompanyName = Match.Type("Alfreds Futterkiste")
                        ContactName = Match.Type("Maria Anders")
                        Country = Match.Type("Germany")
                    |}
                    Employee = {|
                        EmployeeId = Match.Type(1)
                        FirstName = Match.Type("Nancy")
                        LastName = Match.Type("Davolio")
                        Title = Match.Type("Sales Representative")
                    |}
                    Lines = [||]
                |}
            |]) |> ignore

        pact.VerifyAsync(fun ctx ->
            async {
                let client = ElasticApiClient(ctx.MockServerUri.ToString())
                let! results = client.SearchOrdersCustomDslAsync(expectedPayload) |> Async.AwaitTask
                Assert.NotEmpty(results)
                Assert.Equal("order-5", results.[0].Id)
            } |> Async.StartAsTask :> Task
        ) |> Async.AwaitTask |> Async.RunSynchronously
