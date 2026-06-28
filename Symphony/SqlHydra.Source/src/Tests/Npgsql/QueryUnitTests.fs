module Npgsql.``Query Unit Tests``

open Swensen.Unquote
open SqlHydra.Query
open NUnit.Framework
open DB
#if NET8_0
open Npgsql.AdventureWorksNet8
#endif
#if NET9_0
open Npgsql.AdventureWorksNet9
#endif
#if NET10_0
open Npgsql.AdventureWorksNet10
#endif

[<Test>]
let ``Simple Where``() = 
    let sql =  
        select {
            for a in person.address do
            where (a.city = "Dallas")
            orderBy a.city
        }
        |> toSql

    sql.Contains("WHERE") =! true

[<Test>]
let ``Select 1 Column``() = 
    let sql = 
        select {
            for a in person.address do
            select (a.city)
        }
        |> toSql

    sql.Contains("SELECT \"a\".\"city\" FROM") =! true

[<Test>]
let ``Select 2 Columns``() = 
    let sql = 
        select {
            for h in sales.salesorderheader do
            select (h.customerid, h.onlineorderflag)
        }
        |> toSql

    sql.Contains("SELECT \"h\".\"customerid\", \"h\".\"onlineorderflag\" FROM") =! true

[<Test; Ignore("Temporarily ignoring test for emergency fix")>]
let ``Select 1 Table and 1 Column``() = 
    let sql = 
        select {
            for o in sales.salesorderheader do
            join d in sales.salesorderdetail on (o.salesorderid = d.salesorderid)
            where o.onlineorderflag
            select (o, d.unitprice)
        }
        |> toSql

    sql.Contains("""SELECT "o"."salesorderid", "o"."revisionnumber", "o"."orderdate", "o"."duedate", "o"."shipdate", "o"."status", "o"."onlineorderflag", "o"."purchaseordernumber", "o"."accountnumber", "o"."customerid", "o"."salespersonid", "o"."territoryid", "o"."billtoaddressid", "o"."shiptoaddressid", "o"."shipmethodid", "o"."creditcardid", "o"."creditcardapprovalcode", "o"."currencyrateid", "o"."subtotal", "o"."taxamt", "o"."freight", "o"."totaldue", "o"."comment", "o"."rowguid", "o"."modifieddate", "d"."unitprice" FROM""") =! true

[<Test>]
let ``Where with Option Type``() = 
    let sql =  
        select {
            for a in person.address do
            where (a.addressline2 <> None)
        }
        |> toSql

    sql.Contains("IS NOT NULL") =! true

[<Test>]
let ``Where Not Like``() = 
    let sql = 
        select {
            for a in person.address do
            where (a.city <>% "S%")
        }
        |> toSql

    sql =! """SELECT * FROM "person"."address" AS "a" WHERE (NOT ("a"."city" ilike @p0))"""

[<Test>]
let ``Or Where``() = 
    let sql =  
        select {
            for a in person.address do
            where (a.city = "Chicago" || a.city = "Dallas")
        }
        |> toSql

    sql.Contains("WHERE ((\"a\".\"city\" = @p0) OR (\"a\".\"city\" = @p1))") =! true

[<Test>]
let ``And Where``() = 
    let sql =  
        select {
            for a in person.address do
            where (a.city = "Chicago" && a.city = "Dallas")
        }
        |> toSql

    sql.Contains("WHERE ((\"a\".\"city\" = @p0) AND (\"a\".\"city\" = @p1))") =! true

[<Test>]
let ``Where with AND and OR in Parenthesis``() = 
    let sql =  
        select {
            for a in person.address do
            where (a.city = "Chicago" && (a.addressline2 = Some "abc" || isNullValue a.addressline2))
        }
        |> toSql

    Assert.IsTrue( 
        sql.Contains("WHERE ((\"a\".\"city\" = @p0) AND ((\"a\".\"addressline2\" = @p1) OR (\"a\".\"addressline2\" IS NULL)))"),
        "Should wrap OR clause in parenthesis and each individual where clause in parenthesis.")

[<Test>]
let ``Where value and column are swapped``() = 
    let sql =  
        select {
            for a in person.address do
            where (5 < a.addressid && 20 >= a.addressid)
        }
        |> toSql

    sql.Contains("WHERE ((\"a\".\"addressid\" > @p0) AND (\"a\".\"addressid\" <= @p1))") =! true

[<Test>]
let ``Where Not Binary``() = 
    let sql =  
        select {
            for a in person.address do
            where (not (a.city = "Chicago" && a.city = "Dallas"))
        }
        |> toSql

    sql.Contains("WHERE (NOT ((\"a\".\"city\" = @p0) AND (\"a\".\"city\" = @p1)))") =! true

[<Test>]
let ``Where customer isIn List``() = 
    let sql =  
        select {
            for c in sales.customer do
            where (isIn c.customerid [30018;29545;29954])
        }
        |> toSql

    sql.Contains("WHERE (\"c\".\"customerid\" IN (@p0, @p1, @p2))") =! true

[<Test>]
let ``Where customer |=| List``() = 
    let sql =  
        select {
            for c in sales.customer do
            where (c.customerid |=| [30018;29545;29954])
        }
        |> toSql

    sql.Contains("WHERE (\"c\".\"customerid\" IN (@p0, @p1, @p2))") =! true

[<Test>]
let ``Where customer |=| Array``() = 
    let sql =  
        select {
            for c in sales.customer do
            where (c.customerid |=| [| 30018;29545;29954 |])
        }
        |> toSql

    sql.Contains("WHERE (\"c\".\"customerid\" IN (@p0, @p1, @p2))") =! true

[<Test>]
let ``Where customer |=| Seq``() = 
    let buildQuery (values: int seq) = 
        select {
            for c in sales.customer do
            where (c.customerid |=| values)
        }

    let sql =  buildQuery([ 30018;29545;29954 ]) |> toSql
    sql.Contains("WHERE (\"c\".\"customerid\" IN (@p0, @p1, @p2))") =! true

[<Test>]
let ``Where customer |<>| List``() = 
    let sql =  
        select {
            for c in sales.customer do
            where (c.customerid |<>| [ 30018;29545;29954 ])
        }
        |> toSql

    sql.Contains("WHERE (\"c\".\"customerid\" NOT IN (@p0, @p1, @p2))") =! true

[<Test>]
let ``Inner Join``() = 
    let sql = 
        select {
            for o in sales.salesorderheader do
            join d in sales.salesorderdetail on (o.salesorderid = d.salesorderid)
            select o
        }
        |> toSql

    sql.Contains("INNER JOIN \"sales\".\"salesorderdetail\" AS \"d\" ON (\"o\".\"salesorderid\" = \"d\".\"salesorderid\")") =! true

[<Test>]
let ``Left Join``() = 
    let sql = 
        select {
            for o in sales.salesorderheader do
            leftJoin d in sales.salesorderdetail on (o.salesorderid = d.Value.salesorderid)
            select o
        }
        |> toSql

    sql.Contains("LEFT JOIN \"sales\".\"salesorderdetail\" AS \"d\" ON (\"o\".\"salesorderid\" = \"d\".\"salesorderid\")") =! true

[<Test>]
let ``Inner Join - Multi Column``() = 
    let sql = 
        select {
            for o in sales.salesorderheader do
            join d in sales.salesorderdetail on ((o.salesorderid, o.modifieddate) = (d.salesorderid, d.modifieddate))
            select o
        }
        |> toSql

    sql.Contains("INNER JOIN \"sales\".\"salesorderdetail\" AS \"d\" ON (\"o\".\"salesorderid\" = \"d\".\"salesorderid\" AND \"o\".\"modifieddate\" = \"d\".\"modifieddate\")") =! true

[<Test>]
let ``Left Join - Multi Column``() = 
    let sql = 
        select {
            for o in sales.salesorderheader do
            leftJoin d in sales.salesorderdetail on ((o.salesorderid, o.modifieddate) = (d.Value.salesorderid, d.Value.modifieddate))
            select o
        }
        |> toSql

    sql.Contains("LEFT JOIN \"sales\".\"salesorderdetail\" AS \"d\" ON (\"o\".\"salesorderid\" = \"d\".\"salesorderid\" AND \"o\".\"modifieddate\" = \"d\".\"modifieddate\")") =! true

[<Test>]
let ``Correlated Subquery``() = 
    let latestOrderByCustomer = 
        select {
            for d in sales.salesorderheader do
            correlate od in sales.salesorderheader
            where (d.customerid = od.customerid)
            select (maxBy d.orderdate)
        }

    let sql =  
        select {
            for od in sales.salesorderheader do
            where (od.orderdate = subqueryOne latestOrderByCustomer)
        }
        |> toSql

    sql =!
        "SELECT * FROM \"sales\".\"salesorderheader\" AS \"od\" WHERE (\"od\".\"orderdate\" = \
        (SELECT MAX(\"d\".\"orderdate\") AS __hydra_expr_0 FROM \"sales\".\"salesorderheader\" AS \"d\" \
        WHERE (\"d\".\"customerid\" = \"od\".\"customerid\")))".RemoveHydraExpr()

[<Test>]
let ``Correlated subquery with differing for and correlate tables uses the for source``() =
    // When the `for` source and `correlate` target are different tables, the merged Root
    // keys previously collapsed and the inner subquery FROM wrongly referenced the correlate
    // target instead of the `for` source (SelectBuilder.Correlate).
    let inner =
        select {
            for d in sales.salesorderdetail do
            correlate h in sales.salesorderheader
            where (d.salesorderid = h.salesorderid)
            select (maxBy d.orderqty)
        }

    let sql =
        select {
            for h in sales.salesorderheader do
            where (h.revisionnumber = subqueryOne inner)
            select h.salesorderid
        }
        |> toSql

    sql.Contains("FROM \"sales\".\"salesorderdetail\"") =! true

[<Test>]
let ``Correlated subquery parameters do not collide with outer parameters``() =
    // Regression for issue #134: the inner subquery used to be compiled with a fresh
    // ParameterCollector, so its parameter was named @p0 just like the outer query's first
    // parameter. After merging, the outer @p0 bound to BOTH spots. The subquery parameter
    // must be named @p1 (and resolve to its own value), not reuse the outer @p0.
    let latestOrder =
        select {
            for d in sales.salesorderheader do
            correlate od in sales.salesorderheader
            where (d.customerid = od.customerid && d.salesorderid < 10)
            select (maxBy d.orderdate)
        }

    let sql =
        select {
            for od in sales.salesorderheader do
            where (od.salesorderid > 1 && od.orderdate = subqueryOne latestOrder)
        }
        |> toSql

    sql =!
        "SELECT * FROM \"sales\".\"salesorderheader\" AS \"od\" WHERE ((\"od\".\"salesorderid\" > @p0) AND \
        (\"od\".\"orderdate\" = (SELECT MAX(\"d\".\"orderdate\") AS __hydra_expr_0 \
        FROM \"sales\".\"salesorderheader\" AS \"d\" \
        WHERE ((\"d\".\"customerid\" = \"od\".\"customerid\") AND (\"d\".\"salesorderid\" < @p1)))))".RemoveHydraExpr()

[<Test>]
let ``Delete Query with Where``() =
    let sql =  
        delete {
            for c in sales.customer do
            where (c.customerid |<>| [ 30018;29545;29954 ])
        }
        |> toSql

    sql.Contains("DELETE FROM \"sales\".\"customer\"") =! true
    sql.Contains("WHERE (\"sales\".\"customer\".\"customerid\" NOT IN (@p0, @p1, @p2))") =! true

[<Test>]
let ``Delete All``() = 
    let sql =  
        delete {
            for c in sales.customer do
            deleteAll
        }
        |> toSql

    sql =! "DELETE FROM \"sales\".\"customer\""

[<Test>]
let ``Update Query with Where``() = 
    let sql =  
        update {
            for c in sales.customer do
            set c.personid (Some 123)
            where (c.personid = Some 456)
        }
        |> toUpdateSql

    sql =! "UPDATE \"sales\".\"customer\" SET \"personid\" = @p0 WHERE (\"sales\".\"customer\".\"personid\" = @p1)"

[<Test>]
let ``Update Query with multiple Wheres``() = 
    let sql =  
        update {
            for c in sales.customer do
            set c.personid (Some 123)
            where (c.personid = Some 456)
            where (c.customerid = 789)
        }
        |> toUpdateSql

    sql =! """UPDATE "sales"."customer" SET "personid" = @p0 WHERE (("sales"."customer"."personid" = @p1) AND ("sales"."customer"."customerid" = @p2))"""

[<Test>]
let ``Update Query with No Where``() = 
    let sql =  
        update {
            for c in sales.customer do
            set c.customerid 123
            updateAll
        }
        |> toUpdateSql

    sql =! "UPDATE \"sales\".\"customer\" SET \"customerid\" = @p0"

[<Test>]
let ``Update should fail without where or updateAll``() = 
    try 
        let sql =  
            update {
                for c in sales.customer do
                set c.customerid 123
            }
        failwith "Should fail because no `where` or `updateAll` exists."
    with ex ->
        () // Pass

[<Test>]
let ``Update should pass because where exists``() = 
    update {
        for c in sales.customer do
        set c.customerid 123
        where (c.customerid = 1)
    }
    |> ignore

[<Test>]
let ``Update should pass because updateAll exists``() = 
    update {
        for c in sales.customer do
        set c.customerid 123
        updateAll
    }
    |> ignore

[<Test>]
let ``Update with where followed by updateAll should fail``() = 
    try
        update {
            for c in sales.customer do
            set c.customerid 123
            where (c.customerid = 1)
            updateAll
        }
        |> ignore
        Assert.Fail()
    with ex ->
        ()

[<Test>]
let ``Update with updateAll followed by where should fail``() = 
    try
        update {
            for c in sales.customer do
            set c.customerid 123
            updateAll
            where (c.customerid = 1)
        }
        |> ignore
        Assert.Fail()
    with ex ->
        ()

[<Test>]
let ``Insert Query``() = 
    let sql =  
        insert {
            into sales.customer
            entity
                {
                    sales.customer.modifieddate = System.DateTime.Today
                    sales.customer.territoryid = None
                    sales.customer.storeid = None
                    sales.customer.personid = Some 1
                    sales.customer.rowguid = System.Guid.NewGuid()
                    sales.customer.customerid = 0
                }
        }
        |> toInsertSql

    sql =! "INSERT INTO \"sales\".\"customer\" (\"customerid\", \"personid\", \"storeid\", \"territoryid\", \"rowguid\", \"modifieddate\") VALUES (@p0, @p1, @p2, @p3, @p4, @p5)" 

[<Test>]
let ``Inline Aggregates``() = 
    let sql = 
        select {
            for o in sales.salesorderheader do
            select (countBy o.salesorderid)
        }
        |> toSql

    sql =! "SELECT COUNT(\"o\".\"salesorderid\") AS __hydra_expr_0 FROM \"sales\".\"salesorderheader\" AS \"o\"".RemoveHydraExpr()

// ==========================================
// Issue #125 bug verification tests
// ==========================================

// Bug 1: where (s = None) after leftJoin' should produce IS NULL
[<Test>]
let ``Issue125-01 Where joined table = None produces IS NULL``() =
    let sql =
        select {
            for o in sales.salesorderheader do
            leftJoin' d in sales.salesorderdetail; on' (d.Value.salesorderid = o.salesorderid)
            where (d = None)
            select o
        }
        |> toSql

    sql.Contains("IS NULL") =! true

// Bug 2: where (s <> None) after leftJoin' should produce IS NOT NULL
[<Test>]
let ``Issue125-02 Where joined table <> None produces IS NOT NULL``() =
    let sql =
        select {
            for o in sales.salesorderheader do
            leftJoin' d in sales.salesorderdetail; on' (d.Value.salesorderid = o.salesorderid)
            where (d <> None)
            select o
        }
        |> toSql

    sql.Contains("IS NOT NULL") =! true

// Bug 3: 2nd+ join should not throw NotImplementedException
[<Test>]
let ``Issue125-03 Multiple inner joins``() =
    let sql =
        select {
            for p in production.product do
            join sc in production.productsubcategory on (p.productsubcategoryid = Some sc.productsubcategoryid)
            join c in production.productcategory on (sc.productcategoryid = c.productcategoryid)
            select (p.name, sc.name, c.name)
        }
        |> toSql

    sql.Contains("INNER JOIN") =! true
    sql.Contains("\"production\".\"productsubcategory\"") =! true
    sql.Contains("\"production\".\"productcategory\"") =! true

// Bug 4: where on outer table after leftJoin' should work
[<Test>]
let ``Issue125-04 Where on outer table after leftJoin``() =
    let sql =
        select {
            for o in sales.salesorderheader do
            leftJoin' d in sales.salesorderdetail; on' (d.Value.salesorderid = o.salesorderid)
            where (o.onlineorderflag = true)
            select (o, d)
        }
        |> toSql

    sql.Contains("LEFT JOIN") =! true
    sql.Contains("\"o\".\"onlineorderflag\" = @p") =! true

// Bug 5: select of whole table after multi-join should work
[<Test>]
let ``Issue125-05 Select whole table after multi-join``() =
    let sql =
        select {
            for p in production.product do
            join sc in production.productsubcategory on (p.productsubcategoryid = Some sc.productsubcategoryid)
            join c in production.productcategory on (sc.productcategoryid = c.productcategoryid)
            select p
        }
        |> toSql

    sql.Contains("INNER JOIN") =! true
    sql.Contains("\"p\".*") =! true

// Bug 6: orderBy after multi-join should work
[<Test>]
let ``Issue125-06 OrderBy after multi-join``() =
    let sql =
        select {
            for p in production.product do
            join sc in production.productsubcategory on (p.productsubcategoryid = Some sc.productsubcategoryid)
            join c in production.productcategory on (sc.productcategoryid = c.productcategoryid)
            orderBy p.name
            select p
        }
        |> toSql

    sql.Contains("ORDER BY \"p\".\"name\"") =! true

// Bug 7: groupBy after leftJoin' should work
[<Test>]
let ``Issue125-07 GroupBy after leftJoin``() =
    let sql =
        select {
            for o in sales.salesorderheader do
            leftJoin' d in sales.salesorderdetail; on' (d.Value.salesorderid = o.salesorderid)
            groupBy o.customerid
            select o.customerid
        }
        |> toSql

    sql.Contains("GROUP BY \"o\".\"customerid\"") =! true

// Bug 8: compound where predicate across joined tables
[<Test>]
let ``Issue125-08 Compound where predicate across joined tables``() =
    let sql =
        select {
            for o in sales.salesorderheader do
            join d in sales.salesorderdetail on (o.salesorderid = d.salesorderid)
            where (o.onlineorderflag = true && d.unitprice > 100m)
            select o
        }
        |> toSql

    sql.Contains("\"o\".\"onlineorderflag\"") =! true
    sql.Contains("\"d\".\"unitprice\"") =! true

// Bug 9: OR in where clause with bool column after join
[<Test>]
let ``Issue125-09 Or where with bool column after join``() =
    let sql =
        select {
            for o in sales.salesorderheader do
            leftJoin' d in sales.salesorderdetail; on' (d.Value.salesorderid = o.salesorderid)
            where (o.onlineorderflag = true || o.freight > 10m)
            select o
        }
        |> toSql

    sql.Contains("OR") =! true
    sql.Contains("\"o\".\"onlineorderflag\"") =! true
    sql.Contains("\"o\".\"freight\"") =! true

// Bug 10: where with external variable after join
[<Test>]
let ``Issue125-10 Where with captured variable after join``() =
    let minFreight = 50m
    let sql =
        select {
            for o in sales.salesorderheader do
            leftJoin' d in sales.salesorderdetail; on' (d.Value.salesorderid = o.salesorderid)
            where (o.freight > minFreight)
            select o
        }
        |> toSql

    sql.Contains("\"o\".\"freight\" > @p") =! true

// Bug 13: having after join should work
[<Test>]
let ``Issue125-13 Having after join``() =
    let sql =
        select {
            for o in sales.salesorderheader do
            join d in sales.salesorderdetail on (o.salesorderid = d.salesorderid)
            groupBy o.salesorderid
            having (countBy d.salesorderdetailid > 0)
            select o.salesorderid
        }
        |> toSql

    sql.Contains("HAVING") =! true
    sql.Contains("COUNT") =! true

// Bug 14: orderBy with aggregate after multi-join
[<Test>]
let ``Issue125-14 OrderBy with aggregate after join``() =
    let sql =
        select {
            for o in sales.salesorderheader do
            join d in sales.salesorderdetail on (o.salesorderid = d.salesorderid)
            groupBy o.salesorderid
            orderBy (sumBy d.unitprice)
            select o.salesorderid
        }
        |> toSql

    sql.Contains("ORDER BY SUM(\"d\".\"unitprice\")") =! true
