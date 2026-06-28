module Symphony.Bridge.Cli.Program

open System
open System.IO
open System.Text.Json
open System.Text.Json.Nodes
open Symphony.Bridge.Spec
open Symphony.Bridge.Folds
open Symphony.Northwind.main
open SqlHydra.Query

let buildSpec () =
    let cId = Column.create<obj, string> "CustomerID"
    let cName = Column.create<obj, string> "CompanyName"
    let cCountry = Column.create<obj, string> "Country"
    let tSales = Column.create<obj, float> "TotalSales"
    let tOrders = Column.create<obj, int> "TotalOrders"
    
    let fields = [
        SpecBuilder.mapField "customerId" Keyword (Expr.col cId) true
        SpecBuilder.mapField "customerName" Text (Expr.col cName) true
        SpecBuilder.mapField "country" Keyword (Expr.col cCountry) false
        SpecBuilder.mapField "totalSales" Double (Expr.col tSales) true
        SpecBuilder.mapField "totalOrders" Long (Expr.col tOrders) true
        SpecBuilder.mapField "customerSegment" Keyword (Expr.rawOpaque "CASE WHEN TotalSales > 10000 THEN 'Gold' ELSE 'Silver' END") true
    ]

    { Source = "SQLite.CustomersWithSales"
      Index = "customer_analytics_alias"
      Key = ["customerId"]
      Fields = fields
      Detected = [] }

let extractData () =
    use conn = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=E:/github/symphony/references/helios/northwind.db")
    conn.Open()
    use cmd = conn.CreateCommand()
    cmd.CommandText <- """
        SELECT 
            c.CustomerID, c.CompanyName, c.Country, 
            o.OrderID, od.UnitPrice, od.Quantity, od.Discount
        FROM Customers c
        JOIN Orders o ON c.CustomerID = o.CustomerID
        JOIN "Order Details" od ON o.OrderID = od.OrderID
    """
    use reader = cmd.ExecuteReader()
    
    let rawResults = ResizeArray()
    while reader.Read() do
        let cid = reader.GetString(0)
        let cname = if reader.IsDBNull(1) then "" else reader.GetString(1)
        let country = if reader.IsDBNull(2) then "" else reader.GetString(2)
        let oid = reader.GetInt32(3)
        let price = reader.GetDouble(4)
        let qty = reader.GetInt32(5)
        let discount = reader.GetDouble(6)
        rawResults.Add((cid, cname, country, oid, price, qty, discount))
        
    rawResults
    |> Seq.groupBy (fun (cid, cname, country, _, _, _, _) -> cid, cname, country)
    |> Seq.map (fun ((cid, cname, country), group) ->
        let totalOrders = group |> Seq.map (fun (_, _, _, oid, _, _, _) -> oid) |> Seq.distinct |> Seq.length
        let totalSales = 
            group 
            |> Seq.map (fun (_, _, _, _, price, qty, disc) -> price * float qty * (1.0 - disc)) 
            |> Seq.sum
            
        let doc = JsonObject()
        doc.Add("customerId", JsonValue.Create(cid))
        doc.Add("customerName", JsonValue.Create(cname))
        doc.Add("country", JsonValue.Create(country))
        doc.Add("totalOrders", JsonValue.Create(totalOrders))
        doc.Add("totalSales", JsonValue.Create(totalSales))
        doc.Add("customerSegment", JsonValue.Create(if totalSales > 10000.0 then "Gold" else "Silver"))
        doc
    )
    |> Seq.toList

[<EntryPoint>]
let main argv =
    let spec = buildSpec()
    
    let outDir = "E:/github/symphony/Symphony/output"
    Directory.CreateDirectory(outDir) |> ignore
    
    let esMapping = CompileEs.compileMapping spec
    File.WriteAllText(Path.Combine(outDir, "mapping.json"), esMapping)
    printfn "Wrote mapping.json"
    
    let okf = CompileOkf.compileBundle spec
    File.WriteAllText(Path.Combine(outDir, "catalog.md"), okf)
    printfn "Wrote catalog.md"
    
    let data = extractData()
    let ndjsonPath = Path.Combine(outDir, "bulk.ndjson")
    use writer = new StreamWriter(ndjsonPath)
    for doc in data do
        let indexOp = JsonObject()
        let idx = JsonObject()
        idx.Add("_index", JsonValue.Create(spec.Index))
        idx.Add("_id", JsonValue.Create(doc.["customerId"].GetValue<string>()))
        indexOp.Add("index", idx)
        
        writer.WriteLine(indexOp.ToJsonString())
        writer.WriteLine(doc.ToJsonString())
        
    printfn "Wrote %d documents to bulk.ndjson" data.Length
    
    0
