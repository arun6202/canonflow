namespace SharedDomain

// ----------------------------------------------------
// DTOs (Data Transfer Objects)
// ----------------------------------------------------
module Dtos =
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

    type AggregationBucketDto = { Key: string; Revenue: float }
    type ErrorResponseDto = { Message: string }

    type SchemaField = {
        Name: string
        DisplayName: string
        Type: string
        SupportsTerms: bool
        SupportsPrefix: bool
        SupportsRange: bool
        SupportsMatch: bool
    }

    type DomainConfig = {
        DomainId: string
        DisplayName: string
        Fields: SchemaField list
    }

    // Custom DSL AST Types (Strongly Typed Frontend JSON)
    type ClientPredicate =
        | Term of field: string * value: string
        | Terms of field: string * values: string list
        | Match of field: string * value: string
        | Prefix of field: string * value: string
        | Range of field: string * min: float option * max: float option
        | And of ClientPredicate list
        | Or of ClientPredicate list
        | Not of ClientPredicate

    type ClientAggregation =
        | Terms of name: string * field: string * size: int
        | Sum of name: string * field: string

    type AnalyticsRequestDto = {
        Filter: ClientPredicate option
        Aggregations: ClientAggregation list
    }

    type BucketDto = {
        Key: string
        DocCount: int64
        SubValue: float option
    }

    type AnalyticsResponseDto = {
        AggName: string
        Buckets: BucketDto list
    }

    type FinalCustomerAnalysisDto = {
        CustomerId: string
        TotalOrders: int
        TotalSales: decimal
        AvgOrderValue: decimal
        LastOrderDate: string
        MostRecentHandledBy: string
        TopProductCategoryPurchased: string
        DaysSinceLastOrder: int
        SalesRankWithinCountry: int
        CustomerSegment: string
        CustomerStatus: string
    }
