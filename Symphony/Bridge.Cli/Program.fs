module Symphony.Bridge.Cli.Program

open System
open System.Globalization
open System.IO
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open DuckDB.NET.Data
open Symphony.Bridge.Folds
open Symphony.Bridge.Spec

let private sourceDatabasePath =
    Path.GetFullPath("references/helios/northwind.db")

let private outputDirectory =
    Path.GetFullPath("Symphony/output")

let private bulkChunkByteLimit =
    50L * 1024L * 1024L

let buildSpec () =
    let orderId = Column.create<obj, int64> "OrderID"
    let orderDate = Column.create<obj, DateTime> "OrderDate"
    let customerId = Column.create<obj, string> "CustomerID"
    let companyName = Column.create<obj, string> "CompanyName"
    let contactName = Column.create<obj, string> "ContactName"
    let country = Column.create<obj, string> "Country"
    let employeeId = Column.create<obj, int64> "EmployeeID"
    let firstName = Column.create<obj, string> "FirstName"
    let lastName = Column.create<obj, string> "LastName"
    let title = Column.create<obj, string> "Title"
    let productId = Column.create<obj, int64> "ProductID"
    let productName = Column.create<obj, string> "ProductName"
    let categoryName = Column.create<obj, string> "CategoryName"
    let unitPrice = Column.create<obj, decimal> "UnitPrice"
    let quantity = Column.create<obj, int64> "Quantity"
    let discount = Column.create<obj, double> "Discount"

    let fields = [
        SpecBuilder.mapField "id" Keyword (Expr.rawWithDepNames "OrderID || ':' || ProductID" [ "OrderID"; "ProductID" ]) true
        SpecBuilder.mapField "orderId" Keyword (Expr.col orderId) true
        SpecBuilder.mapField "orderDate" Date (Expr.col orderDate) false
        SpecBuilder.mapField "customer.customerId" Keyword (Expr.col customerId) true
        SpecBuilder.mapField "customer.companyName" Text (Expr.col companyName) false
        SpecBuilder.mapField "customer.contactName" Text (Expr.col contactName) false
        SpecBuilder.mapField "customer.country" Keyword (Expr.col country) false
        SpecBuilder.mapField "employee.employeeId" Long (Expr.col employeeId) false
        SpecBuilder.mapField "employee.firstName" Text (Expr.col firstName) false
        SpecBuilder.mapField "employee.lastName" Text (Expr.col lastName) false
        SpecBuilder.mapField "employee.title" Text (Expr.col title) false
        SpecBuilder.mapField "product.productId" Long (Expr.col productId) true
        SpecBuilder.mapField "product.productName" Text (Expr.col productName) false
        SpecBuilder.mapField "product.categoryName" Keyword (Expr.col categoryName) false
        SpecBuilder.mapField "unitPrice" Double (Expr.col unitPrice) true
        SpecBuilder.mapField "quantity" Long (Expr.col quantity) true
        SpecBuilder.mapField "discount" Double (Expr.col discount) true
        SpecBuilder.mapField "lineSales" Double (Expr.rawWithDepNames "UnitPrice * Quantity * (1 - Discount)" [ "UnitPrice"; "Quantity"; "Discount" ]) true
    ]

    { Source = "SQLite.Northwind.OrderLine"
      Index = "northwind_order_lines_alias"
      Key = [ "orderId"; "product.productId" ]
      Fields = fields
      Detected = [] }

let private readStringOrNull (reader: System.Data.Common.DbDataReader) ordinal =
    if reader.IsDBNull ordinal then null else reader.GetString ordinal

let private readDateOrNull (reader: System.Data.Common.DbDataReader) ordinal =
    if reader.IsDBNull ordinal then
        null
    else
        JsonValue.Create(reader.GetDateTime ordinal) :> JsonNode

let private readInt64 (reader: System.Data.Common.DbDataReader) ordinal =
    Convert.ToInt64(reader.GetValue ordinal, CultureInfo.InvariantCulture)

let private readDouble (reader: System.Data.Common.DbDataReader) ordinal =
    Convert.ToDouble(reader.GetValue ordinal, CultureInfo.InvariantCulture)

let private addNestedObject (doc: JsonObject) name values =
    let child = JsonObject()

    values
    |> List.iter (fun (field, value: JsonNode) -> child.Add(field, value))

    doc.Add(name, child)

let extractOrderLines () =
    use conn = new DuckDBConnection("Data Source=:memory:")
    conn.Open()

    use cmdInit = conn.CreateCommand()
    cmdInit.CommandText <- "INSTALL sqlite; LOAD sqlite;"
    cmdInit.ExecuteNonQuery() |> ignore

    use cmdAttach = conn.CreateCommand()
    let escapedSourceDatabasePath = sourceDatabasePath.Replace("'", "''")
    cmdAttach.CommandText <- $"ATTACH '{escapedSourceDatabasePath}' AS sqlite_db (TYPE SQLITE);"
    cmdAttach.ExecuteNonQuery() |> ignore

    use cmd = conn.CreateCommand()
    cmd.CommandText <-
        """
        SELECT
            o.OrderID,
            o.OrderDate,
            c.CustomerID,
            c.CompanyName,
            c.ContactName,
            c.Country,
            e.EmployeeID,
            e.FirstName,
            e.LastName,
            e.Title,
            p.ProductID,
            p.ProductName,
            cat.CategoryName,
            od.UnitPrice,
            od.Quantity,
            od.Discount,
            od.UnitPrice * od.Quantity * (1 - od.Discount) AS LineSales
        FROM sqlite_db.Orders o
        JOIN sqlite_db.Customers c ON o.CustomerID = c.CustomerID
        LEFT JOIN sqlite_db.Employees e ON o.EmployeeID = e.EmployeeID
        JOIN sqlite_db."Order Details" od ON o.OrderID = od.OrderID
        JOIN sqlite_db.Products p ON od.ProductID = p.ProductID
        LEFT JOIN sqlite_db.Categories cat ON p.CategoryID = cat.CategoryID
        ORDER BY o.OrderID, p.ProductID
        """

    use reader = cmd.ExecuteReader()

    [
        while reader.Read() do
            let orderId = readInt64 reader 0
            let productId = readInt64 reader 10
            let docId = $"{orderId}:{productId}"
            let doc = JsonObject()

            doc.Add("id", JsonValue.Create(docId))
            doc.Add("orderId", JsonValue.Create(string orderId))
            doc.Add("orderDate", readDateOrNull reader 1)

            addNestedObject
                doc
                "customer"
                [ "customerId", JsonValue.Create(readStringOrNull reader 2)
                  "companyName", JsonValue.Create(readStringOrNull reader 3)
                  "contactName", JsonValue.Create(readStringOrNull reader 4)
                  "country", JsonValue.Create(readStringOrNull reader 5) ]

            addNestedObject
                doc
                "employee"
                [ "employeeId", JsonValue.Create(readInt64 reader 6)
                  "firstName", JsonValue.Create(readStringOrNull reader 7)
                  "lastName", JsonValue.Create(readStringOrNull reader 8)
                  "title", JsonValue.Create(readStringOrNull reader 9) ]

            addNestedObject
                doc
                "product"
                [ "productId", JsonValue.Create(productId)
                  "productName", JsonValue.Create(readStringOrNull reader 11)
                  "categoryName", JsonValue.Create(readStringOrNull reader 12) ]

            doc.Add("unitPrice", JsonValue.Create(readDouble reader 13))
            doc.Add("quantity", JsonValue.Create(readInt64 reader 14))
            doc.Add("discount", JsonValue.Create(readDouble reader 15))
            doc.Add("lineSales", JsonValue.Create(readDouble reader 16))

            doc
    ]

let writeBulkFiles (spec: TableSpec) (docs: JsonObject list) =
    let legacyBulkPath = Path.Combine(outputDirectory, "bulk.ndjson")

    if File.Exists legacyBulkPath then
        File.Delete legacyBulkPath

    let bulkDirectory = Path.Combine(outputDirectory, "bulk")
    Directory.CreateDirectory(bulkDirectory) |> ignore

    Directory.EnumerateFiles(bulkDirectory, "*.ndjson")
    |> Seq.iter File.Delete

    let utf8 = UTF8Encoding(false)
    let chunks = ResizeArray<string>()
    let mutable chunkIndex = 0
    let mutable currentBytes = 0L
    let mutable writer: StreamWriter = null

    let openNextChunk () =
        if not (isNull writer) then
            writer.Dispose()

        chunkIndex <- chunkIndex + 1
        currentBytes <- 0L

        let chunkPath = Path.Combine(bulkDirectory, sprintf "order-lines-%04i.ndjson" chunkIndex)
        chunks.Add(chunkPath)
        writer <- new StreamWriter(chunkPath, false, utf8)

    let lineBytes (line: string) =
        int64 (utf8.GetByteCount(line) + utf8.GetByteCount(writer.NewLine))

    let writePair action source =
        let pairBytes = lineBytes action + lineBytes source

        if currentBytes > 0L && currentBytes + pairBytes > bulkChunkByteLimit then
            openNextChunk ()

        writer.WriteLine(action)
        writer.WriteLine(source)
        currentBytes <- currentBytes + pairBytes

    openNextChunk ()

    try
        docs
        |> List.iter (fun doc ->
            let index = JsonObject()
            index.Add("_index", JsonValue.Create(spec.Index))
            index.Add("_id", JsonValue.Create(doc["id"].GetValue<string>()))

            let action = JsonObject()
            action.Add("index", index)

            writePair (action.ToJsonString()) (doc.ToJsonString()))
    finally
        if not (isNull writer) then
            writer.Dispose()

    let chunkSummaries =
        chunks
        |> Seq.map (fun path ->
            let file = FileInfo(path)
            let item = JsonObject()
            item.Add("path", JsonValue.Create(Path.GetRelativePath(outputDirectory, file.FullName)))
            item.Add("bytes", JsonValue.Create(file.Length))
            item)

    let manifest = JsonObject()
    manifest.Add("alias", JsonValue.Create(spec.Index))
    manifest.Add("documents", JsonValue.Create(docs.Length))
    manifest.Add("maxChunkBytes", JsonValue.Create(bulkChunkByteLimit))
    manifest.Add("chunks", JsonArray(chunkSummaries |> Seq.cast<JsonNode> |> Seq.toArray))

    File.WriteAllText(
        Path.Combine(outputDirectory, "bulk-manifest.json"),
        manifest.ToJsonString(JsonSerializerOptions(WriteIndented = true))
    )

    chunks |> Seq.toList

[<EntryPoint>]
let main _ =
    let spec = buildSpec()
    Directory.CreateDirectory(outputDirectory) |> ignore

    let esMapping = CompileEs.compileMapping spec
    File.WriteAllText(Path.Combine(outputDirectory, "mapping.json"), esMapping)
    printfn "Wrote mapping.json"

    let okf = CompileOkf.compileBundle spec
    File.WriteAllText(Path.Combine(outputDirectory, "catalog.md"), okf)
    printfn "Wrote catalog.md"

    let docs = extractOrderLines ()
    let bulkFiles = writeBulkFiles spec docs
    printfn "Wrote %d order-line documents to %d bulk chunks" docs.Length bulkFiles.Length

    let openMetadataContext: CompileOpenMetadata.BundleContext =
        { OutputDirectory = outputDirectory
          DocumentCount = docs.Length
          BulkChunkCount = bulkFiles.Length
          MaxChunkBytes = bulkChunkByteLimit }

    match CompileOpenMetadata.emitBundle openMetadataContext spec with
    | Ok files -> printfn "Wrote %d OpenMetadata draft artifacts" files.Length
    | Error message -> failwith message

    0
