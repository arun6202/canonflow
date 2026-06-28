namespace Elastic.FSharp.Query.Types

open System

[<RequireQualifiedAccess>]
type DateRounding = Year | Month | Week | Day | Hour | Minute | Second

[<RequireQualifiedAccess>]
type DateMath =
    | Now
    | Today
    | DaysAgo  of int
    | HoursAgo of int
    | RoundedTo of DateMath * DateRounding
    | Anchor   of DateTimeOffset * delta:string

[<RequireQualifiedAccess>]
type DateValue =
    | DAbsolute of DateTimeOffset
    | DMath     of DateMath
    | DEpochMs  of int64

[<RequireQualifiedAccess>]
type FieldValue =
    | VString  of string
    | VInt     of int64
    | VFloat   of float
    | VBool    of bool
    | VDate    of DateValue
    | VNull

[<RequireQualifiedAccess>]
type Bound =
    | Gt   of FieldValue
    | Gte  of FieldValue
    | Lt   of FieldValue
    | Lte  of FieldValue
    | Unbounded

[<RequireQualifiedAccess>]
type Fuzziness =
    | Auto
    | Edits of int

type MatchParams = {
    Analyzer : AnalyzerRef option
    Fuzziness : Fuzziness option
    MinimumShouldMatch : string option
    ZeroTermsQuery : string option
}

module MatchParams =
    let empty = { Analyzer = None; Fuzziness = None; MinimumShouldMatch = None; ZeroTermsQuery = None }
