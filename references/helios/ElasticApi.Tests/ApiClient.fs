namespace ElasticApi.Tests

open System
open System.Net.Http
open System.Text.Json
open System.Threading.Tasks

type ElasticApiClient(baseUri: string) =
    let client = new HttpClient()
    do client.BaseAddress <- System.Uri(baseUri)

    let options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)

    member this.SearchOrdersAsync(keyword: string) =
        async {
            let! response = client.GetAsync($"/api/orders/search?q={keyword}") |> Async.AwaitTask
            response.EnsureSuccessStatusCode() |> ignore
            let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            return JsonSerializer.Deserialize<ElasticApi.Dtos.OrderDocumentDto array>(content, options)
        } |> Async.StartAsTask

    member this.GetTopCustomersAsync() =
        async {
            let! response = client.GetAsync("/api/analytics/top-customers") |> Async.AwaitTask
            response.EnsureSuccessStatusCode() |> ignore
            let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            return JsonSerializer.Deserialize<ElasticApi.Dtos.AggregationBucketDto array>(content, options)
        } |> Async.StartAsTask

    member this.GetOrdersByEmployeeAsync(employeeId: int) =
        async {
            let! response = client.GetAsync($"/api/orders/employee/{employeeId}") |> Async.AwaitTask
            response.EnsureSuccessStatusCode() |> ignore
            let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            return JsonSerializer.Deserialize<ElasticApi.Dtos.OrderDocumentDto array>(content, options)
        } |> Async.StartAsTask

    member this.SearchOrdersSqsAsync(keyword: string, defaultOperator: string, fields: string) =
        async {
            let mutable url = $"/api/orders/sqs?q={Uri.EscapeDataString(keyword)}"
            if not (String.IsNullOrEmpty(defaultOperator)) then url <- url + $"&defaultOperator={defaultOperator}"
            if not (String.IsNullOrEmpty(fields)) then url <- url + $"&fields={fields}"
            let! response = client.GetAsync(url) |> Async.AwaitTask
            response.EnsureSuccessStatusCode() |> ignore
            let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            return JsonSerializer.Deserialize<ElasticApi.Dtos.OrderDocumentDto array>(content, options)
        } |> Async.StartAsTask

    member this.SearchOrdersSqsCompoundAsync(keyword: string, employeeId: int) =
        async {
            let url = $"/api/orders/sqs/compound?q={Uri.EscapeDataString(keyword)}&employeeId={employeeId}"
            let! response = client.GetAsync(url) |> Async.AwaitTask
            response.EnsureSuccessStatusCode() |> ignore
            let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            return JsonSerializer.Deserialize<ElasticApi.Dtos.OrderDocumentDto array>(content, options)
        } |> Async.StartAsTask

    member this.SearchOrdersCustomDslAsync(payload: ElasticApi.Dtos.CustomDslQuery) =
        async {
            let json = JsonSerializer.Serialize(payload)
            use content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            let! response = client.PostAsync("/api/orders/custom-dsl", content) |> Async.AwaitTask
            response.EnsureSuccessStatusCode() |> ignore
            let! respContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            return JsonSerializer.Deserialize<ElasticApi.Dtos.OrderDocumentDto array>(respContent, options)
        } |> Async.StartAsTask
