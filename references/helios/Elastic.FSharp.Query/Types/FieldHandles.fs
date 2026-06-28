namespace Elastic.FSharp.Query.Types

type AnalyzerRef = AnalyzerRef of string
type NormalizerRef = NormalizerRef of string
type IndexOptions = IndexOptions of string

/// <summary>
/// A strongly-typed handle to an Elasticsearch field.
/// Combines the physical path with Phantom Types to enforce schema correctness.
/// </summary>
/// <typeparam name="'S">The schema/index boundary token.</typeparam>
/// <typeparam name="'K">The logical data kind (e.g., Keyword, Numeric).</typeparam>
/// <typeparam name="'U">An optional unit of measure (e.g., for numerics or dates).</typeparam>
type Field<'S, 'K, 'U> = {
    Path        : string
    Kind        : 'K
}

// Specialisations exposed to users:

/// <summary>Represents a field mapped as 'keyword' in Elasticsearch. Supports exact match and terms queries.</summary>
type KeywordField<'S>           = Field<'S, Keyword, unit>

/// <summary>Represents a field mapped as 'text' in Elasticsearch. Supports full-text match queries and phrase queries.</summary>
type TextField<'S>              = Field<'S, Text, unit>
type TextWithKeywordField<'S>   = Field<'S, TextWithKeyword, unit>
type DateField<'S>              = Field<'S, Date, unit>
type NumericField<'S, 'Unit>    = Field<'S, Numeric, 'Unit>
type BoolField<'S>              = Field<'S, Bool, unit>
type NestedField<'S, 'NS>       = Field<'S, Nested, 'NS>
type EdgeNgramField<'S>         = Field<'S, EdgeNgram, unit>

// A phantom context token passed to the user's closure in the nested DSL
type NestedScope<'NS> = private { _marker : unit }
module NestedScope =
    let internal create () : NestedScope<'NS> = { _marker = () }

[<RequireQualifiedAccess>]
type FieldRef<'S> =
    | KW of KeywordField<'S>
    | TX of TextField<'S>
    | TWK of TextWithKeywordField<'S>
    | DT  of DateField<'S>
    | NUM of Field<'S, Numeric, obj> // Erased unit
    | BL  of BoolField<'S>
