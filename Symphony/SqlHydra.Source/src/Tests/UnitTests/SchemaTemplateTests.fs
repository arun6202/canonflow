module UnitTests.SchemaTemplateTests

open NUnit.Framework
open SqlHydra.Domain
open SqlHydra

// These tests are commented out until Generation tests are reworked for v4.

//[<Test>]
//let ``Schema Template Test - Npgsql`` () =
//    let cfg =
//        { Npgsql.Generation.cfg with
//            TableDeclarations = true
//        }
//    let info = Npgsql.Provider.provider
//    let schema = Npgsql.NpgsqlSchemaProvider.getSchema (cfg, false)
//    let version = Version.get()
//    let output = SchemaTemplate.generate cfg info schema version
//    printfn $"Output:\n{output}"

//[<Test>]
//let ``Schema Template Test - SqlServer`` () =
//    let cfg =
//        { SqlServer.Generation.cfg with
//            TableDeclarations = true
//            Filters =
//                {
//                    Includes = [ "*" ]
//                    Excludes = [ "*/v*" ]
//                    Restrictions = Map.empty
//                }
//        }
//    let info = SqlServer.Provider.provider
//    let schema = SqlServer.SqlServerSchemaProvider.getSchema (cfg, false)
//    let version = Version.get()
//    let output = SchemaTemplate.generate cfg info schema version
//    printfn $"Output:\n{output}"
