module App

open System
open Elmish
open Elmish.React
open Fable.React
open Fable.React.Props
open SharedDomain
open Dtos
open Thoth.Json
open Thoth.Fetch

let sharedExtra = Extra.empty |> Extra.withDecimal |> Extra.withInt64

// --- 1. UI State Representation ---

type RuleNode =
    | Condition of id: string * field: string * op: string * value: string
    | Group of id: string * logicalOp: string * children: RuleNode list

let rec compileToAST (node: RuleNode) : ClientPredicate option =
    match node with
    | Condition (_, field, op, value) ->
        if String.IsNullOrWhiteSpace(value) then None
        else
            match op with
            | "Term" -> Some (ClientPredicate.Term (field, value))
            | "Prefix" -> Some (ClientPredicate.Prefix (field, value))
            | "Match" -> Some (ClientPredicate.Match (field, value))
            | _ -> Some (ClientPredicate.Term (field, value))
    | Group (_, logicalOp, children) ->
        let compiledChildren = children |> List.choose compileToAST
        if compiledChildren.IsEmpty then None
        else
            match logicalOp with
            | "And" -> Some (ClientPredicate.And compiledChildren)
            | "Or" -> Some (ClientPredicate.Or compiledChildren)
            | "Not" -> 
                if compiledChildren.Length > 0 then Some (ClientPredicate.Not compiledChildren.Head)
                else None
            | _ -> Some (ClientPredicate.And compiledChildren)

type SearchState =
    | Idle
    | Loading
    | Success of OrderLineDocumentDto list
    | Failed of string

type AnalyticsState =
    | AIdle
    | ALoading
    | ASuccess of AnalyticsResponseDto list
    | AFailed of string

type Tab =
    | VisualBuilder
    | TextSearch
    | Analytics

type Model = {
    SelectedDomainId: string
    CurrentTab: Tab
    RootNode: RuleNode
    QueryResult: string
    SearchState: SearchState
    
    // Text Search State
    TextQuery: string
    TextParseError: string option
    
    // Analytics State
    AnalyticsField: string
    AnalyticsAggType: string
    AnalyticsState: AnalyticsState
    DomainConfig: Dtos.DomainConfig option
}

type Msg =
    | SetTab of Tab
    | ChangeDomain of string
    // Visual Builder
    | AddCondition of parentId: string
    | AddGroup of parentId: string
    | RemoveNode of id: string
    | UpdateConditionField of id: string * field: string
    | UpdateConditionOp of id: string * op: string
    | UpdateConditionValue of id: string * value: string
    | UpdateGroupLogicalOp of id: string * op: string
    | RunVisualQuery
    | SearchCompleted of Result<OrderLineDocumentDto list, exn>
    // Text Search
    | UpdateTextQuery of string
    | RunTextQuery
    // Analytics
    | UpdateAnalyticsField of string
    | UpdateAnalyticsAggType of string
    | RunAnalytics
    | AnalyticsCompleted of Result<AnalyticsResponseDto list, exn>
    // Schema
    | FetchSchema
    | SchemaLoaded of Result<Dtos.DomainConfig, exn>

let newId () = Guid.NewGuid().ToString()

let createCondition () = Condition (newId(), "Country", "Term", "")
let createGroup () = Group (newId(), "And", [createCondition ()])

let init () =
    let root = Group (newId(), "And", [createCondition ()])
    let initialModel = { 
        SelectedDomainId = "AdventureWorks"
        CurrentTab = VisualBuilder
        RootNode = root
        QueryResult = "Click 'Generate JSON Payload' to compile the tree."
        SearchState = Idle
        TextQuery = "Country:USA AND EmployeeLastName:Callahan"
        TextParseError = None
        AnalyticsField = "Country"
        AnalyticsAggType = "Terms"
        AnalyticsState = AIdle
        DomainConfig = None
    }
    initialModel, Cmd.ofMsg FetchSchema

// --- 2. Parsers & Mappers ---

let parseTextQuery (q: string) : Result<ClientPredicate, string> =
    if String.IsNullOrWhiteSpace(q) then Error "Query cannot be empty"
    else
        let parts = q.Split([|" AND "|], StringSplitOptions.RemoveEmptyEntries)
        let parsePart (p: string) =
            let p = p.Trim()
            let colonIdx = p.IndexOf(':')
            if colonIdx < 0 then Error $"Invalid syntax '{p}'. Expected 'field:value'."
            else
                let f = p.Substring(0, colonIdx).Trim()
                let v = p.Substring(colonIdx + 1).Trim()
                if f.Equals("LineSales", StringComparison.OrdinalIgnoreCase) then Error "Range parsing not supported in text parser MVP."
                else Ok (ClientPredicate.Term (f, v))
        
        let parsedParts = parts |> Array.map parsePart |> Array.toList
        let errors = parsedParts |> List.choose (function Error e -> Some e | _ -> None)
        if errors.Length > 0 then Error (String.Join(", ", errors))
        else
            let terms = parsedParts |> List.choose (function Ok t -> Some t | _ -> None)
            Ok (ClientPredicate.And terms)

let rec mapTree (id: string) (updater: RuleNode -> RuleNode) (node: RuleNode) =
    let isTarget = match node with Condition(i,_,_,_) -> i = id | Group(i,_,_) -> i = id
    if isTarget then updater node
    else
        match node with
        | Condition _ -> node
        | Group (gid, op, children) ->
            Group (gid, op, children |> List.map (mapTree id updater))

let rec removeNodeFromTree (id: string) (node: RuleNode) =
    match node with
    | Condition (cid, _, _, _) -> if cid = id then None else Some node
    | Group (gid, op, children) ->
        if gid = id then None
        else
            let newChildren = children |> List.choose (removeNodeFromTree id)
            Some (Group (gid, op, newChildren))

// --- 3. Update Loop ---

let update msg model =
    match msg with
    | FetchSchema ->
        let domain = model.SelectedDomainId
        let fetchCmd () = Fetch.get<unit, Dtos.DomainConfig>($"http://localhost:5004/api/schema/{domain}")
        let cmd = Cmd.OfPromise.either fetchCmd () (Ok >> SchemaLoaded) (Error >> SchemaLoaded)
        model, cmd
    | SchemaLoaded (Ok config) ->
        { model with DomainConfig = Some config }, Cmd.none
    | SchemaLoaded (Error e) ->
        { model with QueryResult = "Error loading schema: " + e.Message }, Cmd.none
    | ChangeDomain newDomain ->
        let root = Group (newId(), "And", [createCondition ()])
        let newModel = { model with SelectedDomainId = newDomain; RootNode = root; QueryResult = "Domain switched." }
        newModel, Cmd.ofMsg FetchSchema
    | SetTab t -> { model with CurrentTab = t }, Cmd.none

    | AddCondition parentId ->
        let updater n = match n with Group (id, op, children) -> Group (id, op, children @ [createCondition ()]) | _ -> n
        { model with RootNode = mapTree parentId updater model.RootNode }, Cmd.none
        
    | AddGroup parentId ->
        let updater n = match n with Group (id, op, children) -> Group (id, op, children @ [createGroup ()]) | _ -> n
        { model with RootNode = mapTree parentId updater model.RootNode }, Cmd.none
        
    | RemoveNode id ->
        match removeNodeFromTree id model.RootNode with
        | Some newRoot -> { model with RootNode = newRoot }, Cmd.none
        | None -> { model with RootNode = createGroup () }, Cmd.none

    | UpdateConditionField (id, f) ->
        let updater n = match n with Condition (i, _, op, v) -> Condition (i, f, op, v) | _ -> n
        { model with RootNode = mapTree id updater model.RootNode }, Cmd.none

    | UpdateConditionOp (id, o) ->
        let updater n = match n with Condition (i, f, _, v) -> Condition (i, f, o, v) | _ -> n
        { model with RootNode = mapTree id updater model.RootNode }, Cmd.none

    | UpdateConditionValue (id, v) ->
        let updater n = match n with Condition (i, f, op, _) -> Condition (i, f, op, v) | _ -> n
        { model with RootNode = mapTree id updater model.RootNode }, Cmd.none

    | UpdateGroupLogicalOp (id, o) ->
        let updater n = match n with Group (i, _, children) -> Group (i, o, children) | _ -> n
        { model with RootNode = mapTree id updater model.RootNode }, Cmd.none

    | RunVisualQuery ->
        match compileToAST model.RootNode with
        | Some ast ->
            let jsonStr = Encode.Auto.toString(4, value = ast)
            let fetchCmd () = Fetch.post<ClientPredicate, OrderLineDocumentDto list>($"http://localhost:5004/api/orders/custom-dsl?domain={model.SelectedDomainId}", ast, extra = sharedExtra)
            let cmd = Cmd.OfPromise.either fetchCmd () (Ok >> SearchCompleted) (Error >> SearchCompleted)
            { model with QueryResult = jsonStr; SearchState = Loading }, cmd
        | None ->
            { model with QueryResult = "Error: Tree is empty."; SearchState = Idle }, Cmd.none

    | SearchCompleted (Ok results) -> { model with SearchState = Success results }, Cmd.none
    | SearchCompleted (Error ex) -> { model with SearchState = Failed ex.Message }, Cmd.none

    | UpdateTextQuery q -> { model with TextQuery = q; TextParseError = None }, Cmd.none

    | RunTextQuery ->
        match parseTextQuery model.TextQuery with
        | Error err -> { model with TextParseError = Some err }, Cmd.none
        | Ok ast ->
            let jsonStr = Encode.Auto.toString(4, value = ast)
            let fetchCmd () = Fetch.post<ClientPredicate, OrderLineDocumentDto list>($"http://localhost:5004/api/orders/custom-dsl?domain={model.SelectedDomainId}", ast, extra = sharedExtra)
            let cmd = Cmd.OfPromise.either fetchCmd () (Ok >> SearchCompleted) (Error >> SearchCompleted)
            { model with QueryResult = jsonStr; SearchState = Loading; TextParseError = None }, cmd

    | UpdateAnalyticsField f -> { model with AnalyticsField = f }, Cmd.none
    | UpdateAnalyticsAggType t -> { model with AnalyticsAggType = t }, Cmd.none

    | RunAnalytics ->
        let agg = 
            match model.AnalyticsAggType with
            | "Terms" -> ClientAggregation.Terms ("myAgg", model.AnalyticsField, 10)
            | "Sum" -> ClientAggregation.Sum ("myAgg", model.AnalyticsField)
            | _ -> ClientAggregation.Terms ("myAgg", model.AnalyticsField, 10)
        
        let req : AnalyticsRequestDto = { Filter = None; Aggregations = [agg] }
        let fetchCmd () = Fetch.post<AnalyticsRequestDto, AnalyticsResponseDto list>($"http://localhost:5004/api/orders/analytics-dsl?domain={model.SelectedDomainId}", req, extra = sharedExtra)
        let cmd = Cmd.OfPromise.either fetchCmd () (Ok >> AnalyticsCompleted) (Error >> AnalyticsCompleted)
        { model with AnalyticsState = ALoading }, cmd

    | AnalyticsCompleted (Ok res) -> { model with AnalyticsState = ASuccess res }, Cmd.none
    | AnalyticsCompleted (Error ex) -> { model with AnalyticsState = AFailed ex.Message }, Cmd.none


// --- 4. View Logic ---

let renderDataTable searchState =
    match searchState with
    | Idle -> p [] [ str "Ready to search." ]
    | Loading -> div [ Style [ Color "#61dafb"; FontWeight "bold" ] ] [ str "Searching Elasticsearch..." ]
    | Failed e -> div [ Style [ Color "#d40032" ] ] [ str ("Error: " + e) ]
    | Success docs -> 
        div [] [
            h3 [ Style [ Color "#a6e22e" ] ] [ str (sprintf "Found %d Hits" docs.Length) ]
            if docs.Length > 0 then
                table [ Style [ Width "100%"; TextAlign TextAlignOptions.Left; BorderCollapse "collapse"; MarginTop "15px" ] ] [
                    thead [] [
                        tr [ Style [ BorderBottom "1px solid rgba(255,255,255,0.2)" ] ] [
                            th [ Style [ Padding "10px" ] ] [ str "Order ID" ]
                            th [ Style [ Padding "10px" ] ] [ str "Customer" ]
                            th [ Style [ Padding "10px" ] ] [ str "Country" ]
                            th [ Style [ Padding "10px" ] ] [ str "Product" ]
                            th [ Style [ Padding "10px" ] ] [ str "Sales ($)" ]
                        ]
                    ]
                    tbody [] (
                        docs |> List.truncate 50 |> List.map (fun doc ->
                            tr [ Style [ BorderBottom "1px solid rgba(255,255,255,0.05)" ] ] [
                                td [ Style [ Padding "10px" ] ] [ str (doc.OrderId.ToString()) ]
                                td [ Style [ Padding "10px" ] ] [ str doc.Customer.CompanyName ]
                                td [ Style [ Padding "10px" ] ] [ str doc.Customer.Country ]
                                td [ Style [ Padding "10px" ] ] [ str doc.Product.ProductName ]
                                td [ Style [ Padding "10px"; Color "#61dafb"; FontWeight "bold" ] ] [ str (doc.LineSales.ToString("F2")) ]
                            ]
                        )
                    )
                ]
        ]

let rec viewNode (model: Model) (node: RuleNode) dispatch =
    match node with
    | Condition (id, field, op, value) ->
        div [ ClassName "rule-row" ] [
            select [ OnChange (fun e -> dispatch (UpdateConditionField (id, e.Value))); Value field ] [
                match model.DomainConfig with
                | Some config ->
                    for f in config.Fields do
                        option [ Value f.Name ] [ str f.DisplayName ]
                | None -> option [ Value "Loading..." ] [ str "Loading Schema..." ]
            ]
            select [ OnChange (fun e -> dispatch (UpdateConditionOp (id, e.Value))); Value op ] [
                option [ Value "Term" ] [ str "Equals (Term)" ]
                option [ Value "Prefix" ] [ str "Starts With (Prefix)" ]
                option [ Value "Match" ] [ str "Full Text (Match)" ]
            ]
            input [ Type "text"; Placeholder "Enter value..."; Value value; OnChange (fun e -> dispatch (UpdateConditionValue (id, e.Value))) ]
            button [ ClassName "btn-danger"; OnClick (fun _ -> dispatch (RemoveNode id)); Title "Remove Rule" ] [ str "✕" ]
        ]
    | Group (id, logicalOp, children) ->
        div [ ClassName "glass-card group-container"; Style [ MarginBottom "15px" ] ] [
            div [ Style [ Display DisplayOptions.Flex; AlignItems AlignItemsOptions.Center; MarginBottom "15px" ] ] [
                select [ OnChange (fun e -> dispatch (UpdateGroupLogicalOp (id, e.Value))); Value logicalOp; Style [ FontWeight "800"; Color "#61dafb"; MarginRight "15px"; Background "rgba(97, 218, 251, 0.1)"; BorderColor "rgba(97, 218, 251, 0.3)" ] ] [
                    option [ Value "And" ] [ str "AND" ]
                    option [ Value "Or" ] [ str "OR" ]
                    option [ Value "Not" ] [ str "NOT" ]
                ]
                button [ ClassName "btn-secondary"; OnClick (fun _ -> dispatch (AddCondition id)); Style [ MarginRight "10px" ] ] [ str "+ Rule" ]
                button [ ClassName "btn-secondary"; OnClick (fun _ -> dispatch (AddGroup id)); Style [ MarginRight "auto" ] ] [ str "+ Group" ]
                button [ ClassName "btn-danger"; OnClick (fun _ -> dispatch (RemoveNode id)) ] [ str "✕" ]
            ]
            div [ ClassName "rule-children" ] (children |> List.map (fun c -> viewNode model c dispatch))
        ]

let view model dispatch =
    div [ Style [ MaxWidth "900px"; Margin "0 auto" ] ] [
        
        // Header & Domain Picker
        div [ Style [ Display DisplayOptions.Flex; JustifyContent "space-between"; AlignItems AlignItemsOptions.Center; MarginBottom "20px" ] ] [
            h1 [ Style [ Margin "0"; Color "#fff"; TextShadow "0 0 20px rgba(97, 218, 251, 0.5)" ] ] [ str "SOTA Search Platform" ]
            
            div [ Style [ Display DisplayOptions.Flex; AlignItems AlignItemsOptions.Center ] ] [
                span [ Style [ Color "#aaa"; MarginRight "10px"; FontWeight "bold"; FontSize "14px" ] ] [ str "Dataset Engine:" ]
                select [ 
                    OnChange (fun e -> dispatch (ChangeDomain e.Value))
                    Value model.SelectedDomainId
                    Style [ Padding "8px 12px"; BorderRadius "6px"; Background "rgba(255,255,255,0.1)"; Color "#fff"; Border "1px solid rgba(255,255,255,0.2)"; FontWeight "bold"; Outline "none"; Cursor "pointer"; FontSize "14px" ]
                ] [
                    option [ Value "AdventureWorks"; Style [ Color "#000" ] ] [ str "AdventureWorks (SQLite)" ]
                    option [ Value "Northwind"; Style [ Color "#000" ] ] [ str "Northwind (Elasticsearch)" ]
                ]
            ]
        ]
        
        // Tab Bar
        div [ Style [ Display DisplayOptions.Flex; MarginBottom "20px"; BorderBottom "2px solid rgba(255,255,255,0.1)" ] ] [
            let tabStyle isActive = 
                [ Padding "10px 20px"; Cursor "pointer"; FontWeight "bold"; 
                  Color (if isActive then "#61dafb" else "#aaa")
                  BorderBottom (if isActive then "3px solid #61dafb" else "3px solid transparent") ]
            
            div [ OnClick (fun _ -> dispatch (SetTab VisualBuilder)); Style (tabStyle (model.CurrentTab = VisualBuilder)) ] [ str "Visual Builder" ]
            div [ OnClick (fun _ -> dispatch (SetTab TextSearch)); Style (tabStyle (model.CurrentTab = TextSearch)) ] [ str "Text Search" ]
            div [ OnClick (fun _ -> dispatch (SetTab Analytics)); Style (tabStyle (model.CurrentTab = Analytics)) ] [ str "Analytics" ]
        ]

        // Tab Content
        match model.CurrentTab with
        | VisualBuilder ->
            div [] [
                div [ Style [ Display DisplayOptions.Flex; JustifyContent "flex-end"; MarginBottom "20px" ] ] [
                    button [ OnClick (fun _ -> dispatch RunVisualQuery); Style [ Padding "12px 24px"; FontSize "16px"; BoxShadow "0 4px 15px rgba(0, 120, 212, 0.4)" ] ] [ str "Execute Visual Query" ]
                ]
                viewNode model model.RootNode dispatch
                div [ ClassName "glass-card"; Style [ MarginTop "30px" ] ] [ renderDataTable model.SearchState ]
                div [ ClassName "glass-card"; Style [ MarginTop "30px" ] ] [
                    h3 [ Style [ MarginTop "0"; Color "#61dafb" ] ] [ str "Compiled AST Payload:" ]
                    pre [ Style [ Margin "0"; MaxHeight "200px"; Color "#a6e22e"; FontSize "14px"; FontFamily "Consolas, monospace"; OverflowY OverflowOptions.Auto ] ] [ str model.QueryResult ]
                ]
            ]
        | TextSearch ->
            div [] [
                div [ ClassName "glass-card" ] [
                    h3 [ Style [ MarginTop "0" ] ] [ str "Google-Style Text Parser" ]
                    p [ Style [ Color "#aaa" ] ] [ str "Try typing: country:USA AND lastName:Callahan" ]
                    input [ Type "text"; Value model.TextQuery; OnChange (fun e -> dispatch (UpdateTextQuery e.Value)); Style [ Width "100%"; Padding "15px"; FontSize "18px"; Background "rgba(255,255,255,0.1)"; Color "#fff"; Border "1px solid rgba(255,255,255,0.2)"; BorderRadius "8px" ] ]
                    
                    match model.TextParseError with
                    | Some err -> p [ Style [ Color "#d40032"; FontWeight "bold" ] ] [ str err ]
                    | None -> null

                    button [ OnClick (fun _ -> dispatch RunTextQuery); Style [ MarginTop "20px"; Padding "12px 24px"; FontSize "16px"; BoxShadow "0 4px 15px rgba(0, 120, 212, 0.4)" ] ] [ str "Parse & Execute" ]
                ]
                div [ ClassName "glass-card"; Style [ MarginTop "30px" ] ] [ renderDataTable model.SearchState ]
            ]
        | Analytics ->
            div [] [
                div [ ClassName "glass-card" ] [
                    h3 [ Style [ MarginTop "0" ] ] [ str "Dynamic Analytics Builder" ]
                    div [ ClassName "rule-row" ] [
                        select [ OnChange (fun e -> dispatch (UpdateAnalyticsAggType e.Value)); Value model.AnalyticsAggType ] [
                            option [ Value "Terms" ] [ str "Group By (Terms)" ]
                            option [ Value "Sum" ] [ str "Total Metric (Sum)" ]
                        ]
                        select [ OnChange (fun e -> dispatch (UpdateAnalyticsField e.Value)); Value model.AnalyticsField ] [
                            match model.DomainConfig with
                            | Some config ->
                                for f in config.Fields do
                                    option [ Value f.Name ] [ str f.DisplayName ]
                            | None -> option [ Value "Loading..." ] [ str "Loading Schema..." ]
                        ]
                    ]
                    button [ OnClick (fun _ -> dispatch RunAnalytics); Style [ MarginTop "20px"; Padding "12px 24px"; FontSize "16px"; BoxShadow "0 4px 15px rgba(0, 120, 212, 0.4)" ] ] [ str "Run Analytics" ]
                ]

                div [ ClassName "glass-card"; Style [ MarginTop "30px" ] ] [
                    match model.AnalyticsState with
                    | AIdle -> p [] [ str "Ready for BI query." ]
                    | ALoading -> div [ Style [ Color "#61dafb"; FontWeight "bold" ] ] [ str "Crunching data..." ]
                    | AFailed e -> div [ Style [ Color "#d40032" ] ] [ str ("Error: " + e) ]
                    | ASuccess res ->
                        div [] (
                            h3 [ Style [ Color "#a6e22e" ] ] [ str "Business Intelligence Results" ]
                            :: (res |> List.map (fun aggRes ->
                                let chartData = 
                                    aggRes.Buckets 
                                    |> List.map (fun b -> 
                                        {| name = b.Key; value = match b.SubValue with Some v -> v | None -> float b.DocCount |}
                                    ) |> List.toArray

                                div [ Style [ MarginBottom "20px" ] ] [
                                    h4 [ Style [ Color "#61dafb"; BorderBottom "1px solid rgba(255,255,255,0.2)"; PaddingBottom "10px" ] ] [ str aggRes.AggName ]
                                    
                                    // BI Chart
                                    div [ ClassName "recharts-wrapper"; Style [ MarginTop "20px"; MarginBottom "20px"; Height "300px" ] ] [
                                        Recharts.ResponsiveContainer {| width = "100%"; height = "100%" |} [
                                            Recharts.BarChart {| data = chartData; margin = {| top = 20; right = 30; left = 20; bottom = 5 |} |} [
                                                Recharts.CartesianGrid {| strokeDasharray = "3 3"; stroke = "rgba(255,255,255,0.1)" |}
                                                Recharts.XAxis {| dataKey = "name"; stroke = "#a0a0a0" |}
                                                Recharts.YAxis {| stroke = "#a0a0a0" |}
                                                Recharts.Tooltip {| contentStyle = {| backgroundColor = "rgba(0,0,0,0.8)"; border = "1px solid #333"; borderRadius = "4px"; color = "#fff" |} |}
                                                Recharts.Legend {| |}
                                                Recharts.Bar {| dataKey = "value"; fill = "#61dafb"; radius = [| 4; 4; 0; 0 |] |}
                                            ]
                                        ]
                                    ]

                                    // BI Table
                                    table [ Style [ Width "100%"; TextAlign TextAlignOptions.Left; BorderCollapse "collapse" ] ] [
                                        thead [] [
                                            tr [ Style [ BorderBottom "1px solid rgba(255,255,255,0.1)" ] ] [
                                                th [ Style [ Padding "10px" ] ] [ str "Bucket Key" ]
                                                th [ Style [ Padding "10px" ] ] [ str "Value" ]
                                            ]
                                        ]
                                        tbody [] (
                                            aggRes.Buckets |> List.map (fun b ->
                                                tr [ Style [ BorderBottom "1px solid rgba(255,255,255,0.05)" ] ] [
                                                    td [ Style [ Padding "10px" ] ] [ str b.Key ]
                                                    td [ Style [ Padding "10px"; Color "#61dafb"; FontWeight "bold" ] ] [ 
                                                        match b.SubValue with
                                                        | Some v -> str (sprintf "%.2f" v)
                                                        | None -> str (b.DocCount.ToString() + " docs")
                                                    ]
                                                ]
                                            )
                                        )
                                    ]
                                ]
                            ))
                        )
                ]
            ]
    ]

Program.mkProgram init update view
|> Program.withReactSynchronous "elmish-app"
|> Program.run
