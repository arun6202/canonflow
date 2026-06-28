module Oracle.DB

#if NET8_0
open Oracle.AdventureWorksNet8
#endif
#if NET9_0
open Oracle.AdventureWorksNet9
#endif
#if NET10_0
open Oracle.AdventureWorksNet10
#endif

let connectionString = @"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=127.0.0.1)(PORT=1521)) (CONNECT_DATA=(SERVICE_NAME=XEPDB1))); User Id=OT;Password=Oracle1;"
let db = QueryContextFactory.Create(connectionString, printf "SQL: %O")

let private emitter = SqlHydra.Query.OracleEmitter() :> SqlHydra.Query.ISqlEmitter

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
    let sql = (emitter.EmitInsert(ir)).Sql
    #if DEBUG
    printfn "toSql: %s" sql
    #endif
    sql
