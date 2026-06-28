-- Reconciliation queries for the current local baseline.
-- These queries are intentionally direct SQL so humans can verify semantic
-- metrics without trusting provider compilers.

-- Northwind source counts
SELECT 'customers' AS check_name, COUNT(*) AS value FROM Customers
UNION ALL SELECT 'orders', COUNT(*) FROM Orders
UNION ALL SELECT 'order_details', COUNT(*) FROM "Order Details"
UNION ALL SELECT 'products', COUNT(*) FROM Products
UNION ALL SELECT 'categories', COUNT(*) FROM Categories
UNION ALL SELECT 'employees', COUNT(*) FROM Employees;

-- Northwind total revenue
SELECT
    'northwind_total_line_sales' AS check_name,
    ROUND(SUM(od.UnitPrice * od.Quantity * (1 - od.Discount)), 2) AS value
FROM "Order Details" od
JOIN Orders o ON od.OrderID = o.OrderID;

-- Northwind revenue by customer country
SELECT
    c.Country,
    COUNT(DISTINCT o.OrderID) AS order_count,
    ROUND(SUM(od.UnitPrice * od.Quantity * (1 - od.Discount)), 2) AS total_revenue
FROM Customers c
JOIN Orders o ON c.CustomerID = o.CustomerID
JOIN "Order Details" od ON o.OrderID = od.OrderID
GROUP BY c.Country
ORDER BY total_revenue DESC;

-- AdventureWorks source counts
SELECT 'adventureworks_flat' AS check_name, COUNT(*) AS value FROM AdventureWorksFlat
UNION ALL SELECT 'dim_customer', COUNT(*) FROM DimCustomer
UNION ALL SELECT 'dim_product', COUNT(*) FROM DimProduct
UNION ALL SELECT 'fact_internet_sales', COUNT(*) FROM FactInternetSales;

-- AdventureWorks total revenue through the current flat serving view
SELECT
    'adventureworks_total_line_sales' AS check_name,
    ROUND(SUM(LineSales), 2) AS value
FROM AdventureWorksFlat;

-- AdventureWorks revenue by country through the current flat serving view
SELECT
    Country,
    COUNT(*) AS sales_line_count,
    ROUND(SUM(LineSales), 2) AS total_revenue
FROM AdventureWorksFlat
GROUP BY Country
ORDER BY total_revenue DESC;
