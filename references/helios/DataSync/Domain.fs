namespace DataSync.Domain

module SimpleTypes =
    open System

    type OrderId = private OrderId of int
    module OrderId =
        let create i = if i > 0 then Ok (OrderId i) else Error "OrderId must be positive"
        let value (OrderId i) = i

    type CustomerId = private CustomerId of string
    module CustomerId =
        let create (s: string) = 
            if String.IsNullOrWhiteSpace(s) then Error "CustomerId cannot be empty" 
            else Ok (CustomerId (s.Trim()))
        let value (CustomerId s) = s

    type EmployeeId = private EmployeeId of int
    module EmployeeId =
        let create i = if i > 0 then Ok (EmployeeId i) else Error "EmployeeId must be positive"
        let value (EmployeeId i) = i

    type ProductId = private ProductId of int
    module ProductId =
        let create i = if i > 0 then Ok (ProductId i) else Error "ProductId must be positive"
        let value (ProductId i) = i

module CompoundTypes =
    open SimpleTypes

    type CustomerInfo = {
        CustomerId: CustomerId
        CompanyName: string
        ContactName: string
        Country: string
    }

    type EmployeeInfo = {
        EmployeeId: EmployeeId
        FirstName: string
        LastName: string
        Title: string
    }

    type ProductInfo = {
        ProductId: ProductId
        ProductName: string
        CategoryName: string
    }

    type OrderLine = {
        Product: ProductInfo
        UnitPrice: decimal
        Quantity: int
        Discount: decimal
    }

    // The root document type that will be serialized to JSON and sent to Elasticsearch.
    // It's part of the public boundary of the domain.
    // Remodeled to represent an Order Line to match the analytics grain.
    type OrderLineDocument = {
        Id: string
        OrderId: int
        OrderDate: string
        Customer: CustomerInfo
        Employee: EmployeeInfo
        Product: ProductInfo
        UnitPrice: decimal
        Quantity: int
        Discount: decimal
        LineSales: decimal
    }
