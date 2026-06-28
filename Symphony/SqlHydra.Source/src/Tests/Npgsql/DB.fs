module Npgsql.DB

#if NET8_0
open Npgsql.AdventureWorksNet8
#endif
#if NET9_0
open Npgsql.AdventureWorksNet9
#endif
#if NET10_0
open Npgsql.AdventureWorksNet10
#endif

#if DOCKERHOST // devcontainer
let connectionString = @"Server=npgsql;Port=5432;Database=Adventureworks;User Id=postgres;Password=postgres;Timeout=3"
#else
let connectionString = @"Server=localhost;Port=54320;Database=Adventureworks;User Id=postgres;Password=postgres;Timeout=3"
#endif

let private emitter = SqlHydra.Query.PostgresEmitter() :> SqlHydra.Query.ISqlEmitter

let toSql (query: SqlHydra.Query.SelectQuery) =
    let sql = (query.CompileWith(emitter)).Sql
    #if DEBUG
    printfn "toSql: %s" sql
    #endif
    sql

let toUpdateSql (query: SqlHydra.Query.UpdateQuery<_, _>) =
    let ir = SqlHydra.Query.QueryUtils.fromUpdate query.Spec
    let sql = (emitter.EmitUpdate(ir)).Sql
    #if DEBUG
    printfn "toSql: %s" sql
    #endif
    sql

let toInsertSql (query: SqlHydra.Query.InsertQuery<_, _>) =
    let ir = SqlHydra.Query.QueryUtils.fromInsert query.Spec
    let baseSql = (emitter.EmitInsert(ir)).Sql
    let sql =
        match query.Spec.IdentityField with
        | Some field -> baseSql + $" RETURNING \"{field}\";"
        | None -> baseSql
    #if DEBUG
    printfn "toSql: %s" sql
    #endif
    sql
