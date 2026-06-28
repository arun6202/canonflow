namespace PlatformApi

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open SharedDomain.Dtos
open YamlDotNet.RepresentationModel

module Catalog =
    type CatalogError =
        | FileNotFound of string
        | ParseError of string
        | MissingRequired of path: string
        | InvalidExtension of path: string * message: string
        | NoUiFields of string

    type UiCapabilities = {
        SupportsTerms: bool
        SupportsPrefix: bool
        SupportsRange: bool
        SupportsMatch: bool
    }

    type ProviderExtension = {
        ClientField: string
        Ui: UiCapabilities
    }

    type SemanticField = {
        Name: string
        Label: string option
        Description: string option
        ProviderExtension: ProviderExtension option
    }

    type SemanticDataset = {
        Name: string
        Source: string option
        Fields: SemanticField list
    }

    type SemanticRelationship = {
        Name: string
        From: string
        To: string
    }

    type SemanticMetric = {
        Name: string
        Description: string option
    }

    type SemanticModel = {
        Name: string
        Description: string option
        Datasets: SemanticDataset list
        Relationships: SemanticRelationship list
        Metrics: SemanticMetric list
    }

    type SemanticDocument = {
        Models: SemanticModel list
    }

    let private errorText = function
        | FileNotFound path -> $"File not found: {path}"
        | ParseError message -> $"YAML parse failed: {message}"
        | MissingRequired path -> $"Missing required value: {path}"
        | InvalidExtension (path, message) -> $"Invalid extension at {path}: {message}"
        | NoUiFields path -> $"No UI-capable fields found in semantic model: {path}"

    let private combineResults (results: Result<'a, CatalogError list> list) =
        let errors =
            results
            |> List.collect (function | Error e -> e | Ok _ -> [])

        if not errors.IsEmpty then
            Error errors
        else
            results
            |> List.choose (function | Ok value -> Some value | Error _ -> None)
            |> Ok

    let private scalarValue (node: YamlNode) =
        match node with
        | :? YamlScalarNode as scalar -> Option.ofObj scalar.Value
        | _ -> None

    let private tryMap (node: YamlNode) =
        match node with
        | :? YamlMappingNode as map -> Some map
        | _ -> None

    let private trySeq (node: YamlNode) =
        match node with
        | :? YamlSequenceNode as seq -> Some (seq.Children |> Seq.toList)
        | _ -> None

    let private key name =
        YamlScalarNode(name) :> YamlNode

    let private tryFind name (map: YamlMappingNode) =
        match map.Children.TryGetValue(key name) with
        | true, value -> Some value
        | _ -> None

    let private optionalString name (map: YamlMappingNode) =
        tryFind name map |> Option.bind scalarValue

    let private requiredString path name (map: YamlMappingNode) =
        match optionalString name map with
        | Some value when not (String.IsNullOrWhiteSpace value) -> Ok value
        | _ -> Error [ MissingRequired $"{path}.{name}" ]

    let private optionalSequence name (map: YamlMappingNode) =
        tryFind name map
        |> Option.bind trySeq
        |> Option.defaultValue []

    let private titleFromClientField clientField =
        match clientField with
        | "Country" -> "Customer Country"
        | "CustomerId" -> "Customer ID"
        | "ProductCategory" -> "Product Category"
        | "EmployeeLastName" -> "Employee Last Name"
        | "LineSales" -> "Line Sales"
        | "OrderId" -> "Order ID"
        | value -> Regex.Replace(value, "([a-z])([A-Z])", "$1 $2")

    let private inferFieldType clientField =
        match clientField with
        | "LineSales" -> "number"
        | _ -> "string"

    let private tryGetString (propertyName: string) (element: JsonElement) =
        match element.TryGetProperty(propertyName) with
        | true, value when value.ValueKind = JsonValueKind.String -> Some (value.GetString())
        | _ -> None

    let private tryGetBool (propertyName: string) (element: JsonElement) =
        match element.TryGetProperty(propertyName) with
        | true, value when value.ValueKind = JsonValueKind.True -> Some true
        | true, value when value.ValueKind = JsonValueKind.False -> Some false
        | _ -> None

    let private parseProviderExtension (path: string) (json: string) =
        try
            use doc = JsonDocument.Parse(json)
            let root = doc.RootElement

            match tryGetString "client_field" root, root.TryGetProperty("ui") with
            | Some clientField, (true, ui) ->
                Ok {
                    ClientField = clientField
                    Ui = {
                        SupportsTerms = tryGetBool "supports_terms" ui |> Option.defaultValue false
                        SupportsPrefix = tryGetBool "supports_prefix" ui |> Option.defaultValue false
                        SupportsRange = tryGetBool "supports_range" ui |> Option.defaultValue false
                        SupportsMatch = tryGetBool "supports_match" ui |> Option.defaultValue false
                    }
                }
            | _ ->
                Error [ InvalidExtension (path, "expected client_field and ui metadata") ]
        with ex ->
            Error [ InvalidExtension (path, ex.Message) ]

    let private parseCustomExtensions path (fieldMap: YamlMappingNode) =
        optionalSequence "custom_extensions" fieldMap
        |> List.choose tryMap
        |> List.choose (fun extensionMap ->
            match optionalString "vendor_name" extensionMap, optionalString "data" extensionMap with
            | Some "NORTHWIND_PLATFORM", Some data when data.Contains("\"client_field\"") -> Some data
            | _ -> None)
        |> List.map (parseProviderExtension path)
        |> combineResults
        |> Result.map List.tryHead

    let private parseField datasetPath index node =
        match tryMap node with
        | None -> Error [ MissingRequired $"{datasetPath}.fields[{index}]" ]
        | Some fieldMap ->
            let fieldPath = $"{datasetPath}.fields[{index}]"
            match requiredString fieldPath "name" fieldMap, parseCustomExtensions fieldPath fieldMap with
            | Ok name, Ok providerExtension ->
                Ok {
                    Name = name
                    Label = optionalString "label" fieldMap
                    Description = optionalString "description" fieldMap
                    ProviderExtension = providerExtension
                }
            | nameResult, extensionResult ->
                [
                    match nameResult with | Error e -> yield! e | Ok _ -> ()
                    match extensionResult with | Error e -> yield! e | Ok _ -> ()
                ]
                |> Error

    let private parseDataset modelPath index node =
        match tryMap node with
        | None -> Error [ MissingRequired $"{modelPath}.datasets[{index}]" ]
        | Some datasetMap ->
            let datasetPath = $"{modelPath}.datasets[{index}]"
            let fields =
                optionalSequence "fields" datasetMap
                |> List.mapi (parseField datasetPath)
                |> combineResults

            match requiredString datasetPath "name" datasetMap, fields with
            | Ok name, Ok parsedFields ->
                Ok {
                    Name = name
                    Source = optionalString "source" datasetMap
                    Fields = parsedFields
                }
            | nameResult, fieldResult ->
                [
                    match nameResult with | Error e -> yield! e | Ok _ -> ()
                    match fieldResult with | Error e -> yield! e | Ok _ -> ()
                ]
                |> Error

    let private parseRelationship modelPath index node =
        match tryMap node with
        | None -> Error [ MissingRequired $"{modelPath}.relationships[{index}]" ]
        | Some relationshipMap ->
            let relationshipPath = $"{modelPath}.relationships[{index}]"

            match requiredString relationshipPath "name" relationshipMap,
                  requiredString relationshipPath "from" relationshipMap,
                  requiredString relationshipPath "to" relationshipMap with
            | Ok name, Ok fromDataset, Ok toDataset ->
                Ok {
                    Name = name
                    From = fromDataset
                    To = toDataset
                }
            | nameResult, fromResult, toResult ->
                [
                    match nameResult with | Error e -> yield! e | Ok _ -> ()
                    match fromResult with | Error e -> yield! e | Ok _ -> ()
                    match toResult with | Error e -> yield! e | Ok _ -> ()
                ]
                |> Error

    let private parseMetric modelPath index node =
        match tryMap node with
        | None -> Error [ MissingRequired $"{modelPath}.metrics[{index}]" ]
        | Some metricMap ->
            let metricPath = $"{modelPath}.metrics[{index}]"

            match requiredString metricPath "name" metricMap with
            | Ok name ->
                Ok {
                    Name = name
                    Description = optionalString "description" metricMap
                }
            | Error errors -> Error errors

    let private parseModel index node =
        match tryMap node with
        | None -> Error [ MissingRequired $"semantic_model[{index}]" ]
        | Some modelMap ->
            let modelPath = $"semantic_model[{index}]"
            let datasets =
                optionalSequence "datasets" modelMap
                |> List.mapi (parseDataset modelPath)
                |> combineResults

            let relationships =
                optionalSequence "relationships" modelMap
                |> List.mapi (parseRelationship modelPath)
                |> combineResults

            let metrics =
                optionalSequence "metrics" modelMap
                |> List.mapi (parseMetric modelPath)
                |> combineResults

            match requiredString modelPath "name" modelMap, datasets, relationships, metrics with
            | Ok name, Ok parsedDatasets, Ok parsedRelationships, Ok parsedMetrics ->
                Ok {
                    Name = name
                    Description = optionalString "description" modelMap
                    Datasets = parsedDatasets
                    Relationships = parsedRelationships
                    Metrics = parsedMetrics
                }
            | nameResult, datasetResult, relationshipResult, metricResult ->
                [
                    match nameResult with | Error e -> yield! e | Ok _ -> ()
                    match datasetResult with | Error e -> yield! e | Ok _ -> ()
                    match relationshipResult with | Error e -> yield! e | Ok _ -> ()
                    match metricResult with | Error e -> yield! e | Ok _ -> ()
                ]
                |> Error

    let private parseDocumentRoot (root: YamlMappingNode) =
        let models =
            optionalSequence "semantic_model" root
            |> List.mapi parseModel
            |> combineResults

        match models with
        | Ok [] -> Error [ MissingRequired "semantic_model" ]
        | Ok parsedModels -> Ok { Models = parsedModels }
        | Error errors -> Error errors

    let loadSemanticDocument path =
        if not (File.Exists path) then
            Error [ FileNotFound path ]
        else
            try
                use reader = File.OpenText(path)
                let stream = YamlStream()
                stream.Load(reader)

                match stream.Documents |> Seq.tryHead with
                | None -> Error [ ParseError "document is empty" ]
                | Some document ->
                    match tryMap document.RootNode with
                    | Some root -> parseDocumentRoot root
                    | None -> Error [ ParseError "root node must be a mapping" ]
            with ex ->
                Error [ ParseError ex.Message ]

    let private uiFields (document: SemanticDocument) =
        document.Models
        |> List.collect _.Datasets
        |> List.collect _.Fields
        |> List.choose (fun field ->
            field.ProviderExtension
            |> Option.map (fun extension -> field, extension))

    let private toSchemaField (field: SemanticField, extension: ProviderExtension) =
        let displayName =
            field.Label
            |> Option.defaultWith (fun () -> titleFromClientField extension.ClientField)

        {
            Name = extension.ClientField
            DisplayName = displayName
            Type = inferFieldType extension.ClientField
            SupportsTerms = extension.Ui.SupportsTerms
            SupportsPrefix = extension.Ui.SupportsPrefix
            SupportsRange = extension.Ui.SupportsRange
            SupportsMatch = extension.Ui.SupportsMatch
        }

    let private semanticModelCandidates () =
        let cwd = Directory.GetCurrentDirectory()
        let baseDir = AppContext.BaseDirectory
        [
            Path.Combine(cwd, "semantic-models")
            Path.Combine(cwd, "..", "semantic-models")
            Path.Combine(baseDir, "semantic-models")
            Path.Combine(baseDir, "..", "..", "..", "..", "semantic-models")
        ]
        |> List.map Path.GetFullPath
        |> List.distinct

    let private tryFindSemanticModelsDir () =
        semanticModelCandidates ()
        |> List.tryFind Directory.Exists

    let private modelFileName domainId =
        match domainId with
        | "Northwind" -> "northwind.osi.yaml"
        | "AdventureWorks" -> "adventureworks.osi.yaml"
        | other -> $"{other.ToLowerInvariant()}.osi.yaml"

    let tryLoadDomainConfig domainId displayName =
        match tryFindSemanticModelsDir () with
        | None -> Error [ FileNotFound "semantic-models directory was not found." ]
        | Some dir ->
            let path = Path.Combine(dir, modelFileName domainId)

            loadSemanticDocument path
            |> Result.bind (fun document ->
                let fields = uiFields document

                if fields.IsEmpty then
                    Error [ NoUiFields path ]
                else
                    let ordered =
                        fields
                        |> List.distinctBy (fun (_, extension) -> extension.ClientField)
                        |> List.sortBy (fun (_, extension) -> extension.ClientField)
                        |> List.map toSchemaField

                    Ok {
                        DomainId = domainId
                        DisplayName = displayName
                        Fields = ordered
                    })

    let loadDomainConfigOrFallback domainId displayName fallback =
        match tryLoadDomainConfig domainId displayName with
        | Ok config -> config
        | Error _ -> fallback

    let explainLoadDomainConfig domainId displayName =
        match tryLoadDomainConfig domainId displayName with
        | Ok config -> Ok config
        | Error errors ->
            errors
            |> List.map errorText
            |> String.concat "; "
            |> Error

    let validateDomain domainId =
        match tryFindSemanticModelsDir () with
        | None -> Error "semantic-models directory was not found."
        | Some dir ->
            let path = Path.Combine(dir, modelFileName domainId)

            match loadSemanticDocument path with
            | Error errors ->
                errors
                |> List.map errorText
                |> String.concat "; "
                |> Error
            | Ok document ->
                let datasetCount =
                    document.Models
                    |> List.sumBy (fun model -> model.Datasets.Length)

                let fieldCount =
                    document.Models
                    |> List.collect _.Datasets
                    |> List.sumBy (fun dataset -> dataset.Fields.Length)

                let uiFieldCount =
                    uiFields document
                    |> List.length

                let relationshipCount =
                    document.Models
                    |> List.sumBy (fun model -> model.Relationships.Length)

                let metricCount =
                    document.Models
                    |> List.sumBy (fun model -> model.Metrics.Length)

                Ok {|
                    DomainId = domainId
                    ModelCount = document.Models.Length
                    DatasetCount = datasetCount
                    FieldCount = fieldCount
                    UiFieldCount = uiFieldCount
                    RelationshipCount = relationshipCount
                    MetricCount = metricCount
                |}
