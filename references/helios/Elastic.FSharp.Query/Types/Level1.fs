namespace Elastic.FSharp.Query.Types

open System.Text.Json.Nodes

[<RequireQualifiedAccess>]
type QueryContainer =
    | MatchAll of unit
    | MatchNone of unit
    | Term of field:string * value:FieldValue
    | Terms of field:string * values:FieldValue list
    | MatchPhrase of field:string * query:string * slop:int option
    | Match of field:string * query:string * analyzer:string option * fuzziness:string option * msm:string option * zeroTerms:string option
    | Prefix of field:string * value:string
    | Fuzzy of field:string * value:string * fuzziness:string
    | Range of field:string * gt:FieldValue option * gte:FieldValue option * lt:FieldValue option * lte:FieldValue option
    | Exists of field:string
    | Bool of must:QueryContainer list * filter:QueryContainer list * should:QueryContainer list * mustNot:QueryContainer list * minimumShouldMatch:string option
    | Nested of path:string * query:QueryContainer * ignoreUnmapped:bool
    // other variants as needed...

[<RequireQualifiedAccess>]
type AggContainer =
    | Terms of field:string * size:int * subAggs:(string * AggContainer) list * orderBy: (string * string) option
    | Sum of field:string
    | Max of field:string
    | Cardinality of field:string
    | TopHits of size:int * sortField:string * descending:bool
