namespace Elastic.FSharp.Query.Operators

open Elastic.FSharp.Query.Types

[<AutoOpen>]
module Text =
    let match' (f: TextField<'S>) (query: string) : Predicate<'S> =
        Predicate.Match (FieldRef.TX f, query, MatchParams.empty)

    let matchWith (f: TextField<'S>) (query: string) (p: MatchParams) : Predicate<'S> =
        Predicate.Match (FieldRef.TX f, query, p)

    let matchWK (f: TextWithKeywordField<'S>) (query: string) : Predicate<'S> =
        Predicate.Match (FieldRef.TWK f, query, MatchParams.empty)

    let matchWithWK (f: TextWithKeywordField<'S>) (query: string) (p: MatchParams) : Predicate<'S> =
        Predicate.Match (FieldRef.TWK f, query, p)

    let phrase (f: TextField<'S>) (query: string) : Predicate<'S> =
        Predicate.Phrase (FieldRef.TX f, query)

    let phraseWK (f: TextWithKeywordField<'S>) (query: string) : Predicate<'S> =
        Predicate.Phrase (FieldRef.TWK f, query)

    let prefix (f: TextField<'S>) (query: string) : Predicate<'S> =
        Predicate.Prefix (FieldRef.TX f, query)

    let prefixWK (f: TextWithKeywordField<'S>) (query: string) : Predicate<'S> =
        Predicate.Prefix (FieldRef.TWK f, query)

    let fuzzy (f: TextField<'S>) (query: string) (fuzz: Fuzziness) : Predicate<'S> =
        Predicate.Fuzzy (FieldRef.TX f, query, fuzz)

    let fuzzyWK (f: TextWithKeywordField<'S>) (query: string) (fuzz: Fuzziness) : Predicate<'S> =
        Predicate.Fuzzy (FieldRef.TWK f, query, fuzz)
