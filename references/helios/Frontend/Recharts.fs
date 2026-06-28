module Recharts

open Fable.Core
open Fable.Core.JsInterop
open Fable.React

let inline ResponsiveContainer (props: obj) (children: ReactElement list) : ReactElement =
    ofImport "ResponsiveContainer" "recharts" props children

let inline BarChart (props: obj) (children: ReactElement list) : ReactElement =
    ofImport "BarChart" "recharts" props children

let inline Bar (props: obj) : ReactElement =
    ofImport "Bar" "recharts" props []

let inline XAxis (props: obj) : ReactElement =
    ofImport "XAxis" "recharts" props []

let inline YAxis (props: obj) : ReactElement =
    ofImport "YAxis" "recharts" props []

let inline CartesianGrid (props: obj) : ReactElement =
    ofImport "CartesianGrid" "recharts" props []

let inline Tooltip (props: obj) : ReactElement =
    ofImport "Tooltip" "recharts" props []

let inline Legend (props: obj) : ReactElement =
    ofImport "Legend" "recharts" props []
