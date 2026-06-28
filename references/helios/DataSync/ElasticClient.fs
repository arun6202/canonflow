namespace DataSync.Infrastructure

open Elastic.Clients.Elasticsearch
open Elastic.Transport
open System
open DataSync.Domain.SimpleTypes
open DataSync.Domain.CompoundTypes

module ElasticManager =

    // DTOs for Elasticsearch serialization boundary
    type ProductDto = { ProductId: int; ProductName: string; CategoryName: string }
    type OrderLineDto = { Product: ProductDto; UnitPrice: decimal; Quantity: int; Discount: decimal }
    type CustomerDto = { CustomerId: string; CompanyName: string; ContactName: string; Country: string }
    type EmployeeDto = { EmployeeId: int; FirstName: string; LastName: string; Title: string }
    
    type OrderLineDocumentDto = {
        Id: string
        OrderId: int
        OrderDate: string
        Customer: CustomerDto
        Employee: EmployeeDto
        Product: ProductDto
        UnitPrice: decimal
        Quantity: int
        Discount: decimal
        LineSales: decimal
    }

    let private toDto (doc: OrderLineDocument) : OrderLineDocumentDto =
        { Id = doc.Id
          OrderId = doc.OrderId
          OrderDate = doc.OrderDate
          Customer = 
            { CustomerId = CustomerId.value doc.Customer.CustomerId
              CompanyName = doc.Customer.CompanyName
              ContactName = doc.Customer.ContactName
              Country = doc.Customer.Country }
          Employee = 
            { EmployeeId = EmployeeId.value doc.Employee.EmployeeId
              FirstName = doc.Employee.FirstName
              LastName = doc.Employee.LastName
              Title = doc.Employee.Title }
          Product = 
            { ProductId = ProductId.value doc.Product.ProductId
              ProductName = doc.Product.ProductName
              CategoryName = doc.Product.CategoryName }
          UnitPrice = doc.UnitPrice
          Quantity = doc.Quantity
          Discount = doc.Discount
          LineSales = doc.LineSales }

    // Return Result for Railway-Oriented Programming
    let indexData (esUrl: string) (orders: OrderLineDocument list) : Result<unit, string> =
        try
            let settings = new ElasticsearchClientSettings(Uri(esUrl))
            let settingsWithIndex = settings.DefaultIndex("orders")
            let client = new ElasticsearchClient(settingsWithIndex)
            
            client.Indices.Delete("orders") |> ignore
            
            let indexSettings = """
{
  "settings": {
    "analysis": {
      "filter": {
        "my_synonyms": {
          "type": "synonym_graph",
          "synonyms": ["soda, pop => beverages", "sea food => seafood", "veggies => produce"]
        }
      },
      "analyzer": {
        "synonym_analyzer": {
          "tokenizer": "standard",
          "filter": ["lowercase", "my_synonyms"]
        }
      }
    }
  },
  "mappings": {
    "properties": {
      "product": {
        "properties": {
          "productName": {
            "type": "text",
            "analyzer": "synonym_analyzer",
            "fields": {
              "keyword": {
                "type": "keyword"
              }
            }
          }
        }
      }
    }
  }
}
"""
            client.Transport.Request<Elastic.Transport.StringResponse>(Elastic.Transport.HttpMethod.PUT, "orders", Elastic.Transport.PostData.String(indexSettings)) |> ignore

            
            let dtos = orders |> List.map toDto
            
            let chunkSize = 10000
            let chunks = dtos |> List.chunkBySize chunkSize

            let mutable success = true
            let mutable errorMsg = ""

            for chunk in chunks do
                if success then
                    let bulkResponse = 
                        Action<BulkRequestDescriptor>(fun b -> 
                            b.Index("orders").IndexMany(chunk) |> ignore
                        )
                        |> client.Bulk
                    
                    if not bulkResponse.IsValidResponse then
                        success <- false
                        errorMsg <- $"Bulk indexing failed: {bulkResponse.DebugInformation}"

            if success then
                Ok ()
            else
                Error errorMsg
        with
        | ex -> Error $"Elasticsearch connection error: {ex.Message}"
