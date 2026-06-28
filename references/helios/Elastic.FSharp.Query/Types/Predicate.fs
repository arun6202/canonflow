namespace Elastic.FSharp.Query.Types

type MinShouldMatch =
    | Count   of int
    | Percent of int
    | Expr    of string

type ILowerVisitor =
    abstract member Visit<'A> : Predicate<'A> -> QueryContainer

and INestedLower =
    abstract member Accept : ILowerVisitor -> QueryContainer

and [<RequireQualifiedAccess>] Predicate<'S> =
    | Eq        of FieldRef<'S> * FieldValue
    | In        of FieldRef<'S> * FieldValue list
    | Exists    of FieldRef<'S>
    | Range     of FieldRef<'S> * Bound * Bound
    | Between   of FieldRef<'S> * FieldValue * FieldValue
    | Not       of Predicate<'S>
    | All       of Predicate<'S> list
    | Any       of Predicate<'S> list * MinShouldMatch
    // Phase 2: Nested
    | Nested    of path:string * inner:INestedLower
    // Phase 3: Text Queries
    | Match     of FieldRef<'S> * query:string * MatchParams
    | Phrase    of FieldRef<'S> * query:string
    | Prefix    of FieldRef<'S> * query:string
    | Fuzzy     of FieldRef<'S> * query:string * Fuzziness
    | RawEs     of QueryContainer

and [<RequireQualifiedAccess>] Agg<'S> =
    | Terms of field:FieldRef<'S> * size:int * subAggs:(string * Agg<'S>) list * orderBy: (string * string) option
    | Sum of field:FieldRef<'S>
    | Max of field:FieldRef<'S>
    | Cardinality of field:FieldRef<'S>
    | TopHits of size:int * sortField:FieldRef<'S> * descending:bool
