namespace SqlHydra.DuckDb

open System.Data
open DuckDB.NET.Data

type ColumnMeta = { Name: string; DataType: string; IsNullable: bool }
type TableMeta = { Name: string; Columns: ColumnMeta list }

module Schema =
    let getTables (connectionString: string) : TableMeta list =
        use conn = new DuckDBConnection(connectionString)
        conn.Open()
        
        use cmd = conn.CreateCommand()
        cmd.CommandText <- "SELECT table_name, column_name, data_type, is_nullable FROM information_schema.columns"
        use reader = cmd.ExecuteReader()
        
        let rec readRows acc =
            if reader.Read() then
                let tableName = reader.GetString(0)
                let colName = reader.GetString(1)
                let dataType = reader.GetString(2)
                let isNullable = reader.GetString(3) = "YES"
                readRows ((tableName, { Name = colName; DataType = dataType; IsNullable = isNullable }) :: acc)
            else acc
            
        readRows []
        |> List.groupBy fst
        |> List.map (fun (t, cols) -> { Name = t; Columns = cols |> List.map snd |> List.rev })
