module SqlServer.``Query Nullable Integration Tests``

open SqlHydra.Query
open DB
open System
open NUnit.Framework
open System.Threading.Tasks
open Swensen.Unquote
#if NET8_0
open SqlServer.AdventureWorksNullableNet8
#endif
#if NET9_0
open SqlServer.AdventureWorksNullableNet9
#endif
#if NET10_0
open SqlServer.AdventureWorksNullableNet10
#endif

let stubbedErrorLog = 
    {
        dbo.ErrorLog.ErrorLogID = 0 // Exclude
        dbo.ErrorLog.ErrorTime = System.DateTime.Now
        dbo.ErrorLog.ErrorLine = Nullable()
        dbo.ErrorLog.ErrorMessage = "TEST INSERT ASYNC"
        dbo.ErrorLog.ErrorNumber = 400
        dbo.ErrorLog.ErrorProcedure = "Procedure 400"
        dbo.ErrorLog.ErrorSeverity = Nullable()
        dbo.ErrorLog.ErrorState = Nullable()
        dbo.ErrorLog.UserName = "jmarr"
    }

[<Test>]
let ``Select Column Aggregates From Product IDs 1-3``() = task {
    let! aggregates =
        selectTask db {
            for p in Production.Product do
            where (isNotNullValue p.ProductSubcategoryID)
            groupBy p.ProductSubcategoryID
            where (p.ProductSubcategoryID.Value |=| [ 1; 2; 3 ])
            select (p.ProductSubcategoryID, minBy p.ListPrice, maxBy p.ListPrice, avgBy p.ListPrice, countBy p.ListPrice, sumBy p.ListPrice)
        }

    gt0 aggregates
            
    let aggByCatID = 
        aggregates 
        |> Seq.map (fun (catId, minPrice, maxPrice, avgPrice, priceCount, sumPrice) -> catId.Value, (minPrice, maxPrice, avgPrice, priceCount, sumPrice)) 
        |> Map.ofSeq

    Assert.AreEqual((539.99M, 3399.99M, 1683.365M, 32, 53867.6800M), aggByCatID.[1], "Expected CatID: 1 aggregates to match.")
    Assert.AreEqual((539.99M, 3578.2700M, 1597.4500M, 43, 68690.3500M), aggByCatID.[2], "Expected CatID: 2 aggregates to match.")
    Assert.AreEqual((742.3500M, 2384.0700M, 1425.2481M, 22, 31355.4600M), aggByCatID.[3], "Expected CatID: 3 aggregates to match.")
}

[<Test>]
let ``Select Column Aggregates``() = task {
    let! aggregates =
        selectTask db {
            for p in Production.Product do
            where (isNotNullValue p.ProductSubcategoryID)
            groupBy p.ProductSubcategoryID
            having (minBy p.ListPrice > 50M && maxBy p.ListPrice < 1000M)
            select (p.ProductSubcategoryID, minBy p.ListPrice, maxBy p.ListPrice)
        }

    gt0 aggregates
}

[<Test>]
let ``Sorted Aggregates - Top 5 categories with highest avg price products``() = task {
    let! aggregates =
        selectTask db {
            for p in Production.Product do
            where (p.ProductSubcategoryID.HasValue = true)
            groupBy p.ProductSubcategoryID
            orderByDescending (avgBy p.ListPrice)
            select (p.ProductSubcategoryID, avgBy p.ListPrice)
            take 5
        }

    gt0 aggregates
}

[<Test>]
let ``Where subqueryMany``() = task {
    let top5CategoryIdsWithHighestAvgPrices =
        select {
            for p in Production.Product do
            where (isNotNullValue p.ProductSubcategoryID)
            groupBy p.ProductSubcategoryID
            orderByDescending (avgBy p.ListPrice)
            select (p.ProductSubcategoryID)
            take 5
        }

    let! top5Categories =
        selectTask db {
            for c in Production.ProductCategory do
            where (Nullable c.ProductCategoryID |=| subqueryMany top5CategoryIdsWithHighestAvgPrices)
            select c.Name
        }

    gt0 top5Categories
}

[<Test>]
let ``Select Columns with Option``() = task {
    let! values =
        selectTask db {
            for p in Production.Product do
            where (p.ProductSubcategoryID.HasValue)
            select (p.ProductSubcategoryID, p.ListPrice)
        }

    gt0 values
    Assert.IsTrue(values |> Seq.forall (fun (catId, price) -> catId.HasValue), "Expected subcategories to all have a value.")
}

[<Test>]
let ``InsertGetId Test``() = task {
    let errorLog = 
        {
            dbo.ErrorLog.ErrorLogID = 0 // Exclude
            dbo.ErrorLog.ErrorTime = System.DateTime.Now
            dbo.ErrorLog.ErrorLine = Nullable()
            dbo.ErrorLog.ErrorMessage = "TEST"
            dbo.ErrorLog.ErrorNumber = 400
            dbo.ErrorLog.ErrorProcedure = "Procedure 400"
            dbo.ErrorLog.ErrorSeverity = Nullable()
            dbo.ErrorLog.ErrorState = Nullable()
            dbo.ErrorLog.UserName = "jmarr"
        }

    let! errorLogId = 
        insertTask db {
            for e in dbo.ErrorLog do
            entity errorLog
            getId e.ErrorLogID
        }

    errorLogId >! 0
}

[<Test>]
let ``InsertGetIdAsync Test``() = task {
    let errorLog = 
        {
            dbo.ErrorLog.ErrorLogID = 0 // Exclude
            dbo.ErrorLog.ErrorTime = System.DateTime.Now
            dbo.ErrorLog.ErrorLine = Nullable()
            dbo.ErrorLog.ErrorMessage = "TEST INSERT ASYNC"
            dbo.ErrorLog.ErrorNumber = 400
            dbo.ErrorLog.ErrorProcedure = "Procedure 400"
            dbo.ErrorLog.ErrorSeverity = Nullable()
            dbo.ErrorLog.ErrorState = Nullable()
            dbo.ErrorLog.UserName = "jmarr"
        }

    let! result = 
        insertTask db {
            for e in dbo.ErrorLog do
            entity errorLog
            getId e.ErrorLogID
        }

    result >! 0
}

[<Test>]
let ``Update Set Individual Fields``() = task {
    use! shared = db.OpenContextAsync()
        
    let! row = 
        selectTask shared {
            for e in dbo.ErrorLog do
            head
        }

    let! result = 
        updateTask shared {
            for e in dbo.ErrorLog do
            set e.ErrorNumber 123
            set e.ErrorMessage "ERROR #123"
            set e.ErrorLine 999
            set e.ErrorProcedure null
            where (e.ErrorLogID = row.ErrorLogID)
        }

    result =! 1
}

[<Test>]
let ``UpdateAsync Set Individual Fields``() = task {
    use! shared = db.OpenContextAsync()

    let! row = 
        selectTask shared {
            for e in dbo.ErrorLog do
            head
        }

    let! result = 
        updateTask shared {
            for e in dbo.ErrorLog do
            set e.ErrorNumber (row.ErrorNumber + 1)
            set e.ErrorProcedure null
            where (e.ErrorLogID = row.ErrorLogID)
        }

    result =! 1
}

[<Test>]
let ``Update Entity``() = task {
    use! shared = db.OpenContextAsync()
        
    let! row = 
        selectTask shared {
            for e in dbo.ErrorLog do
            head
        }

    row.ErrorTime <- System.DateTime.Now
    row.ErrorLine <- 888
    row.ErrorMessage <- "ERROR #2"
    row.ErrorNumber <- 500
    row.ErrorProcedure <- null
    row.ErrorSeverity <- Nullable()
    row.ErrorState <- Nullable()
    row.UserName <- "jmarr"

    let! result = 
        updateTask shared {
            for e in dbo.ErrorLog do
            entity row
            excludeColumn e.ErrorLogID
            where (e.ErrorLogID = row.ErrorLogID)
        }

    result =! 1
}

    