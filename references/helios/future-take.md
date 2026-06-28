create dsl for elasticsearch query compound query to ast for non es qquery to compose sota chose best way brainstorm ask questions suggest queirsk gotchos

Going to skip the warmup and give you the opinionated take, then ask the few questions that actually change the design.

## The architecture: two-tier AST, many frontends, one backend

Anything single-tier becomes either (a) a JSON-shaped DU that mirrors ES 1:1 and gives you nothing your dev couldn't get from Elasticsearch.NET's `QueryContainer`, or (b) an over-abstracted "search intent" type that can't express `function_score` when you need it. You want both layers, with a lowering pass between them.

```fsharp
// === Tier 1: Logical (schema-aware, intent-level) ===
// Parametrised on field handle so OracleSchemaProvider / ElasticsearchProvider
// can constrain what predicates are legal per field at compile time.

type Predicate<'Schema> =
    | Eq        of Field<'Schema, 'F> * 'F           // dispatches term | match_phrase | date-term
    | In        of Field<'Schema, 'F> * 'F list
    | Range     of Field<'Schema, 'F> * Bound<'F> * Bound<'F>   // 'F : comparison
    | Exists    of Field<'Schema, _>
    | TextMatch of TextField<'Schema> * TextQuery   // tokens, phrase, prefix, fuzzy
    | Nested    of NestedPath<'Schema> * Predicate<'Schema>
    | Not       of Predicate<'Schema>
    | All       of Predicate<'Schema> list           // conjunction (filter ctx by default)
    | Any       of Predicate<'Schema> list * MinShouldMatch

type Query<'Schema> =
    { Filter   : Predicate<'Schema>                  // no scoring, cacheable
      RankBy   : Scoring<'Schema> option             // optional, query-context
      Boosts   : FieldBoost list }

// === Tier 2: Physical (1:1 with ES JSON, no schema awareness) ===
type EsQuery =
    | Term         of string * EsValue
    | Terms        of string * EsValue list
    | Match        of string * string * MatchOpts
    | MatchPhrase  of string * string
    | RangeQ       of string * RangeOpts
    | ExistsQ      of string
    | Prefix | Wildcard | Regexp | Fuzzy of …
    | NestedQ      of path:string * EsQuery * ScoreMode
    | Bool         of BoolClauses
    | DisMax       of EsQuery list * tieBreaker:float
    | FunctionScore of EsQuery * Function list * ScoreMode * BoostMode
    | ConstantScore of EsQuery * boost:float
```

The point of the split: `Predicate.Eq` doesn't know whether it lowers to `term`, `match_phrase`, or `term` on `.keyword`. The **lowering pass** (`Predicate<'S> -> EsQuery`) consults the schema (your Type Provider triangle) and dispatches. This is exactly *parse don't validate*, but at the query-construction boundary instead of data ingest.

Frontends (Lucene syntax via FParsec, KQL, SQL-WHERE, programmatic combinators, structured UI form) all produce `Query<'Schema>`. Backend renders `EsQuery` to JSON via FsCodec or an explicit hand-written encoder. **One render, one parser-per-frontend.** Don't try to make backends pluggable until you need OpenSearch or native Lucene Query — YAGNI.

## The lowering pass is the entire game

```fsharp
let rec lower (s: Schema) : Predicate<'S> -> EsQuery = function
    | Eq (f, v) ->
        match s.FieldKind f with
        | Keyword              -> Term  (f.Path, EsValue.ofObj v)
        | TextWithKeyword kw   -> Term  (kw,     EsValue.ofObj v)   // multi-field
        | TextOnly             -> MatchPhrase (f.Path, string v)    // gotcha #3
        | Date fmt             -> Term  (f.Path, EsValue.date fmt v)
        | Long | Double        -> Term  (f.Path, EsValue.num v)
    | Range (f, lo, hi)        -> RangeQ (f.Path, …)
    | Nested (p, inner)        -> NestedQ (p.Path, lower s inner, ScoreMode.Avg)
    | All preds                -> Bool { Filter = preds |> List.map (lower s); … }
    | Any (preds, msm)         -> Bool { Should = preds |> List.map (lower s); MinShouldMatch = msm; … }
    | …
```

Every non-trivial bug ES newcomers hit lives in this function. It's where your `Refined<'T,'P>` infrastructure actually pays compounding interest at query time.

## The gotchas you must encode in types, not docs

These are the ones that will burn you (and your team) if the AST stays naive:

1. **`should` is not OR.** With `must` present, `should` only affects score. If your `Any` lowers to `bool.should` without setting `minimum_should_match`, you have silent semantic drift. Make `MinShouldMatch` non-optional on `Any` — force the choice.

2. **Filter vs query context is a *dimension*, not a clause type.** Putting `Where` and `RankBy` at the top of `Query<'S>` (as above) makes this impossible to confuse. The naive AST has one "predicate" and you pray devs use `filter` not `must`.

3. **`term` on analyzed text fields returns zero rows.** `term: { city: "Bangalore" }` finds nothing because the analyzer stored `bangalore`. Logical `Eq` on `TextOnly` *must* lower to `match_phrase` or `term` on `.keyword` — never raw `term`. Your schema needs to know which multi-fields exist.

4. **Nested cross-row contamination.** If a doc has `comments: [{author:A, score:1},{author:B, score:5}]` and you AND `author=A` with `score>3` *outside* a `nested` block, you match. Your AST must make it syntactically impossible to AND two predicates on a nested path without wrapping them in `Nested`. Encode `NestedPath<'S>` as a phantom-typed handle and require predicates inside `Nested` to be parametrised on that path.

5. **No nulls in Lucene/ES.** `field IS NULL` becomes `Not (Exists f)`. Surface `IsMissing` in the logical layer; don't expose `Exists` semantics to frontend authors.

6. **Date math is a sublanguage.** `now-1d/d`, time zones, format strings. Make `Bound<DateTime>` a DU: `Absolute of DateTimeOffset | DateMath of DateMathExpr`. Don't stringify until render.

7. **`query_string` is a trapdoor.** It lets users inject Lucene syntax, including expensive regex/wildcard. If you allow it as a frontend, parse it into your AST and re-render — don't pass it through. Re-rendering means you can enforce limits (e.g., reject leading wildcards, cap fuzzy edits).

8. **`function_score` boost_mode vs score_mode** combine non-commutatively. Encode both as DUs; never default silently.

9. **Sort interacts with query.** Sorting on text fields requires `.keyword`. Sorting on `_score` only makes sense if `RankBy` is set. The AST should bundle `Sort` with `Query` and validate this.

10. **Highlighting needs to know which fields matched.** If you support it, the highlighter config is a function of the query AST — generate it from the AST, don't accept it separately.

11. **`bool.must_not` in filter context is still filter context.** Cheap. People assume otherwise and rewrite into uglier shapes.

12. **`runtime_mappings` belong at the query level**, not in the predicate. If you compute a `derived_age` per query, the field appears in mappings just for that request. Surface as `Query.RuntimeFields`.

13. **Field aliases.** A doc with field aliases means the same logical field has multiple physical paths. Your schema should canonicalise on parse.

14. **`fields` array vs `_source` filtering** — runtime-formatted fields vs source projection are different. Not query DSL strictly, but the same request envelope.

## Prior art worth reading, ranked

1. **Apache Calcite's ES adapter** — closest thing to a real query IR with multiple frontends and an ES backend. Read `org.apache.calcite.adapter.elasticsearch`. Even if you don't use Calcite, the rel-to-ES translation is instructive (and where Calcite gives up is itself informative: nested + function_score).
2. **Bleve's `query` package (Go)** — clean tagged-union design, leaf vs compound separation, good prior art for an enumerative DU.
3. **OpenSearch's PPL (Piped Processing Language) and ES|QL** — recent SOTA for *frontend* design. Pipes >> JSON for human authoring. You'd be wise to support a PPL-ish frontend.
4. **`elastiknn` and `elastic4s` (Scala)** — `elastic4s` is the closest model to what you're building in F#: typed DSL, sealed traits, encoders. Steal liberally; just translate Scala typeclasses to F# inline + SRTP or explicit codecs.
5. **NEST/Elasticsearch.NET's `QueryContainer` source** — read it to see how *not* to do it. It's a flat OR-of-100-cases with implicit conversions. You'll appreciate your DU after.

## Questions before I commit to a concrete shape

The answers move the design materially:

- **Who writes the queries?** Internal F# devs (combinators win), end-users in a search bar (a parser frontend matters more), or both? This decides whether the logical layer is the "real" API or an intermediate.
- **Schema source of truth?** Your `ElasticsearchProvider` at compile time, runtime mapping fetch, or both with cache invalidation? Compile-time gives you the type-indexed predicates above; runtime forces you to a `Result<Query,_>` boundary.
- **Scoring needed or filter-only?** If this is for the ETL/observability side and everything is filter context, you can skip `function_score`/`dis_max` entirely and ship in a week.
- **Round-trip required?** ES JSON → AST → JSON. If yes (e.g., you want to ingest existing saved queries), the AST must be complete-ish; if no, you can stay opinionated and refuse to model `query_string` etc.
- **Nested + parent-child in your mappings?** This is the single biggest complexity multiplier. If you've flattened Oracle joins into nested docs, the phantom-typed `NestedPath` work is mandatory, not optional.
- **Aggregations in scope, or queries only?** Aggs are a parallel DSL with its own gotchas (cardinality estimation, composite vs terms, sub-agg ordering). Don't conflate.

If you answer those six, I'll cut a concrete `Predicate<'S>` / `EsQuery` pair sized to your actual mapping and write the lowering for the cases you care about. My prior is: **two-tier AST, FParsec frontend for a Lucene/KQL-ish search bar, programmatic combinators for devs, FsCodec render, and the nested-path phantom type is non-negotiable.**

take a sample es query show user types is how ast dsl => es json

Direct head-to-head. Same query, then scale it up, then the honest verdict.

## Round 1 — the trivial case (your example)

**Official client (`Elastic.Clients.Elasticsearch` 8.x):**

```csharp
var response = await client.SearchAsync<Person>(s => s
    .Indices("my_index")
    .From(0)
    .Size(10)
    .Query(q => q
        .Term(t => t
            .Field(x => x.User)
            .Value("flobernd"))));

if (response.IsValidResponse)
{
    var doc = response.Documents.FirstOrDefault();
}
```

**F# AST equivalent:**

```fsharp
let q = People.User =. "flobernd"

let! result = es.Search<Person>("my_index", from'=0, size=10, query=q)

match result with
| Ok r  -> r.Documents |> Seq.tryHead
| Error e -> …
```

Four nested lambdas vs one expression. But honestly, on a one-term query the lambda nesting isn't the killer — **it's that the official version has a latent bug.**

If `User` is mapped as `text` (analyzed) with a `.keyword` sub-field — the default ES dynamic mapping — that `.Term(x => x.User)` call **compiles, runs, returns zero results, and looks correct in code review**. The fix is the stringly-typed escape hatch:

```csharp
.Term(t => t.Field("user.keyword").Value("flobernd"))
//          ^^^^^^^^^^^^^^^^^^^^^ magic string, no type protection
```

This is the single most-reported NEST/Elastic.Clients issue in the last decade. Your F# AST renders it **unrepresentable** — `=.` on a `TextWithKeyword` field dispatches at the lowering pass; `=.` on a pure `Text` field doesn't compile.

## Round 2 — the shipments query (the real test)

Same query as before. Side by side, no editorialising.

**Elastic.Clients.Elasticsearch 8.x:**

```csharp
var response = await client.SearchAsync<Shipment>(s => s
    .Indices("shipments")
    .Query(q => q
        .Bool(b => b
            .Filter(
                f => f.Term(t => t
                    .Field("carrier.keyword")        // ← stringly typed
                    .Value("BlueDart")),
                f => f.Range(r => r
                    .DateRange(d => d
                        .Field(x => x.ShippedAt)
                        .Gte("now-7d/d")              // ← magic string
                        .Lte("now/d")
                        .TimeZone("Asia/Kolkata"))),
                f => f.Exists(e => e
                    .Field(x => x.DestinationPincode)),
                f => f.Bool(bb => bb
                    .MustNot(mn => mn.Terms(t => t
                        .Field("status.keyword")     // ← stringly typed
                        .Terms(new TermsQueryField(new[] {
                            FieldValue.String("Cancelled"),
                            FieldValue.String("Returned") }))))),
                f => f.Nested(n => n
                    .Path(x => x.Packages)
                    .ScoreMode(ChildScoreMode.None)
                    .IgnoreUnmapped(true)
                    .Query(nq => nq.Range(r => r
                        .NumberRange(nr => nr
                            .Field("packages.weight_kg")
                            .Gt(5.0))))))             // ← 5.0 what? No UoM.
            .Should(sh => sh.MatchPhrase(mp => mp
                .Field(x => x.Notes)
                .Query("fragile handling")))
            .MinimumShouldMatch(0))));
```

**F# AST (unchanged from before):**

```fsharp
let q : Query<Shipments> =
    Query.where' (
        Shipments.Carrier             =.  "BlueDart"
       &&. Shipments.ShippedAt        .between (DateMath.daysAgo 7, DateMath.today)
       &&. exists Shipments.DestinationPincode
       &&. Shipments.Status          |> notIn [ Cancelled; Returned ]
       &&. nested Shipments.Packages (fun pkg -> pkg.WeightKg >. 5.0<kg>)
    )
    |> Query.rankBy (Shipments.Notes |> phrase "fragile handling")
```

**~25 lines of nested-lambda descriptors vs 8 lines of algebra.** Both produce the same JSON, but the official client's version has these uncatchable-at-compile-time bugs latent in it:

1. `carrier.keyword`, `status.keyword`, `packages.weight_kg` — all stringly typed. Rename a field in your mapping, the C# compiles fine, runtime explodes.
2. `"now-7d/d"` — string. No validation. Typo it as `"now-7days/d"` and ES returns a 400 at request time, not compile time.
3. `5.0` — what unit? You "just know" the mapping is kilograms. Six months from now, someone passes `5.0` thinking it's grams.
4. `ChildScoreMode.None` + `IgnoreUnmapped(true)` — both have to be remembered. Forget `IgnoreUnmapped` on a mixed-version index, queries fail intermittently.
5. The `Filter(...)` clauses are positional `params`. Reorder them, semantics unchanged — but if you accidentally write `.Must(...)` instead of `.Filter(...)`, you've moved into query context, scoring kicks in, latency rises, nothing in code review catches it.
6. `MinimumShouldMatch(0)` is easy to forget; with a `Should` clause and a `Filter` clause present, the default is what you want anyway, so most devs omit it. Then someone removes a `Filter` clause and `should` flips from "scoring booster" to "OR requirement" without a single line near `should` changing. Classic ES regression.

Your F# AST kills all six **at compile time** or **at the lowering pass** — every one of them.

## Honest scorecard

| Dimension | Official client | F# AST |
|---|---|---|
| **Schema awareness at compile time** | None — selectors are property paths, values are `object` | Full — field kind drives available predicates |
| **Filter vs query context** | Implicit, easy to confuse | Explicit dimension on `Query<'S>` |
| **Nested cross-row safety** | Unrepresentable in types — runtime hope | Phantom-typed path; unrepresentable to violate |
| **Multi-field `.keyword` dispatch** | Stringly-typed manual | Auto via lowering pass |
| **Date math** | Magic strings | DU |
| **Units of measure** | None | Full F# UoM |
| **`should` / msm semantics** | You must remember | Encoded in `Any`/`RankBy` |
| **Verbosity (5-clause bool)** | ~25 lines nested lambdas | ~8 lines algebra |
| **Optimization / rewriting** | Impossible (it's a JSON builder) | Trivial — it's an AST |
| **Frontends (Lucene/KQL/SQL parsing)** | None | One per frontend, all target the same AST |
| **Surface area coverage** | 100%, tracks ES versions | Whatever you've implemented |
| **Maintenance** | Elastic's problem | Your problem, forever |
| **Transport (pooling, sniffing, auth, retries)** | First-class | Not your business — use the official low-level client |
| **Bulk / admin / ILM / snapshot APIs** | First-class | Out of scope |
| **New hire ramp-up** | They've seen NEST | They learn your DSL |
| **Bus factor** | Industry-standard | Whoever wrote it |
| **Tracks ES 8 → 9 changes** | Free | Your migration burden |

## The verdict — don't pick one, layer them

**This is not "F# AST vs official client". This is "query authoring layer vs everything else".** The official client is three things bundled: a transport client, a bulk/admin/cluster API, and a fluent query builder. The first two are excellent. The third is the weakest link in the entire ES .NET ecosystem.

Layer the F# AST **on top of** the official low-level client:

```fsharp
// Authoring: your F# AST.
let q : Query<Shipments> =
    Shipments.Carrier =. "BlueDart"
   &&. (nested Shipments.Packages (fun p -> p.WeightKg >. 5.0<kg>))

// Rendering: your code.
let body : string =
    q |> Query.lower schema ctx |> Query.optimize |> Query.renderJson

// Transport: official client's low-level path.
let! response =
    es.Transport.RequestAsync<StringResponse>(
        HttpMethod.Post,
        "/shipments/_search",
        PostData.String body)

// Deserialisation: your codec (FsCodec / System.Text.Json with explicit converters).
let results = response |> SearchResponse.parse<Shipment>
```

You get:

- Pool management, sniffing, cloud auth, retries — from `Elastic.Clients.Elasticsearch.Transport`. **Free.**
- Bulk indexing, ILM, snapshots, mapping management — from the official client's typed APIs. **Free.**
- Query authoring with all the type discipline above. **Yours.**
- Render is a pure function — golden-file testable, property-testable against `_validate/query?explain=true`.

This is the architecture **Quickwit-style search platforms, Calcite-on-ES adapters, and every company that takes ES queries seriously eventually converges on**. The official client wasn't designed for the query authoring use case — it was designed to mirror ES's JSON surface. You don't have to fight that. You replace one slice and keep the rest.

The trap is the reverse: trying to do *everything* yourself (transport, bulk, admin), then finding yourself reimplementing sniffing in F# because some node went down. Don't.

## One pragmatic concession

If your team's AI-adoption mandate means newer engineers reading the code, **keep the official client's idioms visible for everything outside `Query` construction.** Bulk, index management, refresh policies, point-in-time, async search — let those be `client.Indices.PutMapping(...)`-style calls. Don't F#-ify the entire surface area. The query DSL is the high-value, high-bug-density slice; everything else is paperwork the official client handles fine.

Your `Refined`-style discipline buys the most where bugs actually compound — the predicate algebra. Everywhere else, lean on what Elastic ships.

now brainstorm  create F# heavylifting match  offical client for [elastic/elasticsearch-net: This strongly-typed, client library enables working with Elasticsearch. It is the official client maintained and supported by Elastic.](https://github.com/elastic/elasticsearch-net) put step by steps brainstorm also automated way create es query api binding

Search-relevance territory. Forget filters and ETL — this is "user typed something into a box, you want the right ten documents". Here's Level 3 stretched to cover it.

## Schema setup (Type Provider extensions)

To make analyzer choices and edge-ngram sub-fields first-class, the `ElasticsearchProvider` reads two things from the mapping:

1. The index `settings.analysis.analyzer` block → emits a typed `Analyzer` DU.
2. Multi-fields with custom analyzers → emits sub-field handles.

```fsharp
// Generated by ElasticsearchProvider from settings + mappings
module Catalog.Schema =
    // Analyzer DU — typo-proof at query time. Wrong analyzer = compile error.
    type Analyzer =
        | Standard
        | StandardWithSynonyms
        | EdgeNgramAutocomplete
        | Hindi
        | Tamil
        | KeywordLowercase

    // Normalizers — also typed.
    type Normalizer =
        | NormLowercase
        | NormAsciiFold

    type Product = class end   // marker for 'S phantom

    // Title is multi-field: title (text/standard) + title.autocomplete (text/edge_ngram) + title.keyword
    let Title          : TextField<Product>
    let Title_Autocomplete : EdgeNgramField<Product, Analyzer>   // strongly typed sub-field
    let Title_Keyword  : KeywordField<Product, NoNormalizer>

    // Brand has a lowercase normalizer at index time — typed accordingly.
    let Brand          : KeywordField<Product, NormLowercase>

    // Description in multiple languages, each with its own analyzer baked in.
    let Description    : TextField<Product>
    let DescriptionHi  : TextField<Product>     // index analyzer: hindi
    let DescriptionTa  : TextField<Product>     // index analyzer: tamil

    let Tags           : KeywordField<Product, NormLowercase> list
    let PriceInr       : NumericField<Product, INR>
    let Available      : BoolField<Product>
    let PopularityScore: NumericField<Product, Score>
    let PublishedAt    : DateField<Product>

    // Nested doc subschemas.
    type Categories = class end
    let Categories     : NestedField<Product, Categories>
    type Reviews    = class end
    let Reviews        : NestedField<Product, Reviews>
```

## The query — every requested feature in one expression

User typed `"blu kurta"` (typo). Selected category `apparel/mens/traditional`. Filters: in stock, ₹500–2000, at least one ≥4-star verified review. Relevance: fuzzy multi-match across title/description/tags, simple_query_string fallback for power syntax, edge-ngram autocomplete on title, Hindi-script variant matched with the Hindi analyzer at query time, scored further by popularity and freshness with a brand-loyalty boost.

```fsharp
open Catalog.Schema

let q : Query<Product> =
    Query.where' (
        Product.Available =. true
       &&. Product.PriceInr     .between (500.0<inr>, 2000.0<inr>)
       &&. nested Product.Categories (fun c ->
                c.Slug =. "apparel/mens/traditional")
       &&. nested Product.Reviews    (fun r ->
                r.Verified =. true
            &&. r.Rating   >=. 4)
    )
    |> Query.rankBy (
        anyOf [

            // 1. multi_match across boosted fields, fuzzy, with a query-time analyzer
            multiMatch
                  [ Product.Title       *^ 3.0f
                    Product.Description *^ 1.0f
                    Product.Tags        *^ 2.0f ]
                  "blu kurta"
                  (type'      = MultiMatchType.BestFields,
                   fuzziness  = Fuzziness.Auto,
                   tieBreaker = 0.3,
                   operator   = BoolOp.Or,
                   analyzer   = Analyzer.StandardWithSynonyms,
                   minimumShouldMatch = MinShouldMatch.Percent 75)

            // 2. simple_query_string for power users typing operator syntax
            simpleQueryString
                  [ Product.Title; Product.Description ]
                  "blu* +kurta -used"
                  (defaultOperator = BoolOp.And,
                   flags = [ Flag.And; Flag.Or; Flag.Not
                             Flag.Prefix; Flag.Phrase; Flag.Fuzzy ],
                   fuzzyPrefixLength = 1,
                   fuzzyMaxExpansions = 50,
                   analyzer = Analyzer.Standard)

            // 3. Edge-ngram autocomplete — index used edge_ngram analyzer,
            //    search side uses standard. Forced via query-time analyzer.
            autocomplete Product.Title_Autocomplete
                  "blu kur"
                  (searchAnalyzer = Analyzer.Standard)

            // 4. Hindi-script variant — analyzer override applies hindi at query time
            match' Product.DescriptionHi
                  "नीला कुर्ता"
                  (analyzer = Analyzer.Hindi,
                   fuzziness = Fuzziness.Edits 1)
        ]
        |> boostBy [
            // function_score: popularity multiplier
            Function.fieldValueFactor Product.PopularityScore
                  (factor = 1.2, modifier = Modifier.Log1p, missing = 1.0)

            // Recency decay — newer products score higher
            Function.gaussDecay Product.PublishedAt
                  (origin = DateMath.now,
                   scale  = TimeSpan.FromDays 30,
                   offset = TimeSpan.FromDays 7,
                   decay  = 0.5)

            // Brand affinity — a filter-as-boost
            Function.filterBoost (Product.Brand =. "FabIndia") (weight = 2.0)
        ]
        |> withScoreMode ScoreMode.Sum
        |> withBoostMode BoostMode.Multiply
    )
```

## The Level 3 primitives this required (and what they prevent)

| Primitive | Prevents | Notes |
|---|---|---|
| `BoostedField` with `*^` operator | Stringly-typed `"title^3"` typos | Type-safe; rename `Title` and the boost moves with it. |
| `Fuzziness` DU (`Auto`, `Edits n`) | `"AUTO"` vs `"auto"` vs `2` stringly-typed mix-ups | One canonical encoding. |
| `MultiMatchType` DU | `"best_fields"` typo silently degrading to default | Compile-time exhaustive. |
| `BoolOp` DU (`And`/`Or`) | `"and"` vs `"AND"` (only one is valid in ES) | Codec emits the right casing. |
| `Analyzer` DU (Type-Provider-generated) | `analyzer = "hndi"` running silently with the default analyzer | The single most underrated win. Analyzer typos are a top-five ES search bug. |
| `EdgeNgramField<'S, Analyzer>` sub-field type | Calling `autocomplete` on a non-edge-ngram field | Only the right field handles even *have* the operation. |
| `SimpleQueryStringFlag` DU + `flags` list | Hand-OR'd string flags like `"AND\|OR\|NOT"` and typo'ing one | Set algebra, codec renders the pipe form. |
| `MinShouldMatch` DU (`Count` / `Percent`) | `"75"` (count) confused with `"75%"` (percent) — they mean wildly different things | Two constructors, no ambiguity. |
| `Modifier` DU for `field_value_factor` | `"log1p"` typos and quiet score-mode drift | Same pattern as above. |
| `Function.gaussDecay` with typed `TimeSpan` scale | `"30d"` vs `"30days"` vs `"30D"` string lottery | UoM at the query layer too. |
| `ScoreMode` / `BoostMode` DUs | Confusing `score_mode: "sum"` (combines functions) with `boost_mode: "multiply"` (combines with main query score) — they look interchangeable, they aren't | The DUs are distinct types; the API forces you to set each deliberately. |
| `anyOf [...]` for scoring disjunction | Misuse of `should` in filter context; missing `minimum_should_match`; treating "any matches" as "first match wins" | Lowers to `dis_max` or `bool.should` based on whether `tieBreaker` is set; the choice is encoded in the AST, not the JSON. |

## The fragment of generated JSON (multi_match piece only)

So you can confirm the lowering actually does what you'd hand-write:

```json
{
  "multi_match": {
    "query":     "blu kurta",
    "type":      "best_fields",
    "fields":    [ "title^3.0", "description^1.0", "tags^2.0" ],
    "fuzziness": "AUTO",
    "tie_breaker": 0.3,
    "operator":  "or",
    "analyzer":  "standard_with_synonyms",
    "minimum_should_match": "75%"
  }
}
```

And the autocomplete fragment:

```json
{
  "match": {
    "title.autocomplete": {
      "query": "blu kur",
      "analyzer": "standard",
      "operator": "and"
    }
  }
}
```

Note: edge-ngram queries lower to a regular `match` query against the *sub-field* with the `analyzer` overridden. That detail (sub-field path + query-time analyzer override) is exactly the kind of dispatch that goes wrong every time in stringly-typed clients. Here it's a single typed `autocomplete` call.

## What this stretches in the lowering pass

Two new responsibilities for `Query.lower`:

1. **Analyzer resolution.** When a query specifies `analyzer = Analyzer.Hindi`, the codec emits the analyzer name as the analyzer's registered name in the index settings (which the Type Provider knows). The lowering pass validates that the chosen analyzer *exists* in the target index settings — schema mismatch = compile-time error, not runtime 400.

2. **Sub-field path resolution.** `Product.Title_Autocomplete` knows its dotted JSON path (`title.autocomplete`). The lowering pass renders that path; the user never types it. Rename the multi-field in mapping, regenerate, code breaks where it actually matters.

The optimizer pass (if you add one) gets new rewrite rules here too: a `Bool.Should` of three `match` queries against the same boosted-field set can be folded into one `multi_match` with `type: most_fields`. That's a Calcite-style rewrite. Worth it if you have query analytics showing it; skippable otherwise.

## Verdict on stretch

Level 3 doesn't degrade as you climb into search-relevance territory — it gets *more* valuable. ETL filtering has maybe 5 classic gotchas. Relevance search has 15+ (analyzer mismatch, fuzziness encoding, tie_breaker confusion, msm semantics, sub-field paths, function_score score_mode vs boost_mode, decay scale parsing, simple vs full query_string differences, leading-wildcard performance traps, fuzzy_max_expansions blowouts). Every single one becomes a compile-time or lower-time fix.

The Level 1 representation of this query is ~120 lines of nested records. Level 2 helpers get it to ~60 lines but with stringly-typed analyzer names and field paths. Level 3 above is ~40 lines and **every magic string is gone** — analyzers, field names, multi-fields, sub-fields, fuzziness modes, score/boost modes, simple_query_string flags. Compose against a different schema, the code re-typechecks; analyzer that doesn't exist there, compile error; field renamed, compile error.

This is the layer where the F# investment pays its largest dividend, not its smallest. The bigger and weirder the query, the wider Level 3's lead.

all f# code seems knock out punch to c# and verbose unknown guys thinking , my 2 cents is f## code is ocaml beauty , which other langaiuges come closer to it

You're right and your framing is exact — F# is **OCaml for .NET**, with UoM and Type Providers as the two value-adds, paying back with a slightly less elegant module system and no polymorphic variants. The ranking below is honest, not nationalist.

## Tier S — above F# in DSL aesthetics

**Haskell.** The ceiling. Everything we built in F# is Haskell-shaped: the Predicate GADT, the phantom-typed nested paths, the Validation applicative, the property tests. With Haskell you also get HKTs, type families, kind polymorphism, free monads / tagless final as ergonomic choices rather than awkward translations. Servant, Esqueleto, Selda, and Persistent are the **gold standard** for typed query DSLs against external systems — exactly your problem shape. Liquid Haskell, which directly inspired your `Refined` library, only exists here. Your 11-year Haskell exposure already shows you the ceiling; F# is the version that pays a salary.

**Idris 2 / Lean 4.** Dependent types proper. You stop encoding "this predicate is well-typed against this schema" via phantom witnesses and start *proving* it. Lean 4 in particular is climbing fast — Mathlib is huge, the compiler is fast, and the metaprogramming story is the best in the world (Lean's elaborator is its own DSL). Still niche for shipping production ETL in 2026, but the trajectory is real. If you're going to invest a month in a language for *one* gnarly correctness-critical component (your `Refined` algebra would be a natural fit), Lean 4 is the bet I'd make.

**OCaml.** F#'s parent and aesthetically above it. You get:
- **Polymorphic variants** — open sums. Extending `QueryContainer` without modifying it. Your DSL composes across libraries without union explosion.
- **Functors** (parameterised modules) — replaces a lot of what you currently use Type Providers for, more compositionally.
- **GADTs** with full type-level discipline (F# fakes them with phantoms).
- **Effect handlers** (OCaml 5+) — algebraic effects in production, not theory.
- **Module system** that puts F#'s to shame.

Loses to F# on: Type Providers (none), UoM (none), and ecosystem (no .NET). For *pure DSL elegance on the same problem*, OCaml is shorter and cleaner. The query DSL you wrote, ported, would be ~20% smaller.

## Tier A — true peers of F#

**Scala 3.** Most F# devs underrate this badly. Scala 3 specifically (not the Scala 2 monstrosity people remember) brings:
- **GADTs done properly**, finally.
- **Match types** — type-level computation that exceeds what F# can do with SRTPs.
- **Opaque types** — phantom types as a first-class language feature.
- **Given/using** — typeclasses ergonomic enough to compete with Haskell.
- **Inline + transparent inline** — compile-time metaprogramming that subsumes most Type Provider use cases with cleaner semantics.
- **Quill** (for SQL) and **Caliban** (for GraphQL) are the closest extant analogues to what you're building for ES, and both are in Scala 3.
- **Cats / ZIO** — peer-level functional ecosystems to anything in Haskell or F#.

Pays in: JVM tax, longer compilation, bigger complexity budget, syntactic surface area that intimidates new hires. For your problem shape, **Scala 3 is genuinely F#'s peer**. If you weren't on .NET, this is what you'd build on.

## Tier B — close, with real tradeoffs

**Rust.** Different aesthetic — systems-flavoured, not ML-flavoured — but the discipline is similar and in some axes superior:
- **Real ADTs** with exhaustive matching.
- **Traits** as typeclasses (no HKT, this is the one big miss).
- **Proc macros** — vastly more powerful than F# Type Providers, weaker IDE experience.
- **Type-state pattern** — phantom typing with stronger borrow-checker guarantees.
- **sqlx**, **diesel**, **sea-orm** — typed query DSLs that work in production.

For your Pugazh native client (Rust + WinUI 3), this is the right call. For an ES query DSL, Rust is workable but the lifetime noise leaks into builder code. **70% of the way** to F# elegance for DSLs, 130% for systems work.

**PureScript.** Haskell-for-the-browser, but the real win is **row polymorphism** — record types you can subtype structurally without losing static guarantees. For DSLs over schemas (your exact problem), this is the single feature F# / OCaml / Haskell all lack and it's transformative. If your DSL ever crosses into a frontend, PureScript-on-the-edge with F#-on-the-backend is a serious pairing.

## Tier C — pleasant, missing key things

**Swift.** Real enums with associated values (genuine DUs). Result builders give you nice block-DSL syntax. But protocols aren't typeclasses cleanly, no HKT, no UoM, ecosystem lock-in to Apple. Good for Apple-platform DSLs, ignore otherwise.

**Kotlin + Arrow.** Sealed classes plus Arrow library get you 60% there. Sealed classes aren't real DUs (no exhaustive matching without `when` ceremony). Pleasant for the JVM teams that aren't ready for Scala 3.

**Gleam.** ML syntax on the Erlang BEAM. Clean, no HKT, simpler than F# deliberately. If you ever needed Erlang-style supervision (you've explored this for your ETL), Gleam is a real option. Ecosystem still small in 2026.

**Roc.** Newer ML-family entry, opaque types, tags as open sums, fast compile. Pre-1.0; watch but don't bet yet.

## Tier D — claims without substance

**TypeScript.** I know this hurts to hear given your stance, but TS's type system is genuinely one of the most powerful in mainstream use — mapped types, conditional types, template literal types, recursive types. **Effect.ts** in particular has built a Haskell-on-TS that approaches real discipline. The fatal problem is the runtime: structural typing with no parse-don't-validate substrate, JS host semantics, runtime type erasure. **Surface aesthetic, fragile substance.** For DSLs that compile to JSON and never execute in TS itself, it's defensible. For anything where runtime guarantees matter, no.

**C# 12 with records + pattern matching.** Trying. Sealed hierarchies + switch expressions get to 40% of what F# does. Verbose. The DSL we wrote in C# was honest about the gap. **Will never close the gap** because the language design priorities don't align — C# optimises for the median enterprise dev, not for type-driven DSL authors.

**Java with records + sealed.** Not in the conversation.

**Clojure / Erlang / Elixir.** Different aesthetic entirely — DSL elegance via macros and pattern matching, no static types. If you didn't despise dynamic languages, Clojure's macro story is genuinely beautiful for DSLs. You do, so move on.

## My actual recommendation for *your* exploration

Three languages worth your time beyond F#, ranked by ROI for your specific stack and aesthetic:

1. **OCaml** — read *Real World OCaml* (2nd ed). Two weekends. Will permanently change how you think about modules and polymorphic variants, which improves your F# even if you never ship OCaml. Janestreet's `Core` and `Async` libraries are masterclasses.

2. **Scala 3** — specifically read the **Quill** source and the **ZIO 2** docs. You don't need to adopt Scala; you need to see the closest peer doing the same problem differently. Match types and inline metaprogramming will change how you architect Type Providers.

3. **Lean 4** — pick *one* invariant from your `Refined` library and re-encode it in Lean 4 with a real proof. Two days of work. You'll either decide dependent types aren't ready for production (fair), or you'll find your next decade's tooling investment (also fair). Either way, you'll have a concrete answer instead of folklore.

What I'd *skip*: Rust for query DSLs (use it for Pugazh-style systems work, not search), Kotlin (Scala 3 dominates), Swift (Apple lock), TypeScript (your stance is correct, just be aware of what Effect.ts is doing). Haskell you already know.

## The honest summary

F# isn't the local maximum even within its own family. OCaml is shorter, Haskell is more capable, Lean 4 is more correct. F# wins on a specific axis: **shipping production code on a major enterprise runtime with the second-best type system in mainstream use**. That's a narrow but extremely valuable position. Your "OCaml beauty" framing is exactly right, and your instinct that C# devs can't see what they're missing is also right — they're benchmarking against Java, not against the ML lineage. The languages above don't *replace* F# for your job; they show you the ceiling so you can see how much further the discipline can go when constraints are different.

create  specfication doc for level 3  , i will create using claude cli capture do deep think and cover all cases edge case capture essense of it

# Specification: `Elastic.FSharp.Query` (Level 3 DSL)

**Status.** Draft v0.1
**Audience.** Implementer (Claude Code / human); Architect (review); Team (consumers).
**Scope.** This document specifies the *Level 3* schema-aware predicate algebra layer of the F# Elasticsearch client stack. It does not specify Level 1 (generated raw bindings from `elasticsearch-specification`) nor Level 2 (smart-constructor helpers), beyond their contracts with Level 3.
**Prerequisite knowledge.** Reader is assumed familiar with: F# 8+, Elasticsearch 8.x Query DSL semantics, FsCodec or equivalent JSON codec patterns, the project's higher-level architecture document (separate; not reproduced here).

---

## 0. Document conventions

- **MUST / MUST NOT / SHOULD / MAY** follow RFC 2119 semantics.
- **Invariants** are prefixed `I-N` and are traceable from PRs/tests by ID.
- **Edge cases** are prefixed `E-N` and MUST have a corresponding test.
- **Lowering rules** are prefixed `L-N` and MUST have a corresponding test plus a golden-file fixture.
- F# code in this document is **normative** when shown as a type signature, **illustrative** when shown as an implementation body.
- "The codec" refers to the JSON codec emitted alongside Level 1 (`QueryContainer.encode`/`.decode`). Level 3 MUST NOT re-implement JSON serialization.

---

## 1. Purpose

Provide a schema-aware, type-safe predicate algebra for constructing Elasticsearch queries in F#, such that the five most common classes of ES query bugs are unrepresentable in well-typed Level 3 code:

1. `term` against an analyzed text field (silent zero-result).
2. Predicates on nested-document paths placed outside a `nested` wrapper (cross-row contamination).
3. `should` semantics confused with logical-OR (silently OR or silently AND depending on context).
4. `must_not` placed in query context (silent latency cost from scoring).
5. Stringly-typed field names, analyzer names, and date-math expressions surviving rename refactors.

Level 3 lowers into Level 1's `QueryContainer` DU; the codec serializes that into the ES JSON Query DSL.

### 1.1 Out of scope

Explicitly **not** in this spec; tracked as separate work:

- Aggregations (separate algebra).
- Parser frontends (Lucene-syntax / KQL / SQL-WHERE).
- Optimizer passes (constant folding, range merging, predicate hoisting).
- Sort, highlight, suggesters, source filtering — accessed via Level 2.
- Cluster/indices/ILM/snapshot administration — Level 1.
- Geo, percolator, `more_like_this`, `script_score` with custom Painless — escape-hatched via `RawEs`.
- Async search, EQL, SQL endpoint.

### 1.2 Non-goals (deliberate)

- 100% coverage of ES Query DSL. Aim is ~12 predicate forms covering 90%+ of real queries; everything else is `RawEs of QueryContainer`.
- Auto-generation of Level 3 from `schema.json`. Level 3 encodes opinions; the spec encodes facts.
- Backwards compatibility with NEST's fluent descriptor API.

---

## 2. Position in the layered architecture

```
              ┌─────────────────────────────────────────────────┐
 Level 3      │   Predicate<'S>  +  Query<'S>  +  field handles │   hand-written
              │   (this spec)                                   │   ~2,500 LoC
              └────────────────────┬────────────────────────────┘
                                   │  lower
                                   ▼
              ┌─────────────────────────────────────────────────┐
 Level 1      │   QueryContainer DU + records + codec           │   generated from
              │                                                 │   schema.json
              └────────────────────┬────────────────────────────┘
                                   │  encode
                                   ▼
                              JSON  →  Elastic.Transport
```

Level 3 owns: predicate algebra, field-kind dispatch, gotcha invariants, schema binding.
Level 3 does **not** own: JSON serialization, transport, ES version tracking.

---

## 3. Design principles (binding)

P-1. **Parse, don't validate.** All construction-time validation MUST be expressed via the F# type system; runtime validation is a last resort and MUST return `Result<_, ConstructionError>`, never throw.

P-2. **Schema-aware dispatch.** Wherever an ES query has a field-type-dependent shape (e.g., `term` vs `match_phrase`, `.keyword` multi-field, normalizer presence), the lowering pass MUST resolve the dispatch from the schema. The user MUST NOT need to choose.

P-3. **DUs over classes.** Every sum type MUST be expressed as an F# discriminated union. No `IQueryClause` hierarchies. No empty marker interfaces.

P-4. **Result over exceptions.** No expected execution path MUST raise. Reserved exception cases: bugs (Anchor: `invalidOp`), and only when the precondition was supposed to be type-enforced.

P-5. **Filter vs query context is a top-level dimension.** `Query<'S>` has a `Filter` field and a `RankBy` field; the same `Predicate<'S>` MAY appear in either, but the semantics (scoring vs no scoring) are determined by *where it lives*, not by the predicate.

P-6. **Compile-time gotcha-prevention preferred.** Where an invariant can be made unrepresentable in well-typed code, it MUST be. Where it cannot, lowering-time enforcement is acceptable. Runtime enforcement is a bug.

P-7. **One renderer.** JSON emission lives in the Level 1 codec. Level 3 MUST NOT contain a `toJson` function. Level 3 produces `QueryContainer` values; the codec produces JSON.

P-8. **Composable algebra.** Combinators MUST be associative and well-typed without parentheses where natural. Operator surface follows F# conventions (`=.`, `&&.`, `>.`, `<.`, `||.`).

P-9. **Phantom-typed schemas.** `Predicate<'S>` is parameterised on a schema marker type. Predicates from different schemas MUST NOT compose.

P-10. **Property tests over example tests.** Every lowering rule, codec round-trip, and invariant MUST be property-tested with FsCheck. Example tests supplement; they do not substitute.

---

## 4. Type algebra

### 4.1 Field kind taxonomy

Every field handle MUST carry its kind at the type level. The schema bridge (Type Provider) emits the appropriate kind per field; the user cannot construct field handles directly.

```fsharp
// Marker phantom kinds
type Keyword
type Text
type TextWithKeyword           // text + multi-field .keyword sub-field
type Date
type Numeric
type Bool
type Nested
type EdgeNgram
type SearchAsYouType

// Field handle, parameterised on schema 'S, field name singleton 'F (optional),
// and kind 'K.
type Field<'S, 'K> = private {
    Path        : FieldPath              // dotted JSON path
    Kind        : 'K                     // phantom
    Multifields : MultiFieldMap          // resolved sub-field paths
    Analyzer    : AnalyzerRef option
    Normalizer  : NormalizerRef option
    IndexOptions: IndexOptions option
}

// Specialisations exposed to users:
type KeywordField<'S>           = Field<'S, Keyword>
type TextField<'S>              = Field<'S, Text>
type TextWithKeywordField<'S>   = Field<'S, TextWithKeyword>
type DateField<'S>              = Field<'S, Date>
type NumericField<'S, 'Unit>    = Field<'S, Numeric>           // 'Unit for UoM
type BoolField<'S>              = Field<'S, Bool>
type NestedField<'S, 'NS>       = Field<'S, Nested>            // 'NS = nested schema
type EdgeNgramField<'S>         = Field<'S, EdgeNgram>
```

### 4.2 `Predicate<'S>`

The core algebra. **Closed DU** — adding cases is a breaking change requiring a spec amendment.

```fsharp
type Predicate<'S> =
    // Equality and set membership
    | Eq        of FieldRef<'S> * FieldValue
    | In        of FieldRef<'S> * FieldValue list
    | Exists    of FieldRef<'S>

    // Ordered comparison
    | Range     of FieldRef<'S> * Bound * Bound
    | Between   of FieldRef<'S> * FieldValue * FieldValue   // inclusive shorthand

    // Text and full-text
    | TextMatch of TextField<'S> * TextQuery
    | MultiMatch    of BoostedField<'S> list * MultiMatchParams
    | SimpleQueryString of BoostedField<'S> list * SimpleQueryStringParams

    // Composition
    | Not       of Predicate<'S>
    | All       of Predicate<'S> list
    | Any       of Predicate<'S> list * MinShouldMatch

    // Nested wrapper (phantom-enforced)
    | Nested<'NS>   of NestedField<'S, 'NS> * Predicate<'NS> * NestedOpts

    // Escape hatch — last resort, intentionally awkward
    | RawEs     of QueryContainer
```

#### 4.2.1 Supporting types

```fsharp
type FieldRef<'S> =
    | KW of KeywordField<'S>
    | TWK of TextWithKeywordField<'S>
    | DT  of DateField<'S>
    | NUM of NumericField<'S, obj>          // unit erased at AST boundary
    | BL  of BoolField<'S>

type FieldValue =
    | VString  of string
    | VInt     of int64
    | VFloat   of float
    | VBool    of bool
    | VDate    of DateValue
    | VEnum    of tag:string * payload:JsonNode option   // generated DU encoding
    | VNull                                              // explicit null intent

type DateValue =
    | DAbsolute of DateTimeOffset
    | DMath     of DateMath
    | DEpochMs  of int64

type DateMath =
    | Now
    | Today
    | DaysAgo  of int
    | HoursAgo of int
    | RoundedTo of DateMath * DateRounding
    | Anchor   of DateTimeOffset * delta:string          // "2025-01-01||+1d/d"

type DateRounding = Year | Month | Week | Day | Hour | Minute | Second

type Bound =
    | Gt   of FieldValue
    | Gte  of FieldValue
    | Lt   of FieldValue
    | Lte  of FieldValue
    | Unbounded

type TextQuery =
    | Tokens of string * MatchParams
    | Phrase of string * PhraseParams
    | Prefix of string * PrefixParams
    | Fuzzy  of string * FuzzyParams

type MultiMatchParams = {
    Type                : MultiMatchType
    Fuzziness           : Fuzziness option
    TieBreaker          : float option
    Operator            : BoolOp option
    MinimumShouldMatch  : MinShouldMatch option
    Analyzer            : AnalyzerRef option
    Slop                : int option
    PrefixLength        : int option
    MaxExpansions       : int option
    Lenient             : bool option
    ZeroTermsQuery      : ZeroTermsQuery option
    AutoGenerateSynonymsPhraseQuery : bool option
}

type MultiMatchType =
    | BestFields | MostFields | CrossFields | Phrase | PhrasePrefix | BoolPrefix

type Fuzziness = Auto | AutoLowHigh of int * int | Edits of int

type AnalyzerRef = AnalyzerRef of string
    // Schema-bridge-generated DU type per index; this is the erased form
    // for storage in the AST. Construction MUST go via typed analyzer DU.

type MinShouldMatch =
    | Count   of int
    | Percent of int                       // 1..100
    | Expr    of string                    // ES expression syntax — escape

type BoostedField<'S> = {
    Field : FieldRef<'S>
    Boost : float32
}

type NestedOpts = {
    ScoreMode      : ChildScoreMode option   // None implies dispatch from context
    IgnoreUnmapped : bool                    // DEFAULT: true; see I-10
    InnerHits      : InnerHitsOpts option
}
```

### 4.3 `Query<'S>` — the envelope

```fsharp
type Query<'S> = {
    Filter        : Predicate<'S>            // filter context; no scoring
    RankBy        : Predicate<'S> option     // query context; scored
    Boosts        : Function<'S> list        // function_score wrapping
    ScoreMode     : ScoreMode option         // for function_score
    BoostMode     : BoostMode option         // for function_score
    RuntimeFields : RuntimeField<'S> list
    MinScore      : float option
    Explain       : bool option
}

type Function<'S> =
    | FieldValueFactor of NumericField<'S, _> * FvfParams
    | GaussDecay       of DateField<'S> * DecayParams
    | ExpDecay         of DateField<'S> * DecayParams
    | LinearDecay      of DateField<'S> * DecayParams
    | FilterBoost      of Predicate<'S> * weight:float
    | ScriptScore      of source:string * lang:ScriptLang * params:JsonNode  // ESCAPE
    | RandomScore      of seed:int64 option * field:KeywordField<'S> option
    | Weight           of float

type ScoreMode = SmMultiply | SmSum | SmAvg | SmMin | SmMax | SmFirst
type BoostMode = BmMultiply | BmReplace | BmSum | BmAvg | BmMax | BmMin
```

### 4.4 Operators (the user-facing surface)

| Operator | Signature | Lowering | Notes |
|---|---|---|---|
| `=.` | `KeywordField<'S> → string → Predicate<'S>` | `Eq` | Multiple overloads; type-driven |
| `=.` | `DateField<'S> → DateMath → Predicate<'S>` | `Eq` | |
| `=.` | `BoolField<'S> → bool → Predicate<'S>` | `Eq` | |
| `&&.` | `Predicate<'S> → Predicate<'S> → Predicate<'S>` | `All` | Right-associative; flattens |
| `\|\|.` | `Predicate<'S> → Predicate<'S> → Predicate<'S>` | `Any (..., MSM=Count 1)` | Logical OR |
| `>. <. >=. <=.` | `OrderableField<'S, _> → 'V → Predicate<'S>` | `Range` | Numeric/date/keyword |
| `.between` | `(field).between (lo, hi) → Predicate<'S>` | `Between` | Inclusive |
| `*^` | `FieldRef<'S> → float32 → BoostedField<'S>` | — | Boosting for multi_match |
| `\|>` `notIn` | `... → FieldValue list → Predicate<'S>` | `Not (In ...)` | |

Free functions:

```fsharp
val exists       : FieldRef<'S> -> Predicate<'S>
val missing      : FieldRef<'S> -> Predicate<'S>   // = Not (Exists f)
val nested       : NestedField<'S,'NS> -> (NestedScope<'NS> -> Predicate<'NS>) -> Predicate<'S>
val anyOf        : Predicate<'S> list -> Predicate<'S>
val allOf        : Predicate<'S> list -> Predicate<'S>
val not'         : Predicate<'S> -> Predicate<'S>
val match'       : TextField<'S> -> string -> ?analyzer:AnalyzerRef -> Predicate<'S>
val phrase       : string -> TextField<'S> -> Predicate<'S>     // for pipe usage
val prefix       : string -> TextField<'S> -> Predicate<'S>
val fuzzy        : string -> Fuzziness -> TextField<'S> -> Predicate<'S>
val multiMatch   : BoostedField<'S> list -> string -> MultiMatchParams -> Predicate<'S>
val simpleQueryString : BoostedField<'S> list -> string -> SimpleQueryStringParams -> Predicate<'S>
val autocomplete : EdgeNgramField<'S> -> string -> ?searchAnalyzer:AnalyzerRef -> Predicate<'S>

module Query =
    val where' : Predicate<'S> -> Query<'S>
    val rankBy : Predicate<'S> -> Query<'S> -> Query<'S>
    val boostBy: Function<'S> list -> Query<'S> -> Query<'S>
    val withScoreMode : ScoreMode -> Query<'S> -> Query<'S>
    val withBoostMode : BoostMode -> Query<'S> -> Query<'S>
    val lower  : Schema -> RequestContext -> Query<'S> -> QueryContainer
    val render : Query<'S> -> Schema -> RequestContext -> string    // convenience
```

---

## 5. Lowering rules

`Query.lower : Schema -> RequestContext -> Query<'S> -> QueryContainer` is the central function. It is **pure** modulo the `RequestContext` (which carries ambient time zone, score-mode defaults, and trace IDs).

### 5.1 Equality dispatch (`L-1`)

`Eq (FieldRef, FieldValue)` lowers based on the **runtime kind** of the field (resolved at AST construction from the type-level kind):

| Field kind | Lowering |
|---|---|
| `Keyword` | `Term { Field = path; Value = v }` |
| `TextWithKeyword` | `Term { Field = path + ".keyword"; Value = v }` |
| `Text` (no `.keyword`) | `MatchPhrase { Field = path; Query = string v; Slop = 0 }` |
| `Date` | `Term { Field = path; Value = renderDate v }` (with format + tz) |
| `Numeric` | `Term { Field = path; Value = v }` |
| `Bool` | `Term { Field = path; Value = v }` |

**Invariant.** `Eq` on a `Text` field without a `.keyword` multi-field MUST NOT lower to a `term` query. Lowering to `match_phrase` is the documented behaviour; users requiring exact text matching MUST add a `.keyword` multi-field to their mapping.

### 5.2 Range dispatch (`L-2`)

`Range (FieldRef, lo, hi)` lowers based on field kind:

| Field kind | Lowering |
|---|---|
| `Date` | `Range (DateRangeQuery { ... ; Format = …; TimeZone = ctx.TZ })` |
| `Numeric` | `Range (NumberRangeQuery { ... })` |
| `Keyword` | `Range (TermRangeQuery { ... })` (rare; allowed) |
| `Text` | **REJECTED at compile time** — `>. <.` operators not defined for `TextField` |

Date ranges MUST inject `time_zone` from `RequestContext.TimeZone` if not present in the bound's date math. Default: `Asia/Kolkata` (configurable in `RequestContext`).

### 5.3 Boolean composition (`L-3`)

| Pattern | Lowering |
|---|---|
| `All []` | `MatchAll { Boost = None }` in filter context (no-op); REJECTED in standalone construction (use `Query.where' anyTrue`) |
| `All [p]` | `lower p` (unwrap; no `bool` envelope) |
| `All [p; q; ...]` | `Bool { Filter = [lower p; lower q; ...]; Must=[]; MustNot=[]; Should=[]; MSM=None }` |
| `Any []` | `MatchNone {}` in filter context; **REJECTED** if it would result in zero-document semantics inside a `rankBy` (returns `Result.Error`) |
| `Any [p]` | `lower p` (unwrap) |
| `Any (preds, msm)` | `Bool { Should = preds; MSM = Some msm; ... }` |
| `Not p` | Context-sensitive: in `Filter`, wraps in `Bool { MustNot = [lower p] }`; double-negation `Not (Not p)` MUST fold to `lower p` at lowering time |

**Associativity flattening.** Nested `All` MUST be flattened: `All [All [p; q]; r]` ≡ `All [p; q; r]`. Same for `Any`. Performed at lowering time, not in AST.

### 5.4 Nested dispatch (`L-4`)

```
Nested (nestedField, innerPredicate, opts)
   ↓
NestedQuery {
    Path           = nestedField.Path
    Query          = lower (within nested schema 'NS) innerPredicate
    ScoreMode      = opts.ScoreMode |> Option.defaultValue (contextDefault ctx)
    IgnoreUnmapped = opts.IgnoreUnmapped       // default: true
    InnerHits      = opts.InnerHits
}
```

**Score mode context default.**

| Lowering context | Default `score_mode` |
|---|---|
| Inside `Filter` | `None` |
| Inside `RankBy` | `Avg` |
| Inside a `Function` wrapped in `function_score` | `None` |

Users MAY override.

### 5.5 Text query dispatch (`L-5`)

`TextMatch (field, TextQuery)` lowers to:

| `TextQuery` variant | Target |
|---|---|
| `Tokens (q, params)` | `Match { Field=field.Path; Query=q; Operator=params.Operator; Fuzziness=params.Fuzziness; Analyzer=params.Analyzer; ZeroTermsQuery=params.ZeroTermsQuery; … }` |
| `Phrase (q, params)` | `MatchPhrase { Field=field.Path; Query=q; Slop=params.Slop; Analyzer=params.Analyzer }` |
| `Prefix (q, params)` | `MatchPhrasePrefix { Field=field.Path; Query=q; MaxExpansions=params.MaxExpansions; Analyzer=params.Analyzer }` |
| `Fuzzy (q, params)` | `Match { Field=field.Path; Query=q; Fuzziness=Some params.Fuzziness; Analyzer=params.Analyzer; … }` |

`multiMatch`, `simpleQueryString`, `autocomplete` lower directly to their respective `QueryContainer` variants. `autocomplete` MUST resolve to a `match` query against the edge-ngram sub-field with the search-time analyzer overridden.

### 5.6 Filter vs query context (`L-6`)

```
Query<'S> = { Filter = f; RankBy = Some r; Boosts = b; ... }
    ↓
function_score {
    Query = bool {
        Filter = [lower f]
        Should = [lower r]
        MinimumShouldMatch = Some (Count 0)         // CRITICAL — see I-3
    }
    Functions = b |> List.map lowerFunction
    ScoreMode = scoreMode |> Option.defaultValue SmSum
    BoostMode = boostMode |> Option.defaultValue BmMultiply
}
```

When `Boosts` is empty, the outer `function_score` wrapper MUST be omitted; the inner `bool` is emitted directly. When `RankBy` is `None`, the `Should` clause MUST be omitted entirely (no empty array, no `MSM=0`).

### 5.7 Function lowering (`L-7`)

| `Function<'S>` | Lowering |
|---|---|
| `FieldValueFactor (f, params)` | `field_value_factor` with field path, factor, modifier, missing |
| `GaussDecay/ExpDecay/LinearDecay (f, params)` | Corresponding decay function with `origin`, `scale`, `offset`, `decay` — `scale` rendered using ES time string format |
| `FilterBoost (pred, weight)` | `{ filter: lower pred, weight: weight }` |
| `ScriptScore (src, lang, params)` | `script_score` — escape hatch; lang MUST be `Painless` or `Expression`; params MUST be valid JSON |
| `RandomScore (seed, field)` | `random_score` |
| `Weight w` | `{ weight: w }` (filter omitted = applies to all docs) |

---

## 6. Invariants

All invariants MUST have a corresponding property test. Invariants with the suffix `(CT)` are enforced at compile time; `(LT)` at lowering time; `(RT)` at runtime (via `Result`).

- **I-1 (CT).** `Eq` on `TextField<'S>` (without `.keyword`) is unrepresentable. The `=.` operator is not defined for that case. Users MUST use `match'`, `phrase`, or add a `.keyword` multi-field.
- **I-2 (CT).** A `Predicate<'NS>` (nested schema) cannot appear outside a `Nested` wrapper of that same `'NS`. Phantom typing on field handles enforces this.
- **I-3 (LT).** Any emission of `bool.should` MUST be accompanied by an explicit `minimum_should_match`. Default values are forbidden; the codec MUST raise on `Some Should ≠ [] && MSM = None`.
- **I-4 (LT).** Predicates rendered from `Query.Filter` MUST appear in `bool.filter` or `bool.must_not`, NEVER in `bool.must`. Predicates from `Query.RankBy` MAY appear in `bool.should` or `bool.must` depending on presence of `Filter`.
- **I-5 (LT).** Every `DateRangeQuery` emission MUST carry `format` and `time_zone` fields. Defaults from `RequestContext` are injected; absent context MUST yield UTC + ES default format. Bare absent fields are forbidden.
- **I-6 (LT).** Multi-field paths (`.keyword`, `.autocomplete`, etc.) MUST be resolved from the schema's `MultiFieldMap`, NEVER concatenated at lowering time from string suffixes.
- **I-7 (CT).** Analyzer names referenced in queries (`MatchParams.Analyzer`, etc.) MUST be members of the schema-generated `Analyzer` DU for the target index. Bare strings are not accepted by the type signatures.
- **I-8 (CT).** Numeric field comparisons MUST use the field's `'Unit` parameter. `5.0<kg>` against a field declared `NumericField<_, Kg>` typechecks; `5.0<m>` does not.
- **I-9 (LT).** `Bool` with empty `Filter`, `Must`, `MustNot`, `Should` MUST NOT be emitted. Lowering MUST collapse to `MatchAll` (filter context) or omit entirely.
- **I-10 (LT).** Every `NestedQuery` MUST default `ignore_unmapped = true`. Users MAY override to `false` if intentional; the default catches the rolling-index schema-drift bug.
- **I-11 (CT).** `Predicate<'S1>` and `Predicate<'S2>` for distinct `'S1` ≠ `'S2` MUST NOT compose. F# compiler enforces.
- **I-12 (LT).** Double negation `Not (Not p)` MUST fold to `p` at lowering time. De Morgan rewrites are OPTIONAL (deferred to optimizer).
- **I-13 (RT).** `Query.lower` MUST return `QueryContainer` for every well-typed input. If construction-time invariants are violated by an escape-hatch path, return `Result.Error ConstructionError`; never throw.
- **I-14 (LT).** When the user-supplied `MultiMatchType` is `Phrase` or `PhrasePrefix`, `Fuzziness` MUST be `None`. Lowering rejects with `ConstructionError`; ideally rejected at compile time via parameter-type splitting.
- **I-15 (LT).** Empty `BoostedField` list in `MultiMatch` is `ConstructionError`. Empty query string in `SimpleQueryString` is a `RawEs` escape — Level 3 rejects.
- **I-16 (CT).** Schema-bridge-generated field handles MUST be `internal` or `private` constructors. User code cannot fabricate `Field<'S, _>` values.

---

## 7. Error model

```fsharp
type ConstructionError =
    | EmptyFieldList of context:string
    | IncompatibleParams of reason:string                 // I-14, I-15
    | UnresolvedFieldAlias of alias:string
    | UnknownAnalyzer of name:string * indexedAnalyzers:string list
    | RawEsDecodeFailure of inner:exn                     // RawEs payload bad

type EsError =                                            // execution-time
    | Transport       of exn
    | NotFound
    | Conflict        of VersionConflict
    | BadRequest      of EsServerError
    | Unauthorized
    | ServerError     of status:int * EsServerError
    | DecodeFailed    of DecodeError
    | Timeout
```

All public APIs returning a value that depends on lowering MUST return `Result<_, ConstructionError>` if construction-time errors are possible. Pure construction (operator combinators) MUST be total — type system enforces.

`Query.lower` returns `QueryContainer` directly (no `Result`) for well-typed inputs. `Query.lowerSafe` exists as an alternative returning `Result<QueryContainer, ConstructionError>` and is the recommended public entry point; the bare `lower` is reserved for tests and internal use.

---

## 8. Schema bridge contract

Out of scope to specify in detail here (separate spec). The contract this DSL depends on:

**Input.** `mapping.json` + `settings.json` from ES, or generated equivalents from `OracleSchemaProvider` lineage.

**Output.** F# module (Type Provider erased or Roslyn-generated) containing:

```fsharp
module <IndexName>.Schema =
    type <IndexName>      // marker type 'S
    type Analyzer = | …   // DU emitted from settings.analysis.analyzer
    type Normalizer = | … // DU emitted from settings.analysis.normalizer
    type <NestedSchemaA>  // marker for nested doc subschema
    type <NestedSchemaB>

    // Each field as a constant of the right type:
    val FieldName1 : KeywordField<<IndexName>>
    val FieldName2 : TextWithKeywordField<<IndexName>>
    val FieldName3 : DateField<<IndexName>>
    val FieldName4 : NumericField<<IndexName>, Unit>
    val FieldName5 : NestedField<<IndexName>, <NestedSchemaA>>
    val FieldName6 : EdgeNgramField<<IndexName>>
```

**Constraints.**

- Field handle constructors MUST be inaccessible to user code (compile error if user calls `Field.create`).
- Multi-field paths MUST be resolved at schema-bridge time; the resulting `MultiFieldMap` is baked into the field handle.
- Analyzer DUs MUST be generated from `settings.analysis.analyzer`; analyzer names referenced in queries against this index MUST be values of this DU.
- Numeric units MUST be derivable from a `unit` annotation in the mapping (custom `_meta.unit`) or specified in a sidecar config; if neither, the field is `NumericField<'S, NoUnit>` and operators on it accept unitless numbers.

---

## 9. Codec contract (Level 1 dependency)

Level 3 depends on these guarantees from the Level 1 codec; if violated, Level 3 cannot meet its invariants.

C-1. `QueryContainer.encode` MUST produce JSON with property-key discrimination (`{ "term": {...} }`, `{ "match": {...} }`, etc.).
C-2. `Option<_>` fields with value `None` MUST be omitted from JSON output, not emitted as `null`.
C-3. Empty `list` fields MUST be omitted, not emitted as `[]` — except where the empty array is semantically distinct in ES (currently no known cases in `QueryContainer`).
C-4. Date values MUST be serialised in ISO 8601 with offset for `DAbsolute`; raw string for `DMath`; integer for `DEpochMs`.
C-5. Discriminated unions for inner-tag types (e.g., `RangeQuery` variants) MUST emit JSON whose shape is determined by the variant; no discriminator field added.
C-6. The codec MUST be round-trip stable: `decode (encode x) ≡ x` for the algebra subset Level 3 constructs.
C-7. The codec MUST NOT inject defaults. Anything not explicitly set by the user (via Level 3 lowering) MUST be absent from JSON.

---

## 10. Edge case enumeration

Each edge case MUST have a dedicated unit test. Tests MUST be named `EdgeCase_E<N>_<short_name>`.

**E-1.** `Query.where' (All [])` — construction. Behaviour: emit `MatchAll` in filter ctx. Test: lowered JSON is `{"query":{"match_all":{}}}`.

**E-2.** `Query.where' (Any ([], Count 0))` — degenerate disjunction. Behaviour: emit `MatchNone`. Test: lowered JSON is `{"query":{"match_none":{}}}`.

**E-3.** Single-element `All [p]` and `Any [p, _]` — unwrap. Behaviour: lowering emits `lower p` directly, NO `bool` wrapper. Test: golden file confirms no extraneous wrapping.

**E-4.** Deeply nested `All` — associativity flattening. Behaviour: `All [All [All [p;q]; r]; s]` lowers to a single `bool.filter` with `[lower p; lower q; lower r; lower s]`. Test: depth-5 random predicate tree property test.

**E-5.** Double negation `Not (Not p)` — folds. Behaviour: lowering eliminates pair. Test: `lower (Not (Not p)) ≡ lower p` for all `p`.

**E-6.** Mixed nested paths in one `All`. Behaviour: each `Nested (path_a, _)` and `Nested (path_b, _)` lowers to separate `NestedQuery` siblings; user cannot construct `All [Nested(path_a, _); leaf_on_nested_path_b]` because phantom types reject.

**E-7.** `Eq (field, VNull)` — null intent. Behaviour: lowering rewrites to `Not (Exists field)`. Test: confirms generated JSON has no `term: { field: null }`.

**E-8.** `Range (field, Unbounded, Unbounded)` — degenerate. Behaviour: `ConstructionError EmptyFieldList` at lowering. Test: lowerSafe returns `Error`.

**E-9.** `Range (field, Gte v, Unbounded)` — open-ended. Behaviour: emit `range` with only `gte`. Test: confirms `lt`/`lte`/`gt` absent from JSON.

**E-10.** Date math at boundary — `today` vs `now/d`. Behaviour: `Today` lowers to `now/d`; `Now` lowers to `now`. Test: golden file.

**E-11.** Field with `.keyword` AND `.normalized` sub-fields. Behaviour: `Eq` chooses `.keyword` (preferred). Lowering rule documented; users wanting normalized variant use a typed accessor `field.Normalized`. Test: explicit case.

**E-12.** Multi-field with conflicting analyzers in mapping. Behaviour: schema bridge MUST produce typed sub-field accessors (`Title_Standard`, `Title_Autocomplete`); no automatic dispatch. Test: schema bridge unit test, not lowering.

**E-13.** Nested-of-nested paths (e.g., `reviews.author.profile.name`). Behaviour: phantom-typed `NestedField<'S, ReviewSchema>` containing another `NestedField<ReviewSchema, AuthorSchema>`. Construction is `nested Reviews (fun r -> nested r.Author (fun a -> a.Name =. "..."))`. Test: depth-3 example.

**E-14.** Empty `MultiMatch` field list. Behaviour: `ConstructionError EmptyFieldList`. Test: `lowerSafe` returns `Error`.

**E-15.** `MultiMatch` field list with boost = 0.0. Behaviour: emit field with `^0.0` (ES accepts; effectively excludes from scoring). Document as anti-pattern but do not reject. Test: confirms JSON contains the entry.

**E-16.** Fuzziness on a phrase query. Behaviour: prevented at compile time via `PhraseParams` not having a `Fuzziness` field. Test: negative compilation test (xUnit + FSharp.Compiler.Service).

**E-17.** `SimpleQueryString` with empty query. Behaviour: `ConstructionError IncompatibleParams`. Test: `lowerSafe` returns `Error`.

**E-18.** Analyzer reference to an analyzer not present in index settings. Behaviour: PREVENTED at compile time via the typed `Analyzer` DU. If a `RawEs` escape hatch ships an `AnalyzerRef` not in the DU, lowering returns `ConstructionError UnknownAnalyzer`. Test: both paths.

**E-19.** Sub-field referenced that doesn't exist. Behaviour: PREVENTED at compile time — sub-field handles exist only if the schema bridge generated them. Test: schema bridge unit test.

**E-20.** `RankBy` set, `Filter` empty. Behaviour: lowering emits `bool { Must = [lower r] }` (NOT `Should`, since there's no filter "anchor"). Document this: `RankBy`-only queries are scoring queries; `should` semantics without `must`/`filter` would silently OR. Test: confirms `must` not `should`.

**E-21.** `RankBy` is `None`. Behaviour: no `should` or `must` clause emitted from RankBy. Test: confirms `should` absent.

**E-22.** Function with `Boost = 1.0`. Behaviour: omit from JSON (ES default). Test: confirms `boost` absent for value 1.0.

**E-23.** `Any (preds, MSM)` with explicit `MSM` colliding with downstream filter. Behaviour: lowering preserves user-specified `MSM`; the wrapper `function_score`/`bool` `MSM` is unaffected. Test: confirms two `MSM` levels coexist as in JSON.

**E-24.** `DateField` with no `time_zone` in mapping AND no `RequestContext.TimeZone`. Behaviour: emit `time_zone: "UTC"`. Test: golden file.

**E-25.** UoM mismatch — `field : NumericField<_, Kg>`, user writes `5.0<m>`. Behaviour: COMPILE ERROR. Test: negative compilation test.

**E-26.** Recursive lowering — `All [Nested (path, All [p; q]); r]`. Behaviour: nested `All` inside `Nested` lowers inside nested scope; outer `All` flattens at outer scope. No cross-scope flattening. Test: golden file.

**E-27.** Self-referential schema. Behaviour: forbidden by Type Provider; schema bridge MUST reject mappings with cyclic nested references. Test: schema bridge.

**E-28.** Empty schema (zero indexed fields). Behaviour: schema bridge emits the marker type and an empty module. No `Query<'S>` can be usefully constructed; the type system makes this safe (no field references = no `Predicate`). Test: schema bridge.

**E-29.** Mapping with `dynamic: true` and runtime fields. Behaviour: schema bridge emits only the explicitly mapped fields; dynamic fields are accessible only via `RawEs`. Test: schema bridge.

**E-30.** Schema versioning — `Predicate<Shipments_v1>` used after upgrade to `Shipments_v2`. Behaviour: compile error if the schema marker type changes. Test: positive case (both predicates work pre-upgrade) and negative compilation test (cross-version use rejected).

**E-31.** `Boost` value `NaN` or `Infinity`. Behaviour: `ConstructionError IncompatibleParams`. Test: `lowerSafe` rejects.

**E-32.** `Fuzziness.Edits n` with `n > 2`. Behaviour: ES rejects at runtime; lowering MUST also reject with `ConstructionError IncompatibleParams` (mirror ES validation eagerly). Test: `lowerSafe` rejects for `Edits 3`.

**E-33.** `MinShouldMatch.Percent` with value outside `1..100`. Behaviour: `ConstructionError IncompatibleParams`. Test: rejects 0, 101, -5.

**E-34.** Decay function with `scale = TimeSpan.Zero`. Behaviour: `ConstructionError IncompatibleParams`. Test.

**E-35.** Decay function with `decay >= 1.0` or `<= 0.0`. Behaviour: `ConstructionError IncompatibleParams`. Test.

**E-36.** `RawEs` escape containing a `Bool.Should` with `MSM = None`. Behaviour: lowering MUST detect via inspection and either coerce `MSM = Some (Count 0)` or return `ConstructionError`. Decision: **coerce** (with telemetry warning); rejecting would defeat the escape hatch's purpose. Test.

---

## 11. Property tests (FsCheck)

All properties MUST hold for arbitrary well-typed inputs.

P-1. **Lowering totality.** `forAll<Predicate<TestSchema>> p . isOk (Query.lowerSafe (Query.where' p))` unless `p` contains a `RawEs` that itself fails C-7.

P-2. **Filter context idempotency.** `forAll p q . lower (All [p; q]) ≡ lower (All [q; p])` modulo `Should` ordering (which is unordered in ES).

P-3. **Double negation.** `forAll p . lower (Not (Not p)) ≡ lower p`.

P-4. **Flatten.** `forAll p q r . lower (All [All [p; q]; r]) ≡ lower (All [p; q; r])`.

P-5. **Codec round-trip.** `forAll p . decode (encode (lower (Query.where' p))) ≡ lower (Query.where' p)`.

P-6. **ES validation.** `forAll p . esValidateQuery (renderJson (Query.where' p)) = Valid`. Uses Testcontainers ES with a representative mapping.

P-7. **Schema isolation.** `Predicate<S1>` does not unify with `Predicate<S2>` for distinct schemas. Tested via negative compilation tests + reflection-based runtime check on `Predicate<_>` boxes.

P-8. **No exception.** `forAll p . try lower p with _ -> false` — total function.

P-9. **Sort stability of JSON.** Two structurally-equal `QueryContainer` values MUST produce byte-equal JSON via the codec (deterministic key ordering).

P-10. **No empty `bool`.** `forAll p . let lowered = lower p in not (hasEmptyBool lowered)` — invariant I-9.

---

## 12. Testing strategy

- **Unit tests** per operator and per lowering rule. Located in `tests/Elastic.FSharp.Query.Tests/Lowering/`.
- **Property tests** per Section 11. Located in `tests/Elastic.FSharp.Query.Tests/Properties/`.
- **Golden-file tests** for each `L-N` lowering rule. Fixtures in `tests/golden/`. Generated from canonical inputs; diff in PR.
- **Negative compilation tests** for each `(CT)` invariant. Using FSharp.Compiler.Service to attempt compilation and assert failure with expected diagnostic.
- **Integration tests** against Testcontainers ES 8.x. Located in `tests/Elastic.FSharp.Query.Tests/Integration/`. Runs every property test's output through `_validate/query?explain=true` and asserts no error.
- **Mutation testing** via Stryker.NET on the lowering pass. Target ≥ 85% mutation score on `Query.lower`.
- **Performance regression tests** — lowering 10,000 `Predicate<TestSchema>` instances MUST complete in < 200ms.

---

## 13. Implementation phasing

Phase 1. **Core predicate algebra** (week 1)
   - Types from §4.2 and §4.3
   - Operators from §4.4 (Eq/Range/All/Any/Not/Exists)
   - Lowering rules L-1 through L-3
   - No schema bridge yet — field handles constructed via test fixture
   - Tests for E-1 through E-10, P-1 through P-5

Phase 2. **Nested & phantom typing** (week 2)
   - `NestedField<'S, 'NS>` and `NestedScope<'NS>`
   - Lowering rule L-4
   - Tests for E-13, E-26, I-2, I-10

Phase 3. **Text queries** (week 3)
   - `TextMatch` variants and `match'`, `phrase`, `prefix`, `fuzzy`
   - Lowering rule L-5
   - Tests for E-16, I-14

Phase 4. **Schema bridge** (weeks 4–5)
   - Type Provider or Roslyn source generator
   - `Analyzer` and `Normalizer` DUs from settings
   - Multi-field accessor generation
   - Tests for E-12, E-18, E-19, E-27, E-28, E-29

Phase 5. **Advanced text & autocomplete** (week 6)
   - `multiMatch`, `simpleQueryString`, `autocomplete`
   - Lowering rule L-5 extended
   - Tests for E-14, E-15, E-17

Phase 6. **Function score & boosts** (week 7)
   - `Function<'S>` algebra
   - `Query.boostBy`, `Query.withScoreMode`, `Query.withBoostMode`
   - Lowering rule L-7
   - Tests for E-22, E-31, E-34, E-35

Phase 7. **Property tests at scale + ES integration** (week 8)
   - All P-N properties green
   - Testcontainers integration green
   - Golden files committed

Phase 8. **Documentation, examples, migration guide** (week 9)
   - Per-operator docs in XML comments
   - 20 canonical examples ported from existing NEST code
   - Performance benchmarks vs `Elastic.Clients.Elasticsearch`

---

## 14. Module structure

```
src/Elastic.FSharp.Query/
├── Elastic.FSharp.Query.fsproj
├── Types/
│   ├── FieldKinds.fs                 // phantom kind types
│   ├── FieldHandles.fs               // Field<'S,'K> and specialisations
│   ├── Values.fs                     // FieldValue, DateValue, DateMath, etc.
│   ├── Predicate.fs                  // Predicate<'S> DU
│   ├── Query.fs                      // Query<'S>, Function<'S>
│   └── Errors.fs                     // ConstructionError, EsError
├── Operators/
│   ├── Equality.fs                   // =. and overloads
│   ├── Comparison.fs                 // >. <. between
│   ├── Composition.fs                // &&. ||. not'
│   ├── Text.fs                       // match' phrase prefix fuzzy autocomplete
│   ├── MultiField.fs                 // multiMatch simpleQueryString
│   └── NestedDsl.fs                  // nested combinator
├── Lowering/
│   ├── Context.fs                    // RequestContext, ScoreMode dispatch
│   ├── Equality.fs                   // L-1
│   ├── Range.fs                      // L-2
│   ├── Boolean.fs                    // L-3 (flatten, fold negation)
│   ├── Nested.fs                     // L-4
│   ├── Text.fs                       // L-5
│   ├── Envelope.fs                   // L-6 (Filter+RankBy → function_score/bool)
│   ├── Functions.fs                  // L-7
│   └── Lower.fs                      // entry point: Query.lower / lowerSafe
├── Schema/
│   ├── SchemaContract.fs             // interfaces consumed by schema bridge
│   └── (Provider lives in Elastic.FSharp.Schema, separate project)
└── PublicApi.fs                      // module Query = …, operator exports

tests/Elastic.FSharp.Query.Tests/
├── Lowering/                         // unit tests per L-N
├── Properties/                       // FsCheck per P-N
├── EdgeCases/                        // one file per E-N group
├── NegativeCompilation/              // CT invariants
├── Integration/                      // Testcontainers ES
└── Golden/                           // fixtures
```

---

## 15. F# style requirements

- **All record types `[<Struct>]` where ≤ 4 fields and all primitives.** Reduces allocation pressure in hot paths.
- **No `obj`, no `dynamic`, no reflection at runtime** in non-codec paths.
- **No `mutable`** outside FsCheck generator state and benchmark harnesses.
- **No partial active patterns** in lowering (forces total dispatch).
- **`[<RequireQualifiedAccess>]` on every DU.**
- **Curry over tuples** by default; tuples only when arguments form a semantic pair.
- **`Result.bind` over computation expressions** in lowering pass (predictable allocation).
- Width: 100 columns. Indent: 4 spaces.
- **No mutual recursion across files.** Mutual recursion within a file via `and` is acceptable if necessary; prefer non-recursive refactoring.

---

## 16. Public API surface (the user-visible contract)

```fsharp
namespace Elastic.FSharp.Query

[<AutoOpen>]
module Operators =
    val (=.)  : FieldRef<'S> -> 'V -> Predicate<'S>            // multiple overloads
    val (&&.) : Predicate<'S> -> Predicate<'S> -> Predicate<'S>
    val (||.) : Predicate<'S> -> Predicate<'S> -> Predicate<'S>
    val (>.)  : OrderableField<'S, 'V> -> 'V -> Predicate<'S>
    val (<.)  : OrderableField<'S, 'V> -> 'V -> Predicate<'S>
    val (>=.) : OrderableField<'S, 'V> -> 'V -> Predicate<'S>
    val (<=.) : OrderableField<'S, 'V> -> 'V -> Predicate<'S>
    val (*^)  : FieldRef<'S> -> float32 -> BoostedField<'S>

module Predicate =
    val exists       : FieldRef<'S> -> Predicate<'S>
    val missing      : FieldRef<'S> -> Predicate<'S>
    val nested       : NestedField<'S,'NS> -> (NestedScope<'NS> -> Predicate<'NS>) -> Predicate<'S>
    val anyOf        : Predicate<'S> list -> Predicate<'S>
    val allOf        : Predicate<'S> list -> Predicate<'S>
    val not'         : Predicate<'S> -> Predicate<'S>
    val notIn        : FieldValue list -> FieldRef<'S> -> Predicate<'S>
    val isOneOf      : FieldValue list -> FieldRef<'S> -> Predicate<'S>
    val match'       : TextField<'S> -> string -> Predicate<'S>
    val matchWith    : TextField<'S> -> string -> MatchParams -> Predicate<'S>
    val phrase       : string -> TextField<'S> -> Predicate<'S>
    val prefix       : string -> TextField<'S> -> Predicate<'S>
    val fuzzy        : Fuzziness -> string -> TextField<'S> -> Predicate<'S>
    val multiMatch   : BoostedField<'S> list -> string -> MultiMatchParams -> Predicate<'S>
    val simpleQueryString : BoostedField<'S> list -> string -> SimpleQueryStringParams -> Predicate<'S>
    val autocomplete : EdgeNgramField<'S> -> string -> ?searchAnalyzer:Analyzer -> Predicate<'S>
    val rawEs        : QueryContainer -> Predicate<'S>           // escape hatch

module Function =
    val fieldValueFactor : NumericField<'S,_> -> FvfParams -> Function<'S>
    val gaussDecay       : DateField<'S> -> DecayParams -> Function<'S>
    val expDecay         : DateField<'S> -> DecayParams -> Function<'S>
    val linearDecay      : DateField<'S> -> DecayParams -> Function<'S>
    val filterBoost      : Predicate<'S> -> float -> Function<'S>
    val weight           : float -> Function<'S>
    val randomScore      : ?seed:int64 -> ?field:KeywordField<'S> -> unit -> Function<'S>

module Query =
    val where'           : Predicate<'S> -> Query<'S>
    val rankBy           : Predicate<'S> -> Query<'S> -> Query<'S>
    val boostBy          : Function<'S> list -> Query<'S> -> Query<'S>
    val withScoreMode    : ScoreMode -> Query<'S> -> Query<'S>
    val withBoostMode    : BoostMode -> Query<'S> -> Query<'S>
    val withMinScore     : float -> Query<'S> -> Query<'S>
    val explain          : Query<'S> -> Query<'S>
    val lower            : Schema -> RequestContext -> Query<'S> -> QueryContainer
    val lowerSafe        : Schema -> RequestContext -> Query<'S> -> Result<QueryContainer, ConstructionError>
    val render           : Schema -> RequestContext -> Query<'S> -> string
    val renderSafe       : Schema -> RequestContext -> Query<'S> -> Result<string, ConstructionError>
```

---

## 17. Acceptance criteria

The implementation is **complete** when:

1. All sections §4, §5, §6 are implemented with corresponding tests green.
2. All E-1 through E-36 have passing tests.
3. All P-1 through P-10 properties hold for ≥ 10,000 generated cases each.
4. Integration suite against Testcontainers ES 8.15 passes.
5. Mutation score on `Lowering/` is ≥ 85%.
6. Performance regression test (§12) passes.
7. The canonical Shipments example query (separate document) round-trips correctly through `lower → encode → decode → lower` (modulo the alpha-equivalence implied by `Should` set semantics).
8. Public API surface §16 has XML doc comments on every exported value.
9. Module structure §14 matches the implementation.
10. F# style requirements §15 are enforced by Fantomas configuration + an `EditorConfig` checked in.

---

## 18. Open questions (to resolve before Phase 1 starts)

Q-1. Should `Query.lower` accept `Schema` and `RequestContext` as ambient (reader monad) or explicit arguments? Recommendation: explicit; F# culture disfavors reader monads at this layer.

Q-2. Should `Predicate<'S>` support runtime-fields predicates (`runtime_mappings` per query)? Recommendation: defer to v2; add `Query.withRuntimeFields` as a stub now.

Q-3. Should `RawEs` carry a `name:string` annotation for telemetry? Recommendation: yes; emit it as a Writer-monad log line during lowering.

Q-4. How are aliases (mapping aliases like `"user.name" → "user_name"`) handled? Recommendation: canonicalised by schema bridge; `Predicate<'S>` uses canonical names exclusively.

Q-5. Should the lowering pass have a separately exposed *unfused* form (before `flatten` and `fold-not-not`) for downstream optimizers? Recommendation: yes, expose as `Query.lowerRaw`; current `Query.lower` is `lowerRaw |> flatten |> foldNot`.

Q-6. Cross-cutting: should `RequestContext` carry an `explain:bool` that injects `explain: true` at query level? Or is that a `Query<'S>` field? Recommendation: `Query<'S>` field (§4.3 already has it).

Q-7. Versioning: when ES changes Query DSL semantics across majors, do we bump Level 3 major version too? Recommendation: yes; Level 3 ties to Level 1 generator's supported ES range.

---

## 19. References

- `elasticsearch-specification` repository (Apache 2.0). Spec source of truth for Level 1.
- ES Query DSL reference (Elastic docs), pinned to ES version targeted by Level 1.
- FsCodec (or equivalent) JSON codec documentation.
- FsCheck property testing manual.
- Existing project documents (not reproduced):
  - "Refined: phantom-typed refinement library for F#"
  - "OracleSchemaProvider / ElasticsearchProvider / LineageProvider triangle"
  - "F#-style discipline in C# (sealed records, DUs, Result/Option, parse-don't-validate)"

---

**End of specification.**