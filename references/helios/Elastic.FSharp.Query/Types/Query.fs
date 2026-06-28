namespace Elastic.FSharp.Query.Types

type Query<'S> = {
    Filter        : Predicate<'S>
    RankBy        : Predicate<'S> option
    Aggs          : (string * Agg<'S>) list
}
