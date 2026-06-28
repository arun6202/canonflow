#r "nuget: Elastic.Clients.Elasticsearch, 8.14.0"
open Elastic.Clients.Elasticsearch
open Elastic.Transport
open Elastic.Transport.Products.Elasticsearch

let c = ElasticsearchClient()
let body = """{ "settings": { "number_of_shards": 1 } }"""
let pd = PostData.String(body)
let res = c.Transport.Request<StringResponse>(HttpMethod.PUT, "orders", pd)
printfn "%A" res
