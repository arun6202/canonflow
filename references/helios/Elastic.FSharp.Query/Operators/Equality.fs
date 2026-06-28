namespace Elastic.FSharp.Query.Operators

open Elastic.FSharp.Query.Types

[<AutoOpen>]
module Equality =
    let (=.) (f: KeywordField<'S>) (v: string) : Predicate<'S> =
        Predicate.Eq (FieldRef.KW f, FieldValue.VString v)

    let (=.!) (f: TextWithKeywordField<'S>) (v: string) : Predicate<'S> =
        Predicate.Eq (FieldRef.TWK f, FieldValue.VString v)

    let (=.?) (f: BoolField<'S>) (v: bool) : Predicate<'S> =
        Predicate.Eq (FieldRef.BL f, FieldValue.VBool v)

    let in' (f: KeywordField<'S>) (vs: string list) : Predicate<'S> =
        Predicate.In (FieldRef.KW f, vs |> List.map FieldValue.VString)

    let inWK' (f: TextWithKeywordField<'S>) (vs: string list) : Predicate<'S> =
        Predicate.In (FieldRef.TWK f, vs |> List.map FieldValue.VString)
