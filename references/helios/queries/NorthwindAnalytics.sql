/* 
    Northwind Complex Compound Query

    Goal:
    For each customer, analyze:
    - total orders
    - total sales
    - average order value
    - last order date
    - top product category purchased
    - most recent employee who handled their order
    - customer rank by sales within country
    - customer segment
*/

WITH OrderLineSales AS
(
    SELECT
        c.CustomerID,
        c.CompanyName AS CustomerName,
        c.Country,
        o.OrderID,
        o.OrderDate,
        e.EmployeeID,
        e.FirstName + ' ' + e.LastName AS EmployeeName,
        p.ProductID,
        p.ProductName,
        cat.CategoryID,
        cat.CategoryName,

        od.UnitPrice,
        od.Quantity,
        od.Discount,

        CAST(
            od.UnitPrice * od.Quantity * (1 - od.Discount)
            AS DECIMAL(18, 2)
        ) AS LineSales
    FROM Customers c
    JOIN Orders o ON c.CustomerID = o.CustomerID
    JOIN Employees e ON o.EmployeeID = e.EmployeeID
    JOIN [Order Details] od ON o.OrderID = od.OrderID
    JOIN Products p ON od.ProductID = p.ProductID
    JOIN Categories cat ON p.CategoryID = cat.CategoryID
),
CustomerBaseStats AS
(
    SELECT
        CustomerID,
        CustomerName,
        Country,
        COUNT(DISTINCT OrderID) AS TotalOrders,
        SUM(LineSales) AS TotalSales,
        MAX(OrderDate) AS LastOrderDate
    FROM OrderLineSales
    GROUP BY CustomerID, CustomerName, Country
),
CustomerCategoryRank AS
(
    SELECT
        CustomerID,
        CategoryName,
        SUM(LineSales) AS CategorySales,
        ROW_NUMBER() OVER(PARTITION BY CustomerID ORDER BY SUM(LineSales) DESC) as CatRank
    FROM OrderLineSales
    GROUP BY CustomerID, CategoryName
),
CustomerRecentEmployee AS
(
    SELECT
        CustomerID,
        EmployeeName,
        OrderDate,
        ROW_NUMBER() OVER(PARTITION BY CustomerID ORDER BY OrderDate DESC) as EmpRank
    FROM OrderLineSales
)
SELECT 
    bs.CustomerID,
    bs.CustomerName,
    bs.Country,
    bs.TotalOrders,
    bs.TotalSales,
    CAST(bs.TotalSales / bs.TotalOrders AS DECIMAL(18,2)) AS AvgOrderValue,
    bs.LastOrderDate,
    DATEDIFF(day, bs.LastOrderDate, GETDATE()) AS DaysSinceLastOrder,
    
    (SELECT TOP 1 CategoryName FROM CustomerCategoryRank ccr WHERE ccr.CustomerID = bs.CustomerID AND CatRank = 1) AS TopCategory,
    (SELECT TOP 1 EmployeeName FROM CustomerRecentEmployee cre WHERE cre.CustomerID = bs.CustomerID AND EmpRank = 1) AS MostRecentHandledBy,

    RANK() OVER(PARTITION BY bs.Country ORDER BY bs.TotalSales DESC) AS SalesRankWithinCountry,

    CASE 
        WHEN bs.TotalSales > 100000 THEN 'Platinum'
        WHEN bs.TotalSales > 50000 THEN 'Gold'
        WHEN bs.TotalSales > 10000 THEN 'Silver'
        ELSE 'Bronze'
    END AS CustomerSegment,

    CASE
        WHEN DATEDIFF(day, bs.LastOrderDate, GETDATE()) <= 90 THEN 'Active'
        WHEN DATEDIFF(day, bs.LastOrderDate, GETDATE()) <= 180 THEN 'At Risk'
        ELSE 'Dormant'
    END AS CustomerStatus

FROM CustomerBaseStats bs
ORDER BY bs.Country, SalesRankWithinCountry;
