module SqlHydra.Query.ColumnReaders

open System
open System.Data.Common

let readRequired<'T> (reader: DbDataReader) (ordinal: int) : obj =
    reader.GetFieldValue<'T>(ordinal) |> box

let readOption<'T> (reader: DbDataReader) (ordinal: int) : obj =
    if reader.IsDBNull(ordinal) then box None
    else reader.GetFieldValue<'T>(ordinal) |> Some |> box

let readNullableStruct<'T when 'T : struct and 'T :> ValueType and 'T : (new: unit -> 'T)> (reader: DbDataReader) (ordinal: int) : obj =
    if reader.IsDBNull(ordinal) then Nullable() |> box
    else Nullable(reader.GetFieldValue<'T>(ordinal)) |> box

let readNullableObj<'T when 'T : not struct> (reader: DbDataReader) (ordinal: int) : obj =
    if reader.IsDBNull(ordinal) then null |> box
    else reader.GetFieldValue<'T>(ordinal) |> box
