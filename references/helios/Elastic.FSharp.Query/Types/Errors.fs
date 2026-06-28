namespace Elastic.FSharp.Query.Types

type ConstructionError =
    | EmptyFieldList of context:string
    | IncompatibleParams of reason:string
    | UnresolvedFieldAlias of alias:string
    | UnknownAnalyzer of name:string * indexedAnalyzers:string list
    | RawEsDecodeFailure of inner:exn

// Simple RequestContext for Phase 1
type RequestContext = {
    TimeZone : string
}
