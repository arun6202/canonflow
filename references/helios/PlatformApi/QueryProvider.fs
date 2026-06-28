namespace PlatformApi.Engine

open SharedDomain.Dtos

type IQueryProvider =
    abstract member SearchDocuments: predicate:ClientPredicate -> Async<Result<OrderLineDocumentDto seq, string>>
    abstract member ExecuteAnalytics: filter:ClientPredicate option * aggs:ClientAggregation list -> Async<Result<AnalyticsResponseDto seq, string>>
