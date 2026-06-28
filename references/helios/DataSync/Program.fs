namespace DataSync.App

open DataSync.Data
open DataSync.Infrastructure

module Program =
    [<EntryPoint>]
    let main args =
        // Composition Root: Wire up dependencies
        let connectionString = @"Data Source=E:\github\Adventureworks\gem\northwind\northwind.db;Mode=ReadOnly;"
        let esUrl = "http://localhost:9200"

        printfn "Starting Northwind to Elasticsearch Sync (Idiomatic F#)..."

        // Execute the workflow using Result-oriented programming
        let workflowResult =
            DataAccess.getOrders connectionString
            |> Result.bind (fun orders -> 
                printfn $"Successfully read and validated {orders.Length} orders from SQLite."
                ElasticManager.indexData esUrl orders
            )

        // Handle the final outcome at the edge of the application
        match workflowResult with
        | Ok () ->
            printfn "Sync completed successfully."
            0
        | Error msg ->
            printfn "Sync failed with error: %s" msg
            1
