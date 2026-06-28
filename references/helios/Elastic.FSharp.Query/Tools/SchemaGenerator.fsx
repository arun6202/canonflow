#r "nuget: System.Text.Json, 10.0.9"

open System
open System.IO
open System.Text.Json

let indexName = "Northwind"

let toPascalCase (str: string) =
    if String.IsNullOrEmpty(str) then str
    else
        let parts = str.Split('_')
        parts |> Array.map (fun p -> p.[0].ToString().ToUpper() + p.Substring(1)) |> String.concat ""

let generateAnalyzers (settingsJson: string) =
    use doc = JsonDocument.Parse(settingsJson)
    let root = doc.RootElement
    let mutable analyzers = []
    let mutable normalizers = []

    let hasIndex, index = root.TryGetProperty("index")
    if hasIndex then
        let hasAnalysis, analysis = index.TryGetProperty("analysis")
        if hasAnalysis then
            let hasAnalyzer, analyzerObj = analysis.TryGetProperty("analyzer")
            if hasAnalyzer then
                for prop in analyzerObj.EnumerateObject() do
                    analyzers <- prop.Name :: analyzers
            let hasNormalizer, normalizerObj = analysis.TryGetProperty("normalizer")
            if hasNormalizer then
                for prop in normalizerObj.EnumerateObject() do
                    normalizers <- prop.Name :: normalizers

    let emitDU name items =
        if List.isEmpty items then
            sprintf "    type %s = private | Dummy" name
        else
            let lines = items |> List.map (fun i -> sprintf "        | %s" (toPascalCase i))
            sprintf "    type %s =\n%s\n        member this.Value = match this with %s" 
                name 
                (String.concat "\n" lines)
                (items |> List.map (fun i -> sprintf "| %s -> \"%s\"" (toPascalCase i) i) |> String.concat " ")

    [emitDU "Analyzer" analyzers; emitDU "Normalizer" normalizers] |> String.concat "\n\n"


let rec parseProperties schemaName (props: JsonElement) prefix (sb: System.Text.StringBuilder) (nestedClasses: System.Text.StringBuilder) =
    for prop in props.EnumerateObject() do
        let name = prop.Name
        let pascalName = toPascalCase name
        let path = if prefix = "" then name else prefix + "." + name
        let hasType, typeProp = prop.Value.TryGetProperty("type")
        let t = if hasType then typeProp.GetString() else "object"

        match t with
        | "keyword" -> 
            sb.AppendLine(sprintf "    let %s : KeywordField<%s> = { Path = \"%s\"; Kind = Unchecked.defaultof<_> }" pascalName schemaName path) |> ignore
        | "text" -> 
            let hasFields, fieldsObj = prop.Value.TryGetProperty("fields")
            let hasKeyword = hasFields && (let h, _ = fieldsObj.TryGetProperty("keyword") in h)
            if hasKeyword then
                sb.AppendLine(sprintf "    let %s : TextWithKeywordField<%s> = { Path = \"%s\"; Kind = Unchecked.defaultof<_> }" pascalName schemaName path) |> ignore
            else
                sb.AppendLine(sprintf "    let %s : TextField<%s> = { Path = \"%s\"; Kind = Unchecked.defaultof<_> }" pascalName schemaName path) |> ignore
        | "date" -> 
            sb.AppendLine(sprintf "    let %s : DateField<%s> = { Path = \"%s\"; Kind = Unchecked.defaultof<_> }" pascalName schemaName path) |> ignore
        | "integer" | "long" | "float" | "double" -> 
            sb.AppendLine(sprintf "    let %s : NumericField<%s, unit> = { Path = \"%s\"; Kind = Unchecked.defaultof<_> }" pascalName schemaName path) |> ignore
        | "boolean" -> 
            sb.AppendLine(sprintf "    let %s : BoolField<%s> = { Path = \"%s\"; Kind = Unchecked.defaultof<_> }" pascalName schemaName path) |> ignore
        | "nested" ->
            let nestedSchemaName = pascalName + "Schema"
            nestedClasses.AppendLine(sprintf "    type %s = class end" nestedSchemaName) |> ignore
            sb.AppendLine(sprintf "    let %s : NestedField<%s, %s> = { Path = \"%s\"; Kind = Unchecked.defaultof<_> }" pascalName schemaName nestedSchemaName path) |> ignore
            let hasProps, propsObj = prop.Value.TryGetProperty("properties")
            if hasProps then
                parseProperties nestedSchemaName propsObj path sb nestedClasses
        | _ -> ()


let generateSchema (mappingJson: string) (settingsJson: string) =
    let sb = System.Text.StringBuilder()
    sb.AppendLine("namespace Elastic.FSharp.Query")
    sb.AppendLine("open Elastic.FSharp.Query.Types")
    sb.AppendLine("")
    sb.AppendLine(sprintf "module %sSchema =" indexName)
    sb.AppendLine(sprintf "    type %s = class end" indexName)
    sb.AppendLine("")
    
    sb.AppendLine(generateAnalyzers settingsJson)
    sb.AppendLine("")

    use doc = JsonDocument.Parse(mappingJson)
    let root = doc.RootElement
    let props = root.GetProperty("properties")
    
    let fieldsSb = System.Text.StringBuilder()
    let nestedSb = System.Text.StringBuilder()
    
    parseProperties indexName props "" fieldsSb nestedSb

    sb.Append(nestedSb.ToString()) |> ignore
    sb.AppendLine("")
    sb.Append(fieldsSb.ToString()) |> ignore
    
    sb.ToString()


let mapping = File.ReadAllText("mapping.json")
let settings = File.ReadAllText("settings.json")
let code = generateSchema mapping settings
File.WriteAllText("GeneratedSchema.fs", code)
printfn "Successfully generated Schema"
