CREATE TABLE DimCustomer(
	CustomerKey int,
	GeographyKey int,
	CustomerAlternateKey text,
	Title text,
	FirstName text,
	MiddleName text,
	LastName text,
	NameStyle bit,
	BirthDate date,
	MaritalStatus text,
	Suffix text,
	Gender text,
	EmailAddress text,
	YearlyIncome money,
	TotalChildren tinyint,
	NumberChildrenAtHome tinyint,
	EnglishEducation text,
	SpanishEducation text,
	FrenchEducation text,
	EnglishOccupation text,
	SpanishOccupation text,
	FrenchOccupation text,
	HouseOwnerFlag text,
	NumberCarsOwned tinyint,
	AddressLine1 text,
	AddressLine2 text,
	Phone text,
	DateFirstPurchase date,
	CommuteDistance text
);

CREATE TABLE DimProduct(
	ProductKey int,
	ProductAlternateKey text,
	ProductSubcategoryKey int,
	WeightUnitMeasureCode text,
	SizeUnitMeasureCode text,
	EnglishProductName text,
	SpanishProductName text,
	FrenchProductName text,
	StandardCost money,
	FinishedGoodsFlag bit,
	Color text,
	SafetyStockLevel smallint,
	ReorderPoint smallint,
	ListPrice money,
	Size text,
	SizeRange text,
	Weight float,
	DaysToManufacture int,
	ProductLine text,
	DealerPrice money,
	Class text,
	Style text,
	ModelName text,
	LargePhoto blob,
	EnglishDescription text,
	FrenchDescription text,
	ChineseDescription text,
	ArabicDescription text,
	HebrewDescription text,
	ThaiDescription text,
	GermanDescription text,
	JapaneseDescription text,
	TurkishDescription text,
	StartDate date,
	EndDate date,
	Status text
);

CREATE TABLE FactInternetSales(
	ProductKey int,
	OrderDateKey int,
	DueDateKey int,
	ShipDateKey int,
	CustomerKey int,
	PromotionKey int,
	CurrencyKey int,
	SalesTerritoryKey int,
	SalesOrderNumber text,
	SalesOrderLineNumber tinyint,
	RevisionNumber tinyint,
	OrderQuantity smallint,
	UnitPrice money,
	ExtendedAmount money,
	UnitPriceDiscountPct float,
	DiscountAmount float,
	ProductStandardCost money,
	TotalProductCost money,
	SalesAmount money,
	TaxAmt money,
	Freight money,
	CarrierTrackingNumber text,
	CustomerPONumber text,
	OrderDate date,
	DueDate date,
	ShipDate date
);

.mode csv
.separator |
.import E:/github/Adventureworks/gem/scratch_repo/data/csv-utf8/DimCustomer.csv DimCustomer
.import E:/github/Adventureworks/gem/scratch_repo/data/csv-utf8/DimProduct.csv DimProduct
.import E:/github/Adventureworks/gem/scratch_repo/data/csv-utf8/FactInternetSales.csv FactInternetSales

CREATE VIEW AdventureWorksFlat AS
SELECT 
    f.SalesOrderNumber || '-' || f.SalesOrderLineNumber as Id,
    CAST(REPLACE(f.SalesOrderNumber, 'SO', '') AS INTEGER) as OrderId,
    f.OrderDate as OrderDate,
    c.CustomerAlternateKey as CustomerId,
    'USA' as Country,
    c.LastName as EmployeeLastName,
    p.EnglishProductName as ProductCategory,
    f.SalesAmount as LineSales
FROM FactInternetSales f
JOIN DimCustomer c ON f.CustomerKey = c.CustomerKey
JOIN DimProduct p ON f.ProductKey = p.ProductKey;
