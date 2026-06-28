module SqlServer.DB

#if NET8_0
open SqlServer.AdventureWorksNet8
#endif
#if NET9_0
open SqlServer.AdventureWorksNet9
#endif
#if NET10_0
open SqlServer.AdventureWorksNet10
#endif

#if DOCKERHOST // devcontainer
let server = "mssql"
#else
let server = "localhost,12019"
#endif

let connectionString = $@"Server={server};Database=AdventureWorks;User=sa;Password=Password#123;Connect Timeout=3;TrustServerCertificate=True"
let db = QueryContextFactory.Create(connectionString, printf "SQL: %O")

let emitter = SqlHydra.Query.SqlServerEmitter() :> SqlHydra.Query.ISqlEmitter

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
        | Some _ -> baseSql + ";SELECT scope_identity() as Id"
        | None -> baseSql
    #if DEBUG
    printfn "toSql: %s" sql
    #endif
    sql

