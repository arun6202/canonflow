module Elastic.FSharp.Query.Tests.TextTests

open Xunit
open Elastic.FSharp.Query.Types
open Elastic.FSharp.Query.Operators
open Elastic.FSharp.Query.Lowering

type ArticleSchema = class end
let titleField : TextField<ArticleSchema> = { Path = "title"; Kind = Unchecked.defaultof<Text> }
let contentField : TextWithKeywordField<ArticleSchema> = { Path = "content"; Kind = Unchecked.defaultof<TextWithKeyword> }

[<Fact>]
let ``Text Queries Lower Correctly`` () =
    let q1 = Query.where' (match' titleField "hello")
    match Query.lowerQuery q1 with
    | QueryContainer.Match(path, q, None, None, None, None) ->
        Assert.Equal("title", path)
        Assert.Equal("hello", q)
    | _ -> Assert.Fail("Match failed")

    let q2 = Query.where' (phraseWK contentField "hello world")
    match Query.lowerQuery q2 with
    | QueryContainer.MatchPhrase(path, q, None) ->
        Assert.Equal("content", path)
        Assert.Equal("hello world", q)
    | _ -> Assert.Fail("MatchPhrase failed")

[<Fact>]
let ``I-14, E-16: Fuzziness on phrase is rejected at compile time`` () =
    // The `phrase` operator signature is `TextField<'S> -> string -> Predicate<'S>`.
    // It does not accept a Fuzziness parameter.
    // The F# compiler ensures that E-16 / I-14 are satisfied natively.
    ()
