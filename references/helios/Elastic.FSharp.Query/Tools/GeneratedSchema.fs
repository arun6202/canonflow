namespace Elastic.FSharp.Query
open Elastic.FSharp.Query.Types

module NorthwindSchema =
    type Northwind = class end

    type Analyzer =
        | Autocomplete
        | StandardEnglish
        member this.Value = match this with | Autocomplete -> "autocomplete" | StandardEnglish -> "standard_english"

    type Normalizer =
        | LowercaseNormalizer
        member this.Value = match this with | LowercaseNormalizer -> "lowercase_normalizer"

    type OrdersSchema = class end

    let CustomerId : KeywordField<Northwind> = { Path = "customer_id"; Kind = Unchecked.defaultof<_> }
    let Name : TextWithKeywordField<Northwind> = { Path = "name"; Kind = Unchecked.defaultof<_> }
    let CreatedAt : DateField<Northwind> = { Path = "created_at"; Kind = Unchecked.defaultof<_> }
    let TotalSpend : NumericField<Northwind, unit> = { Path = "total_spend"; Kind = Unchecked.defaultof<_> }
    let IsActive : BoolField<Northwind> = { Path = "is_active"; Kind = Unchecked.defaultof<_> }
    let Orders : NestedField<Northwind, OrdersSchema> = { Path = "orders"; Kind = Unchecked.defaultof<_> }
    let OrderId : KeywordField<OrdersSchema> = { Path = "orders.order_id"; Kind = Unchecked.defaultof<_> }
    let Amount : NumericField<OrdersSchema, unit> = { Path = "orders.amount"; Kind = Unchecked.defaultof<_> }
