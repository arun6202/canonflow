module SqlHydra.Program

open System
open FSharp.SystemCommandLine
open Input
open Console
open Domain

let run (provider: ISqlHydraDbProvider) (tomlFile: IO.FileInfo option, project: IO.FileInfo, connString: string option) =
    let tomlFile = defaultArg tomlFile (IO.FileInfo($"sqlhydra-{provider.Id}.toml"))

    {
        Provider = provider
        TomlFile = tomlFile
        Project = project
        Version = Version.get()
        ConnectionString = connString
    }
    |> Console.run

[<AutoOpen>]
module Options =
    let tomlFile =
        optionMaybe<IO.FileInfo> "--toml-file"
        |> alias "-t"
        |> desc "The toml configuration filename. Default: 'sqlhydra-{provider}.toml'"

    let project =
        option<IO.FileInfo> "--project"
        |> alias "-p"
        |> desc "The project file to update. If not configured, the first .fsproj found in the run directory will be used."
        |> defaultValueFactory (fun _ ->
            IO.DirectoryInfo(".").EnumerateFiles("*.fsproj") 
            |> Seq.tryHead
            |> Option.defaultWith (fun () -> failwith "Unable to find a .fsproj file in the run directory. Please specify one using the `--project` option.")
        )

    let connectionString =
        optionMaybe<string> "--connection-string"
        |> alias "-cs"
        |> desc "The DB connection string to use. This will override the connection string in the toml file."

[<AutoOpen>]
module Commands = 
    let mssql = 
        command "mssql" {
            description "Use the built-in SQL Server provider."
            inputs (tomlFile, project, connectionString)
            setAction (run SqlServer.Provider.instance)
        }

    let npgsql = 
        command "npgsql" {
            description "Use the built-in PostgreSQL provider."
            inputs (tomlFile, project, connectionString)
            setAction (run Npgsql.Provider.instance)
        }

    let sqlite = 
        command "sqlite" {
            description "Use the built-in SQLite provider."
            inputs (tomlFile, project, connectionString)
            setAction (run Sqlite.Provider.instance)
        }

    let mysql = 
        command "mysql" {
            description "Use the built-in MySQL provider."
            inputs (tomlFile, project, connectionString)
            setAction (run MySql.Provider.instance)
        }

    let oracle =
        command "oracle" {
            description "Use the built-in Oracle provider."
            inputs (tomlFile, project, connectionString)
            setAction (run Oracle.Provider.instance)
        }

    let custom =
        let providerName = 
            argument<string> "providerName" 
            |> desc "The name used to locate your custom provider. This is usually the name of the project, ProjectReference, or PackageReference that contains your provider implementation."

        command "custom" {
            description "Use a custom provider implemented in the target project or its referenced assemblies."
            inputs (providerName, tomlFile, project, connectionString)
            setAction (fun (providerName, tomlFile, project, connectionString) -> 
                let provider = Extensions.loadProvider project providerName
                run (provider) (tomlFile, project, connectionString)
            )
        }

[<EntryPoint>]
let main argv =
    rootCommand argv {
        description "SqlHydra.Cli"
        inputs context
        helpAction
        addCommands [ mssql; npgsql; sqlite; mysql; oracle; custom ]
    }
