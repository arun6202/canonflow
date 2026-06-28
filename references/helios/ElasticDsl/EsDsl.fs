namespace ElasticDsl

// ════════════════════════════════════════════════════════════════════════════
//  An elegant, composable read-only query DSL over Elasticsearch.
//
//  Design law:  Query<'doc> is a BOOLEAN ALGEBRA (bounded complemented lattice).
//               Core = { True, False, Leaf, Not, And, Or }.  Everything else
//               (XOR, between, nand, implication, ...) is DERIVED in-algebra,
//               so the ES compiler never grows a case for it.
//
//  Consumers see ONLY domain terms. The words "Elasticsearch", "_source",
//  "bool", "DSL" never cross the contract boundary.  Read-only by construction:
//  there is no write/update/delete node anywhere in the algebra.
// ════════════════════════════════════════════════════════════════════════════

module Json =
    type Json =
        | JNull
        | JBool   of bool
        | JNum    of decimal
        | JStr    of string
        | JArr    of Json list
        | JObj    of (string * Json) list

    let str   s = JStr s
    let num  (n: decimal) = JNum n
    let int' (i: int) = JNum (decimal i)
    let bool' b = JBool b
    let obj fields = JObj fields
    let arr xs = JArr xs

    let rec render =
        function
        | JNull   -> "null"
        | JBool b -> if b then "true" else "false"
        | JNum n  -> string n
        | JStr s  -> "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""
        | JArr xs -> "[" + String.concat "," (List.map render xs) + "]"
        | JObj fs -> "{" + String.concat "," [ for (k, v) in fs -> render (JStr k) + ":" + render v ] + "}"

// ── 1. Capability kinds — phantom typeclass-witnesses ───
type IExact    = interface end   
type IOrdered  = interface end   
type IFullText = interface end   

type Keyword = 
    inherit IExact                              
type Numeric = 
    inherit IExact
    inherit IOrdered         
type DateK   = 
    inherit IExact
    inherit IOrdered         
type TextK   = 
    inherit IFullText                           
type BoolK   = 
    inherit IExact

// ── 2. Leaf predicates + the closed Boolean algebra ─────────────────────────
open Json

type RangeBound =
    | Gt  of Json
    | Gte of Json
    | Lt  of Json
    | Lte of Json

type Leaf =
    | Term     of path: string * value: Json
    | Terms    of path: string * values: Json list
    | RangeL   of path: string * bounds: RangeBound list
    | MatchL   of path: string * text: string
    | PhraseL  of path: string * text: string
    | PrefixL  of path: string * prefix: string
    | WildcardL of path: string * pattern: string
    | ExistsL  of path: string

type QueryAst =
    | True
    | False
    | Leaf of Leaf
    | Not  of QueryAst
    | And  of QueryAst list
    | Or   of QueryAst list

type Query<'doc> = private Q of QueryAst

// ── 3. Smart constructors: normalize on construction ──────
module Query =
    let internal ast (Q a) = a
    let internal wrap a : Query<'doc> = Q a

    let andOf (qs: Query<'doc> list) : Query<'doc> =
        let flat =
            qs |> List.map ast
               |> List.collect (function And xs -> xs | x -> [ x ])     
        if flat |> List.contains False then wrap False                  
        else
            match flat |> List.filter (fun x -> x <> True) with         
            | []    -> wrap True
            | [ x ] -> wrap x
            | xs    -> wrap (And (List.distinct xs))                    

    let orOf (qs: Query<'doc> list) : Query<'doc> =
        let flat =
            qs |> List.map ast
               |> List.collect (function Or xs -> xs | x -> [ x ])
        if flat |> List.contains True then wrap True                    
        else
            match flat |> List.filter (fun x -> x <> False) with        
            | []    -> wrap False
            | [ x ] -> wrap x
            | xs    -> wrap (Or (List.distinct xs))

    let notOf (q: Query<'doc>) : Query<'doc> =
        match ast q with
        | True   -> wrap False
        | False  -> wrap True
        | Not x  -> wrap x
        | x      -> wrap (Not x)

    let tru<'doc> : Query<'doc> = wrap True      
    let fls<'doc> : Query<'doc> = wrap False      

    let andAlso a b = andOf [ a; b ]
    let orElse  a b = orOf  [ a; b ]
    let xor a b = orOf [ andOf [ a; notOf b ]; andOf [ notOf a; b ] ]
    let nand a b = notOf (andAlso a b)
    let implies a b = orElse (notOf a) b

// ── 4. Ergonomic operators + capability-typed leaf surface ───────────────────
module Dsl =
    let inline ( &&& ) a b = Query.andAlso a b
    let inline ( ||| ) a b = Query.orElse  a b
    let inline ( ^^^ ) a b = Query.xor     a b
    let inline ( !! ) q    = Query.notOf   q
    let all  () = Query.tru
    let none () = Query.fls

    type Field<'doc, 'v, 'k> =
        private { Path: string; Encode: 'v -> Json }

    module Field =
        let define<'doc, 'v, 'k> (path: string) (encode: 'v -> Json) : Field<'doc, 'v, 'k> =
            { Path = path; Encode = encode }

    let private leaf l : Query<'doc> = Query.wrap (Leaf l)

    let eq<'doc, 'v, 'k when 'k :> IExact> (f: Field<'doc, 'v, 'k>) (v: 'v) : Query<'doc> =
        leaf (Term (f.Path, f.Encode v))

    let oneOf<'doc, 'v, 'k when 'k :> IExact> (f: Field<'doc, 'v, 'k>) (vs: 'v list) : Query<'doc> =
        match vs with
        | []  -> Query.fls                                   
        | [v] -> eq f v
        | _   -> leaf (Terms (f.Path, List.map f.Encode vs))

    let gt<'doc, 'v, 'k when 'k :> IOrdered>  (f: Field<'doc, 'v, 'k>) (v: 'v) : Query<'doc> = leaf (RangeL (f.Path, [ Gt  (f.Encode v) ]))
    let gte<'doc, 'v, 'k when 'k :> IOrdered> (f: Field<'doc, 'v, 'k>) (v: 'v) : Query<'doc> = leaf (RangeL (f.Path, [ Gte (f.Encode v) ]))
    let lt<'doc, 'v, 'k when 'k :> IOrdered>  (f: Field<'doc, 'v, 'k>) (v: 'v) : Query<'doc> = leaf (RangeL (f.Path, [ Lt  (f.Encode v) ]))
    let lte<'doc, 'v, 'k when 'k :> IOrdered> (f: Field<'doc, 'v, 'k>) (v: 'v) : Query<'doc> = leaf (RangeL (f.Path, [ Lte (f.Encode v) ]))
    let between<'doc, 'v, 'k when 'k :> IOrdered> (f: Field<'doc, 'v, 'k>) (lo: 'v) (hi: 'v) : Query<'doc> =
        leaf (RangeL (f.Path, [ Gte (f.Encode lo); Lte (f.Encode hi) ]))

    let matches<'doc, 'k when 'k :> IFullText> (f: Field<'doc, string, 'k>) (s: string) : Query<'doc> = leaf (MatchL  (f.Path, s))
    let phrase<'doc, 'k when 'k :> IFullText>  (f: Field<'doc, string, 'k>) (s: string) : Query<'doc> = leaf (PhraseL (f.Path, s))
    let prefix<'doc, 'k when 'k :> IExact>     (f: Field<'doc, string, 'k>) (s: string) : Query<'doc> = leaf (PrefixL (f.Path, s))

    let exists<'doc, 'v, 'k> (f: Field<'doc, 'v, 'k>) : Query<'doc> = leaf (ExistsL f.Path)

// ── 5. The compiler (PURE / Planner): QueryAst → ES filter-context query ─────
module Compile =
    let private compileLeaf =
        function
        | Term (p, v)     -> obj [ "term",     obj [ p, obj [ "value", v ] ] ]
        | Terms (p, vs)   -> obj [ "terms",    obj [ p, arr vs ] ]
        | RangeL (p, bs)  ->
            let toField = function Gt v -> "gt", v | Gte v -> "gte", v | Lt v -> "lt", v | Lte v -> "lte", v
            obj [ "range", obj [ p, obj (List.map toField bs) ] ]
        | MatchL (p, t)   -> obj [ "match",    obj [ p, str t ] ]
        | PhraseL (p, t)  -> obj [ "match_phrase", obj [ p, str t ] ]
        | PrefixL (p, t)  -> obj [ "prefix",   obj [ p, obj [ "value", str t ] ] ]
        | WildcardL (p,t) -> obj [ "wildcard", obj [ p, obj [ "value", str t ] ] ]
        | ExistsL p       -> obj [ "exists",   obj [ "field", str p ] ]

    let rec private compile a =
        match a with
        | True   -> obj [ "match_all",  obj [] ]
        | False  -> obj [ "match_none", obj [] ]
        | Leaf l -> compileLeaf l
        | Not q  -> obj [ "bool", obj [ "must_not", arr [ compile q ] ] ]
        | And qs -> obj [ "bool", obj [ "filter",   arr (List.map compile qs) ] ]
        | Or  qs ->
            obj [ "bool", obj [ "should", arr (List.map compile qs)
                                "minimum_should_match", int' 1 ] ]

    let toSearchQuery (q: Query<'doc>) : Json.Json =
        obj [ "query", obj [ "bool", obj [ "filter", arr [ compile (Query.ast q) ] ] ] ]

// ── 6. Cursor algebra (PURE): search_after + PIT, never from/size ────────────
module Cursor =
    type SortKey = { Field: string; Desc: bool }

    type Cursor =
        { Pit: string
          PageSize: int
          Sort: SortKey list
          After: Json.Json list option }

    type Page<'doc> =
        { Hits: 'doc list
          Next: Cursor option }

    let private sortJson (ks: SortKey list) =
        arr [ for k in ks -> obj [ k.Field, str (if k.Desc then "desc" else "asc") ] ]

    let body (q: Query<'doc>) (c: Cursor) : Json.Json =
        let baseFields =
            [ match Compile.toSearchQuery q with Json.JObj fs -> yield! fs | x -> yield "query", x
              "size", Json.int' c.PageSize
              "sort", sortJson c.Sort
              "pit",  obj [ "id", str c.Pit; "keep_alive", str "1m" ]
              "track_total_hits", Json.bool' false ]
        let withAfter =
            match c.After with
            | Some keys -> baseFields @ [ "search_after", arr keys ]
            | None      -> baseFields
        obj withAfter

// ── 7. Telemetry (Writer) + Runner seam ────────────────────
module Telemetry =
    type Event =
        | Compiled of Json.Json
        | Executed of took_ms: int * hits: int
        | Advanced of pageSize: int * exhausted: bool

    type Writer<'a> = { Value: 'a; Log: Event list }
    let ret v = { Value = v; Log = [] }
    let tell e = { Value = (); Log = [ e ] }
    let bind f w = let r = f w.Value in { Value = r.Value; Log = w.Log @ r.Log }

type EsError =
    | Transport of string
    | BadResponse of string
    | Unauthorized

type RawHit = { Source: Json.Json; Sort: Json.Json list }
type RawResult = { TookMs: int; Total: int; Hits: RawHit list }

type ISearchPort =
    abstract Search : body: string -> Async<Result<string, EsError>>

// ── 8. Runner: pure plan → one effect → pure decode, with telemetry ──────────
module Runner =
    open Telemetry
    open System.Text.Json

    type Projection<'doc> = JsonElement -> Result<'doc, EsError>

    let fetchPage
        (port: ISearchPort)
        (decode: Projection<'doc>)
        (q: Query<'doc>)
        (c: Cursor.Cursor)
        : Async<Result<Cursor.Page<'doc> * Telemetry.Event list, EsError>> =
        async {
            let body = Cursor.body q c
            let log1 = [ Compiled body ]
            
            let jsonString = Json.render body

            match! port.Search jsonString with
            | Error e -> return Error e
            | Ok rawJson ->
                try
                    use doc = JsonDocument.Parse(rawJson)
                    let root = doc.RootElement
                    
                    let tookMs = match root.TryGetProperty("took") with true, prop -> prop.GetInt32() | false, _ -> 0
                    
                    let hitsObj = root.GetProperty("hits")
                    let total = match hitsObj.TryGetProperty("total") with true, prop -> prop.GetProperty("value").GetInt32() | false, _ -> 0
                    
                    let hitsArray = hitsObj.GetProperty("hits").EnumerateArray() |> Seq.toList
                    
                    let decoded = hitsArray |> List.map (fun h -> decode (h.GetProperty("_source")))
                    
                    match decoded |> List.tryPick (function Error e -> Some e | _ -> None) with
                    | Some e -> return Error e
                    | None ->
                        let docs = decoded |> List.choose (function Ok d -> Some d | _ -> None)
                        let exhausted = hitsArray.Length < c.PageSize
                        let next =
                            if exhausted then None
                            else
                                hitsArray
                                |> List.tryLast
                                |> Option.bind (fun last -> 
                                    match last.TryGetProperty("sort") with
                                    | true, _ -> Some { c with After = None }
                                    | false, _ -> None
                                )
                        let log = log1 @ [ Executed (tookMs, total)
                                           Advanced (c.PageSize, exhausted) ]
                        return Ok ({ Hits = docs; Next = next }, log)
                with e ->
                    return Error (BadResponse e.Message)
        }

// ── 9. Wire surface (PURE): remote JSON → Query<'doc> ─
module Wire =
    type FieldKind = KKeyword | KNumeric | KDate | KText | KBool
    type FieldSpec = { Path: string; Kind: FieldKind }
    type Contract = { Fields: Map<string, FieldSpec> }

    type ParseError =
        | UnknownField of string
        | CapabilityViolation of field: string * op: string
        | Malformed of string

    type V<'a> = Result<'a, ParseError list>
    
    let private both a b = match a, b with
                           | Ok x, Ok y -> Ok (x, y)
                           | Error e1, Error e2 -> Error (e1 @ e2)
                           | Error e, _ | _, Error e -> Error e

    let private capOk kind op =
        match kind, op with
        | (KKeyword | KNumeric | KDate | KBool), ("eq" | "oneOf" | "exists") -> true
        | (KNumeric | KDate), ("gt" | "gte" | "lt" | "lte" | "between")      -> true
        | KText, ("matches" | "phrase" | "exists")                           -> true
        | KKeyword, "prefix"                                                  -> true
        | _ -> false

    type DslCondition = {
        Field: string
        Value: string
        IsExactMatch: bool
    }

    type CustomDslQuery = {
        Operator: string
        Conditions: DslCondition[]
        Groups: CustomDslQuery[]
    }

    let rec buildFromAst<'doc> (c: Contract) (query: CustomDslQuery) : Result<Query<'doc>, ParseError list> =
        let parseCondition (cond: DslCondition) =
            match c.Fields.TryFind cond.Field with
            | Some spec ->
                let kind = spec.Kind
                let opName = if cond.IsExactMatch then "eq" else "matches"
                
                if not (capOk kind opName) then
                    Error [ CapabilityViolation (cond.Field, opName) ]
                else
                    // Map to the appropriate Query constructor
                    if cond.IsExactMatch then
                        Ok (Query.wrap (Leaf (Term (cond.Field, Json.str cond.Value))))
                    else
                        Ok (Query.wrap (Leaf (MatchL (cond.Field, cond.Value))))
            | None -> Error [ UnknownField cond.Field ]

        let conditionResults = query.Conditions |> Array.toList |> List.map parseCondition
        let groupResults = query.Groups |> Array.toList |> List.map (buildFromAst c)
        
        let allResults = conditionResults @ groupResults
        
        let errors = allResults |> List.choose (function Error e -> Some e | _ -> None) |> List.concat
        if not errors.IsEmpty then
            Error errors
        else
            let queries = allResults |> List.choose (function Ok q -> Some q | _ -> None)
            if query.Operator.ToUpper() = "OR" then
                Ok (Query.orOf queries)
            else
                Ok (Query.andOf queries)
