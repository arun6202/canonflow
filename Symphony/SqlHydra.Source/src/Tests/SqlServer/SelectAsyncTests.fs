module SqlServer.``selectAsync Tests``

open SqlHydra.Query
open Swensen.Unquote
open NUnit.Framework
open DB

#if NET8_0
open SqlServer.AdventureWorksNet8
#endif
#if NET9_0
open SqlServer.AdventureWorksNet9
#endif
#if NET10_0
open SqlServer.AdventureWorksNet10
#endif


[<Test>]
let ``selectAsync - no select``() = async {    
    let! results = 
        selectAsync db {
            for o in Sales.SalesOrderHeader do
            join d in Sales.SalesOrderDetail on (o.SalesOrderID = d.SalesOrderID)
            take 10
            mapArray $"{o.SalesOrderNumber} - {d.LineTotal} - {d.ModifiedDate.ToShortDateString()}"
            //select $"{o.SalesOrderNumber} - {d.LineTotal} - {d.ModifiedDate.ToShortDateString()}"
            //toArray
        }
        
    gt0 results
}

[<Test>]
let ``selectAsync - select p``() = async {
    let! results = 
        selectAsync db {
            for p in Person.Person do
            take 10
            select p
        }
        
    gt0 results
}

[<Test>]
let ``selectAsync - toArray``() = async {
    let! results = 
        selectAsync db {
            for p in Person.Person do
            take 10
            select p
            toArray
        }
        
    gt0 results
}

[<Test>]
let ``selectAsync - mapList column``() = async {
    let! results = 
        selectAsync db {
            for p in Person.Person do
            take 10
            //mapList p.FirstName
            select p.FirstName
            toList
        }
        
    gt0 results
}

[<Test>]
let ``selectAsync - select entity - mapSeq column``() = async {
    let! results = 
        selectAsync db {
            for p in Person.Person do
            take 10
            //mapSeq $"{p.FirstName} {p.LastName}"
            select $"{p.FirstName} {p.LastName}"
        }
        
    gt0 results
}

[<Test>]
let ``selectAsync - select columns into - mapList column``() = async {
    let! results = 
        selectAsync db {
            for p in Person.Person do
            take 10
            //select (p.FirstName, p.LastName) into (fname, lname)
            //mapList $"{fname} {lname}"
            select $"{p.FirstName} {p.LastName}"
            toList
        }
        
    gt0 results
}

[<Test>]
let ``selectAsync - count``() = async {
    let! results = 
        selectAsync db {
            for p in Person.Person do
            count
        }
        
    results >! 0
}

[<Test>]
let ``selectAsync - tryHead - Selected``() = async {
    let! result = 
        selectAsync db {
            for p in Person.Person do
            take 1
            tryHead
        }
        
    result |> Option.isSome =! true
}

[<Test>]
let ``selectAsync - tryHead - Mapped``() = async {
    let! result = 
        selectAsync db {
            for p in Person.Person do
            take 1
            //mapSeq $"{p.FirstName} {p.LastName}"
            select $"{p.FirstName} {p.LastName}"
            tryHead
        }
        
    result |> Option.isSome =! true
}

