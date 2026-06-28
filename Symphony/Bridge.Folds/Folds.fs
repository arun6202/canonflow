namespace Symphony.Bridge.Folds

open Symphony.Bridge.Spec
open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.Json.Nodes

module CompileEs =
    let private mapEsType (t: EsType) =
        match t with
        | Text -> "text"
        | Keyword -> "keyword"
        | Long -> "long"
        | Double -> "double"
        | Date -> "date"
        | Boolean -> "boolean"
        | Nested -> "nested"

    let private getOrAddObject (name: string) (parent: JsonObject) =
        match parent[name] with
        | null ->
            let child = JsonObject()
            parent.Add(name, child)
            child
        | node -> node.AsObject()

    let private getOrAddProperties (fieldObj: JsonObject) =
        match fieldObj["properties"] with
        | null ->
            let props = JsonObject()
            fieldObj.Add("dynamic", JsonValue.Create("strict"))
            fieldObj.Add("properties", props)
            props
        | node -> node.AsObject()

    let private leafMapping (t: EsType) =
        let fieldObj = JsonObject()
        fieldObj.Add("type", JsonValue.Create(mapEsType t))

        if t = Text then
            let fields = JsonObject()
            let keyword = JsonObject()
            keyword.Add("type", JsonValue.Create("keyword"))
            keyword.Add("ignore_above", JsonValue.Create(256))
            fields.Add("keyword", keyword)
            fieldObj.Add("fields", fields)

        fieldObj

    let private addFieldMapping (props: JsonObject) (field: FieldSpec<'row>) =
        let parts =
            field.Target.Split('.', System.StringSplitOptions.RemoveEmptyEntries)
            |> Array.toList

        let rec addAt (current: JsonObject) (remaining: string list) =
            match remaining with
            | [] -> ()
            | [ leaf ] -> current.Add(leaf, leafMapping field.EsType)
            | segment :: rest ->
                let childProps =
                    current
                    |> getOrAddObject segment
                    |> getOrAddProperties

                addAt childProps rest

        addAt props parts

    let compileMapping (spec: TableSpec) : string =
        let props = JsonObject()
        spec.Fields |> List.iter (addFieldMapping props)

        let mapping = JsonObject()
        mapping.Add("dynamic", JsonValue.Create("strict"))
        mapping.Add("properties", props)

        let root = JsonObject()
        root.Add("mappings", mapping)

        root.ToJsonString(JsonSerializerOptions(WriteIndented = true))

module LineageOf =
    let rec foldRaw (raw: Raw) : Lineage =
        match raw with
        | RCol n -> Exact (Set.singleton n)
        | RConcat(a, b) -> Lineage.combine (foldRaw a) (foldRaw b)
        | RApply(_, args) -> 
            args 
            |> List.map foldRaw 
            |> List.fold Lineage.combine (Exact Set.empty)
        | RLit _ -> Exact Set.empty
        | RRaw(_, lin) -> lin

    let fieldLineage (f: FieldSpec<'row>) : Lineage =
        foldRaw f.Expr

module CompileOkf =
    let compileBundle (spec: TableSpec) : string =
        let sb = System.Text.StringBuilder()
        sb.AppendLine($"# OKF Bundle for {spec.Index}") |> ignore
        sb.AppendLine($"Source: {spec.Source}") |> ignore
        sb.AppendLine("## Fields") |> ignore
        for f in spec.Fields do
            let linStr =
                match LineageOf.fieldLineage f with
                | Exact s -> $"Exact {s}"
                | Declared s -> $"Declared {s}"
                | Opaque -> "Opaque"
            sb.AppendLine($"- **{f.Target}**: {f.EsType} (Lineage: {linStr})") |> ignore
        sb.ToString()

module CompileOpenMetadata =
    type BundleContext =
        { OutputDirectory: string
          DocumentCount: int
          BulkChunkCount: int
          MaxChunkBytes: int64 }

    let private jsonOptions =
        JsonSerializerOptions(WriteIndented = true)

    let private addString (name: string) (value: string) (node: JsonObject) =
        node.Add(name, JsonValue.Create(value))
        node

    let private addNumber (name: string) (value: int) (node: JsonObject) =
        node.Add(name, JsonValue.Create(value))
        node

    let private addInt64 (name: string) (value: int64) (node: JsonObject) =
        node.Add(name, JsonValue.Create(value))
        node

    let private customProperties (properties: (string * string) list) =
        let node = JsonObject()

        properties
        |> List.iter (fun (name, value) -> node.Add(name, JsonValue.Create(value)))

        node

    let private ensureDirectory path =
        Directory.CreateDirectory(path) |> ignore

    let private writeJson outputDirectory relativePath (node: JsonObject) =
        let path = Path.Combine(outputDirectory, relativePath)
        let parent = Path.GetDirectoryName(path)

        if not (String.IsNullOrWhiteSpace(parent)) then
            ensureDirectory parent

        File.WriteAllText(path, node.ToJsonString(jsonOptions), Encoding.UTF8)
        path

    let private slug (value: string) =
        value.Replace(".", "-").Replace("_", "-").Replace(" ", "-").ToLowerInvariant()

    let private esTypeName = function
        | Text -> "text"
        | Keyword -> "keyword"
        | Long -> "long"
        | Double -> "double"
        | Date -> "date"
        | Boolean -> "boolean"
        | Nested -> "nested"

    let private lineageGrade = function
        | Exact _ -> "Exact"
        | Declared _ -> "Declared"
        | Opaque -> "Opaque"

    let private lineageColumns = function
        | Exact columns
        | Declared columns -> columns |> Seq.sort |> Seq.toArray
        | Opaque -> Array.empty

    let private specHash (spec: TableSpec) =
        let canonical =
            spec.Fields
            |> List.map (fun field ->
                let deps =
                    field.Lineage
                    |> lineageColumns
                    |> String.concat ","

                $"{field.Target}:{esTypeName field.EsType}:{lineageGrade field.Lineage}:{deps}")
            |> String.concat "|"

        use sha = SHA256.Create()
        sha.ComputeHash(Encoding.UTF8.GetBytes(canonical))
        |> Array.map (fun b -> b.ToString("x2"))
        |> String.concat ""

    let private lineageCoverage (spec: TableSpec) =
        let total = spec.Fields.Length

        let count predicate =
            spec.Fields
            |> List.filter (fun field -> predicate field.Lineage)
            |> List.length

        let exact = count (function Exact _ -> true | _ -> false)
        let declared = count (function Declared _ -> true | _ -> false)
        let opaque = count (function Opaque -> true | _ -> false)

        let node = JsonObject()
        node.Add("totalFields", JsonValue.Create(total))
        node.Add("exactFields", JsonValue.Create(exact))
        node.Add("declaredFields", JsonValue.Create(declared))
        node.Add("opaqueFields", JsonValue.Create(opaque))
        node

    let private entity entityType name fullyQualifiedName description properties =
        JsonObject()
        |> addString "entityType" entityType
        |> addString "name" name
        |> addString "fullyQualifiedName" fullyQualifiedName
        |> addString "description" description
        |> fun node ->
            node.Add("version", JsonValue.Create("0.1.0"))
            node.Add("customProperties", properties)
            node

    let private desiredTest name description target expectation =
        JsonObject()
        |> addString "name" name
        |> addString "description" description
        |> addString "target" target
        |> addString "expectation" expectation

    let private sourceTables () =
        [ "Orders", [ "OrderID"; "OrderDate"; "CustomerID"; "EmployeeID" ]
          "Order Details", [ "OrderID"; "ProductID"; "UnitPrice"; "Quantity"; "Discount" ]
          "Customers", [ "CustomerID"; "CompanyName"; "ContactName"; "Country" ]
          "Employees", [ "EmployeeID"; "FirstName"; "LastName"; "Title" ]
          "Products", [ "ProductID"; "ProductName"; "CategoryID" ]
          "Categories", [ "CategoryID"; "CategoryName" ] ]

    let private tableArtifact hash (tableName, columns) =
        let columnsJson =
            columns
            |> List.map (fun column ->
                JsonObject()
                |> addString "name" column
                |> addString "fullyQualifiedName" $"sqlite.northwind.main.{tableName}.{column}"
                |> addString "description" $"Source column {column}.")
            |> List.map (fun node -> node :> JsonNode)
            |> List.toArray

        entity
            "Table"
            tableName
            $"sqlite.northwind.main.{tableName}"
            $"Northwind source table {tableName}."
            (customProperties [
                "symphony.specHash", hash
                "symphony.sourceDialect", "SQLite"
            ])
        |> fun node ->
            node.Add("columns", JsonArray(columnsJson))
            node

    let private searchIndexArtifact (spec: TableSpec) hash ctx =
        let fields =
            spec.Fields
            |> List.map (fun field ->
                JsonObject()
                |> addString "name" field.Target
                |> addString "dataType" (esTypeName field.EsType))
            |> List.map (fun node -> node :> JsonNode)
            |> List.toArray

        entity
            "SearchIndex"
            spec.Index
            $"elasticsearch.local.{spec.Index}"
            "Elasticsearch serving alias for the Northwind order-line projection."
            (customProperties [
                "symphony.specHash", hash
                "symphony.targetDialect", "Elasticsearch 8/9"
                "symphony.documentGrain", "order-line"
                "symphony.indexAlias", spec.Index
                "symphony.concreteIndex", "validation/generated at load time"
            ])
        |> fun node ->
            node.Add("fields", JsonArray(fields))
            node.Add("lineageCoverage", lineageCoverage spec)
            node.Add("documentCount", JsonValue.Create(ctx.DocumentCount))
            node.Add("bulkChunkCount", JsonValue.Create(ctx.BulkChunkCount))
            node

    let private pipelineArtifact (spec: TableSpec) hash ctx =
        let tasks =
            [ "harvest"; "project"; "compile-elasticsearch"; "write-bulk"; "validate" ]
            |> List.map (fun name ->
                JsonObject()
                |> addString "name" name
                |> addString "fullyQualifiedName" $"pipeline.northwind-order-lines.{name}")
            |> List.map (fun node -> node :> JsonNode)
            |> List.toArray

        entity
            "Pipeline"
            "northwind-order-lines-projection"
            "pipeline.northwind-order-lines-projection"
            "Batch projection from Northwind source tables to the Elasticsearch order-line alias."
            (customProperties [
                "symphony.specHash", hash
                "symphony.sourceDialect", "SQLite/DuckDB"
                "symphony.targetDialect", "Elasticsearch 8/9"
                "symphony.bulkChunks", string ctx.BulkChunkCount
            ])
        |> fun node ->
            node.Add("source", JsonValue.Create(spec.Source))
            node.Add("target", JsonValue.Create($"elasticsearch.local.{spec.Index}"))
            node.Add("tasks", JsonArray(tasks))
            node.Add("documentCount", JsonValue.Create(ctx.DocumentCount))
            node.Add("maxChunkBytes", JsonValue.Create(ctx.MaxChunkBytes))
            node

    let private lineageArtifact (spec: TableSpec) hash =
        let edges =
            spec.Fields
            |> List.collect (fun field ->
                field.Lineage
                |> lineageColumns
                |> Array.map (fun sourceColumn ->
                    let edge = JsonObject()
                    edge.Add("from", JsonValue.Create($"sqlite.northwind.{sourceColumn}"))
                    edge.Add("to", JsonValue.Create($"elasticsearch.local.{spec.Index}.{field.Target}"))
                    edge.Add("grade", JsonValue.Create(lineageGrade field.Lineage))
                    edge :> JsonNode)
                |> Array.toList)

        entity
            "Lineage"
            "northwind-order-lines-lineage"
            "lineage.northwind-order-lines"
            "Constructed field-level lineage from Northwind source columns to Elasticsearch fields."
            (customProperties [
                "symphony.specHash", hash
                "symphony.lineageSource", "constructed"
            ])
        |> fun node ->
            node.Add("edges", JsonArray(edges |> List.toArray))
            node.Add("coverage", lineageCoverage spec)
            node

    let private qualityArtifact (spec: TableSpec) hash =
        let tests =
            [ desiredTest "id-is-unique" "Each Elasticsearch document id is unique." $"{spec.Index}.id" "unique"
              desiredTest "order-id-required" "Order id is required." $"{spec.Index}.orderId" "not-null"
              desiredTest "product-id-required" "Product id is required." $"{spec.Index}.product.productId" "not-null"
              desiredTest "unit-price-non-negative" "Unit price must be non-negative." $"{spec.Index}.unitPrice" ">= 0"
              desiredTest "quantity-positive" "Quantity must be positive." $"{spec.Index}.quantity" "> 0"
              desiredTest "discount-range" "Discount must be between zero and one." $"{spec.Index}.discount" "0 <= value <= 1"
              desiredTest "line-sales-non-negative" "Line sales must be non-negative." $"{spec.Index}.lineSales" ">= 0"
              desiredTest "strict-mapping-rejects-unknown" "Strict mapping rejects unknown fields." spec.Index "dynamic strict"
              desiredTest "source-target-count-match" "Elasticsearch document count equals source projection count." spec.Index "count match" ]
            |> List.map (fun node -> node :> JsonNode)
            |> List.toArray

        entity
            "TestSuite"
            "northwind-order-lines-suite"
            "quality.northwind-order-lines-suite"
            "Desired quality tests for the Northwind order-line Elasticsearch projection."
            (customProperties [
                "symphony.specHash", hash
                "symphony.resultsPolicy", "desired-tests-only"
            ])
        |> fun node ->
            node.Add("tests", JsonArray(tests))
            node

    let private contractArtifact (spec: TableSpec) hash =
        let requiredFields =
            spec.Fields
            |> List.filter _.Required
            |> List.map (fun field -> JsonValue.Create(field.Target) :> JsonNode)
            |> List.toArray

        entity
            "DataContract"
            "northwind-order-lines-contract"
            "contract.northwind-order-lines"
            "Serving contract for the Northwind order-line Elasticsearch alias."
            (customProperties [
                "symphony.specHash", hash
                "symphony.documentGrain", "order-line"
                "symphony.indexAlias", spec.Index
                "symphony.mappingStrictness", "dynamic strict"
                "symphony.compatibility", "Elasticsearch 8.x and 9.x"
                "symphony.lineageExpectation", "no Opaque serving fields"
            ])
        |> fun node ->
            node.Add("searchIndex", JsonValue.Create($"elasticsearch.local.{spec.Index}"))
            node.Add("requiredFields", JsonArray(requiredFields))
            node.Add("qualitySuite", JsonValue.Create("quality.northwind-order-lines-suite"))
            node

    let ensureNoOpaqueLineage (spec: TableSpec) =
        let opaqueFields =
            spec.Fields
            |> List.choose (fun field ->
                match field.Lineage with
                | Opaque -> Some field.Target
                | _ -> None)

        match opaqueFields with
        | [] -> Ok ()
        | fields ->
            let fieldList = String.concat ", " fields
            Error $"OpenMetadata MVP blocks Opaque serving lineage: {fieldList}"

    let emitBundle (ctx: BundleContext) (spec: TableSpec) =
        match ensureNoOpaqueLineage spec with
        | Error message -> Error message
        | Ok () ->
            let outputDirectory = Path.Combine(ctx.OutputDirectory, "openmetadata")
            let hash = specHash spec

            if Directory.Exists(outputDirectory) then
                Directory.Delete(outputDirectory, true)

            ensureDirectory outputDirectory

            let written =
                [ writeJson outputDirectory "database-service.json"
                    (entity
                        "DatabaseService"
                        "sqlite-local"
                        "sqlite.local"
                        "Local SQLite service used for the Northwind MVP."
                        (customProperties [ "symphony.specHash", hash; "symphony.sourceDialect", "SQLite" ]))

                  writeJson outputDirectory "database.json"
                    (entity
                        "Database"
                        "northwind"
                        "sqlite.local.northwind"
                        "Northwind sample database."
                        (customProperties [ "symphony.specHash", hash ]))

                  writeJson outputDirectory "schema.json"
                    (entity
                        "DatabaseSchema"
                        "main"
                        "sqlite.local.northwind.main"
                        "SQLite main schema for Northwind."
                        (customProperties [ "symphony.specHash", hash ]))

                  yield!
                      sourceTables ()
                      |> List.map (fun (tableName, columns) ->
                          writeJson outputDirectory $"tables/{slug tableName}.json" (tableArtifact hash (tableName, columns)))

                  writeJson outputDirectory "search/northwind-order-lines-alias.json" (searchIndexArtifact spec hash ctx)
                  writeJson outputDirectory "pipelines/northwind-order-lines-projection.json" (pipelineArtifact spec hash ctx)
                  writeJson outputDirectory "lineage/northwind-order-lines-lineage.json" (lineageArtifact spec hash)
                  writeJson outputDirectory "quality/northwind-order-lines-suite.json" (qualityArtifact spec hash)
                  writeJson outputDirectory "contracts/northwind-order-lines-contract.json" (contractArtifact spec hash) ]

            Ok written
