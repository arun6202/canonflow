namespace Symphony.Bridge.Spec

type Lineage =
    | Exact of Set<string>
    | Declared of Set<string>
    | Opaque

module Lineage =
    let combine x y =
        match x, y with
        | Opaque, _ | _, Opaque -> Opaque
        | Declared a, Declared b -> Declared (Set.union a b)
        | Declared a, Exact b
        | Exact a, Declared b -> Declared (Set.union a b)
        | Exact a, Exact b -> Exact (Set.union a b)

type Column<'row, 'value> = private Column of name:string

module Column =
    let create<'row, 'value> name : Column<'row, 'value> = Column name
    let name (Column n) = n

type Raw =
    | RCol of string
    | RConcat of Raw * Raw
    | RApply of fn:string * args:Raw list
    | RLit of obj
    | RRaw of sql:string * lineage:Lineage

type Expr<'row, 'value> = private Expr of Raw

module Expr =
    let col (c: Column<'row, 'value>) : Expr<'row, 'value> = Expr (RCol (Column.name c))
    
    let concat (Expr a: Expr<'row, string>) (Expr b: Expr<'row, string>) : Expr<'row, string> =
        Expr (RConcat(a, b))

    let lit (v: 'value) : Expr<'row, 'value> = Expr (RLit (box v))

    let rawWithDeps (sql: string) (cols: Column<'row, obj> list) : Expr<'row, 'value> =
        let deps = cols |> List.map Column.name |> Set.ofList
        Expr (RRaw(sql, Declared deps))

    let rawOpaque (sql: string) : Expr<'row, 'value> =
        Expr (RRaw(sql, Opaque))

    let toRaw (Expr r) = r

type EsType =
    | Text
    | Keyword
    | Long
    | Double
    | Date
    | Boolean
    | Nested

// Refined Types Architecture
type IPredicate<'T> =
    abstract member Check: 'T -> bool

[<Struct>]
type Refined<'T, 'P when 'P :> IPredicate<'T> and 'P : struct> = 
    private { Value: 'T }
    member this.Unwrap() = this.Value

module Refined =
    let create<'T, 'P when 'P :> IPredicate<'T> and 'P : struct> (value: 'T) : Result<Refined<'T, 'P>, string> =
        let p = Unchecked.defaultof<'P>
        if (box p :?> IPredicate<'T>).Check(value) then
            Ok ({ Value = value } : Refined<'T, 'P>)
        else
            Error $"Value %A{value} failed refinement predicate %s{typeof<'P>.Name}"

    let unsafeCreate<'T, 'P when 'P :> IPredicate<'T> and 'P : struct> (value: 'T) : Refined<'T, 'P> =
        { Value = value }

// Specific Northwind Constraints
[<Struct>]
type GreaterThanOrEqualToZero =
    interface IPredicate<decimal> with member _.Check(v) = v >= 0M
    interface IPredicate<float> with member _.Check(v) = v >= 0.0
    interface IPredicate<int> with member _.Check(v) = v >= 0
    interface IPredicate<int64> with member _.Check(v) = v >= 0L

[<Struct>]
type GreaterThanZero =
    interface IPredicate<decimal> with member _.Check(v) = v > 0M
    interface IPredicate<float> with member _.Check(v) = v > 0.0
    interface IPredicate<int> with member _.Check(v) = v > 0
    interface IPredicate<int64> with member _.Check(v) = v > 0L

[<Struct>]
type BetweenZeroAndOne =
    interface IPredicate<double> with member _.Check(v) = v >= 0.0 && v <= 1.0

[<Struct>]
type PrimaryKey =
    interface IPredicate<int64> with member _.Check(_) = true
    interface IPredicate<int> with member _.Check(_) = true
    interface IPredicate<string> with member _.Check(v) = not (System.String.IsNullOrWhiteSpace(v))
    interface IPredicate<System.Guid> with member _.Check(v) = v <> System.Guid.Empty


type RefineTag = string // Simplified for MVP

type Lossiness = Lossless | LossyPrecision | LossyTimezone | BlobRef

type FieldSpec<'row> =
    { Target   : string
      EsType   : EsType
      Expr     : Raw
      Lineage  : Lineage
      Required : bool
      Refine   : RefineTag list
      Loss     : Lossiness }

type KeySpec = string list

type DetectedConstraint = string list

type TableSpec =
    { Source   : string
      Index    : string
      Key      : KeySpec
      Fields   : FieldSpec<obj> list
      Detected : DetectedConstraint list }

module SpecBuilder =
    let mapField targetName esType (expr: Expr<'row, 'value>) required : FieldSpec<'row> =
        let rec lineageOf = function
            | RCol n -> Exact (Set.singleton n)
            | RConcat(a,b) -> Lineage.combine (lineageOf a) (lineageOf b)
            | RApply(_,xs) -> xs |> List.map lineageOf |> List.fold Lineage.combine (Exact Set.empty)
            | RLit _ -> Exact Set.empty
            | RRaw(_,decl) -> decl

        let raw = Expr.toRaw expr
        { Target = targetName
          EsType = esType
          Expr = raw
          Lineage = lineageOf raw
          Required = required
          Refine = []
          Loss = Lossless }
