namespace DataSync.Data

open Microsoft.Data.Sqlite
open System
open DataSync.Domain.SimpleTypes
open DataSync.Domain.CompoundTypes

module DataAccess =

    // DTO for raw data directly from SQLite boundary
    type private RawOrderRow = {
        OrderId: int
        OrderDate: string
        CustomerId: string
        CompanyName: string
        ContactName: string
        Country: string
        EmployeeId: int
        FirstName: string
        LastName: string
        Title: string
        ProductId: int
        UnitPrice: decimal
        Quantity: int
        Discount: decimal
        ProductName: string
        CategoryName: string
    }

    // Helper to safely execute database reads and validate edge constraints
    let getOrders (connectionString: string) : Result<OrderLineDocument list, string> =
        try
            use conn = new SqliteConnection(connectionString)
            conn.Open()

            let query = @"
                SELECT 
                    o.OrderID, o.OrderDate,
                    c.CustomerID, c.CompanyName, c.ContactName, c.Country,
                    e.EmployeeID, e.FirstName, e.LastName, e.Title,
                    od.ProductID, od.UnitPrice, od.Quantity, od.Discount,
                    p.ProductName,
                    cat.CategoryName
                FROM Orders o
                JOIN Customers c ON o.CustomerID = c.CustomerID
                JOIN Employees e ON o.EmployeeID = e.EmployeeID
                JOIN ""Order Details"" od ON o.OrderID = od.OrderID
                JOIN Products p ON od.ProductID = p.ProductID
                JOIN Categories cat ON p.CategoryID = cat.CategoryID
            "
            use cmd = new SqliteCommand(query, conn)
            use reader = cmd.ExecuteReader()

            let readRawRow () =
                if reader.Read() then
                    Some {
                        OrderId = Convert.ToInt32(reader.["OrderID"])
                        OrderDate = if reader.IsDBNull(reader.GetOrdinal("OrderDate")) then "" else Convert.ToDateTime(reader.["OrderDate"]).ToString("yyyy-MM-dd")
                        CustomerId = reader.["CustomerID"].ToString()
                        CompanyName = reader.["CompanyName"].ToString()
                        ContactName = reader.["ContactName"].ToString()
                        Country = reader.["Country"].ToString()
                        EmployeeId = Convert.ToInt32(reader.["EmployeeID"])
                        FirstName = reader.["FirstName"].ToString()
                        LastName = reader.["LastName"].ToString()
                        Title = reader.["Title"].ToString()
                        ProductId = Convert.ToInt32(reader.["ProductID"])
                        UnitPrice = Convert.ToDecimal(reader.["UnitPrice"])
                        Quantity = Convert.ToInt32(reader.["Quantity"])
                        Discount = Convert.ToDecimal(reader.["Discount"])
                        ProductName = reader.["ProductName"].ToString()
                        CategoryName = reader.["CategoryName"].ToString()
                    }
                else
                    None

            // Edge validation: map DTO to Domain.
            let validateRow (raw: RawOrderRow) : Result<_, string> =
                match OrderId.create raw.OrderId, 
                      CustomerId.create raw.CustomerId, 
                      EmployeeId.create raw.EmployeeId, 
                      ProductId.create raw.ProductId with
                | Ok oId, Ok cId, Ok eId, Ok pId -> Ok (oId, cId, eId, pId, raw)
                | Error e, _, _, _ -> Error $"Order {raw.OrderId} validation failed: {e}"
                | _, Error e, _, _ -> Error $"Order {raw.OrderId} validation failed: {e}"
                | _, _, Error e, _ -> Error $"Order {raw.OrderId} validation failed: {e}"
                | _, _, _, Error e -> Error $"Order {raw.OrderId} validation failed: {e}"

            // Read all rows into memory to avoid Sqlite enumeration issues during grouping
            let rawRows = Seq.unfold (fun () -> readRawRow () |> Option.map (fun r -> (r, ()))) () |> Seq.toList

            // Traverse list accumulating results or returning first error
            let validatedRowsResult =
                rawRows
                |> List.fold (fun acc row ->
                    match acc, validateRow row with
                    | Ok accList, Ok validRow -> Ok (validRow :: accList)
                    | Error e, _ -> Error e
                    | _, Error e -> Error e
                ) (Ok [])
                |> Result.map List.rev

            match validatedRowsResult with
            | Error e -> Error e
            | Ok validatedRows ->
                let documents = 
                    validatedRows
                    |> Seq.map (fun (oId, cId, eId, pId, r) ->
                        let lineSales = r.UnitPrice * decimal r.Quantity * (1m - r.Discount)
                        
                        { Id = $"{(OrderId.value oId)}_{ProductId.value pId}"
                          OrderId = OrderId.value oId
                          OrderDate = r.OrderDate
                          Customer = { CustomerId = cId; CompanyName = r.CompanyName; ContactName = r.ContactName; Country = r.Country }
                          Employee = { EmployeeId = eId; FirstName = r.FirstName; LastName = r.LastName; Title = r.Title }
                          Product = { ProductId = pId; ProductName = r.ProductName; CategoryName = r.CategoryName }
                          UnitPrice = r.UnitPrice
                          Quantity = r.Quantity
                          Discount = r.Discount
                          LineSales = Math.Round(lineSales, 2) }
                    )
                    |> Seq.toList
                Ok documents

        with
        | ex -> Error $"Database error: {ex.Message}"
