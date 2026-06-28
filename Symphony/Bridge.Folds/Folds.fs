namespace Symphony.Bridge.Folds

open Symphony.Bridge.Spec
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

    let compileMapping (spec: TableSpec) : string =
        let props = JsonObject()
        for f in spec.Fields do
            let fieldObj = JsonObject()
            fieldObj.Add("type", JsonValue.Create(mapEsType f.EsType))
            props.Add(f.Target, fieldObj)

        let mapping = JsonObject()
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
