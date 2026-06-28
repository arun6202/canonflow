
import { Union, Record } from "../Frontend/fable_modules/fable-library-js.5.2.0/Types.js";
import { int64_type, union_type, option_type, list_type, bool_type, float64_type, decimal_type, record_type, string_type, int32_type } from "../Frontend/fable_modules/fable-library-js.5.2.0/Reflection.js";

export class ProductDto extends Record {
    constructor(ProductId, ProductName, CategoryName) {
        super();
        this.ProductId = (ProductId | 0);
        this.ProductName = ProductName;
        this.CategoryName = CategoryName;
    }
}

export function ProductDto_$reflection() {
    return record_type("ElasticApi.Dtos.ProductDto", [], ProductDto, () => [["ProductId", int32_type], ["ProductName", string_type], ["CategoryName", string_type]]);
}

export class OrderLineDto extends Record {
    constructor(Product, UnitPrice, Quantity, Discount) {
        super();
        this.Product = Product;
        this.UnitPrice = UnitPrice;
        this.Quantity = (Quantity | 0);
        this.Discount = Discount;
    }
}

export function OrderLineDto_$reflection() {
    return record_type("ElasticApi.Dtos.OrderLineDto", [], OrderLineDto, () => [["Product", ProductDto_$reflection()], ["UnitPrice", decimal_type], ["Quantity", int32_type], ["Discount", decimal_type]]);
}

export class CustomerDto extends Record {
    constructor(CustomerId, CompanyName, ContactName, Country) {
        super();
        this.CustomerId = CustomerId;
        this.CompanyName = CompanyName;
        this.ContactName = ContactName;
        this.Country = Country;
    }
}

export function CustomerDto_$reflection() {
    return record_type("ElasticApi.Dtos.CustomerDto", [], CustomerDto, () => [["CustomerId", string_type], ["CompanyName", string_type], ["ContactName", string_type], ["Country", string_type]]);
}

export class EmployeeDto extends Record {
    constructor(EmployeeId, FirstName, LastName, Title) {
        super();
        this.EmployeeId = (EmployeeId | 0);
        this.FirstName = FirstName;
        this.LastName = LastName;
        this.Title = Title;
    }
}

export function EmployeeDto_$reflection() {
    return record_type("ElasticApi.Dtos.EmployeeDto", [], EmployeeDto, () => [["EmployeeId", int32_type], ["FirstName", string_type], ["LastName", string_type], ["Title", string_type]]);
}

export class OrderLineDocumentDto extends Record {
    constructor(Id, OrderId, OrderDate, Customer, Employee, Product, UnitPrice, Quantity, Discount, LineSales) {
        super();
        this.Id = Id;
        this.OrderId = (OrderId | 0);
        this.OrderDate = OrderDate;
        this.Customer = Customer;
        this.Employee = Employee;
        this.Product = Product;
        this.UnitPrice = UnitPrice;
        this.Quantity = (Quantity | 0);
        this.Discount = Discount;
        this.LineSales = LineSales;
    }
}

export function OrderLineDocumentDto_$reflection() {
    return record_type("ElasticApi.Dtos.OrderLineDocumentDto", [], OrderLineDocumentDto, () => [["Id", string_type], ["OrderId", int32_type], ["OrderDate", string_type], ["Customer", CustomerDto_$reflection()], ["Employee", EmployeeDto_$reflection()], ["Product", ProductDto_$reflection()], ["UnitPrice", decimal_type], ["Quantity", int32_type], ["Discount", decimal_type], ["LineSales", decimal_type]]);
}

export class AggregationBucketDto extends Record {
    constructor(Key, Revenue) {
        super();
        this.Key = Key;
        this.Revenue = Revenue;
    }
}

export function AggregationBucketDto_$reflection() {
    return record_type("ElasticApi.Dtos.AggregationBucketDto", [], AggregationBucketDto, () => [["Key", string_type], ["Revenue", float64_type]]);
}

export class ErrorResponseDto extends Record {
    constructor(Message) {
        super();
        this.Message = Message;
    }
}

export function ErrorResponseDto_$reflection() {
    return record_type("ElasticApi.Dtos.ErrorResponseDto", [], ErrorResponseDto, () => [["Message", string_type]]);
}

export class SchemaField extends Record {
    constructor(Name, DisplayName, Type, SupportsTerms, SupportsPrefix, SupportsRange, SupportsMatch) {
        super();
        this.Name = Name;
        this.DisplayName = DisplayName;
        this.Type = Type;
        this.SupportsTerms = SupportsTerms;
        this.SupportsPrefix = SupportsPrefix;
        this.SupportsRange = SupportsRange;
        this.SupportsMatch = SupportsMatch;
    }
}

export function SchemaField_$reflection() {
    return record_type("ElasticApi.Dtos.SchemaField", [], SchemaField, () => [["Name", string_type], ["DisplayName", string_type], ["Type", string_type], ["SupportsTerms", bool_type], ["SupportsPrefix", bool_type], ["SupportsRange", bool_type], ["SupportsMatch", bool_type]]);
}

export class DomainConfig extends Record {
    constructor(DomainId, DisplayName, Fields) {
        super();
        this.DomainId = DomainId;
        this.DisplayName = DisplayName;
        this.Fields = Fields;
    }
}

export function DomainConfig_$reflection() {
    return record_type("ElasticApi.Dtos.DomainConfig", [], DomainConfig, () => [["DomainId", string_type], ["DisplayName", string_type], ["Fields", list_type(SchemaField_$reflection())]]);
}

export class ClientPredicate extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Term", "Terms", "Match", "Prefix", "Range", "And", "Or", "Not"];
    }
}

export function ClientPredicate_$reflection() {
    return union_type("ElasticApi.Dtos.ClientPredicate", [], ClientPredicate, () => [[["field", string_type], ["value", string_type]], [["field", string_type], ["values", list_type(string_type)]], [["field", string_type], ["value", string_type]], [["field", string_type], ["value", string_type]], [["field", string_type], ["min", option_type(float64_type)], ["max", option_type(float64_type)]], [["Item", list_type(ClientPredicate_$reflection())]], [["Item", list_type(ClientPredicate_$reflection())]], [["Item", ClientPredicate_$reflection()]]]);
}

export class ClientAggregation extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["Terms", "Sum"];
    }
}

export function ClientAggregation_$reflection() {
    return union_type("ElasticApi.Dtos.ClientAggregation", [], ClientAggregation, () => [[["name", string_type], ["field", string_type], ["size", int32_type]], [["name", string_type], ["field", string_type]]]);
}

export class AnalyticsRequestDto extends Record {
    constructor(Filter, Aggregations) {
        super();
        this.Filter = Filter;
        this.Aggregations = Aggregations;
    }
}

export function AnalyticsRequestDto_$reflection() {
    return record_type("ElasticApi.Dtos.AnalyticsRequestDto", [], AnalyticsRequestDto, () => [["Filter", option_type(ClientPredicate_$reflection())], ["Aggregations", list_type(ClientAggregation_$reflection())]]);
}

export class BucketDto extends Record {
    constructor(Key, DocCount, SubValue) {
        super();
        this.Key = Key;
        this.DocCount = DocCount;
        this.SubValue = SubValue;
    }
}

export function BucketDto_$reflection() {
    return record_type("ElasticApi.Dtos.BucketDto", [], BucketDto, () => [["Key", string_type], ["DocCount", int64_type], ["SubValue", option_type(float64_type)]]);
}

export class AnalyticsResponseDto extends Record {
    constructor(AggName, Buckets) {
        super();
        this.AggName = AggName;
        this.Buckets = Buckets;
    }
}

export function AnalyticsResponseDto_$reflection() {
    return record_type("ElasticApi.Dtos.AnalyticsResponseDto", [], AnalyticsResponseDto, () => [["AggName", string_type], ["Buckets", list_type(BucketDto_$reflection())]]);
}

export class FinalCustomerAnalysisDto extends Record {
    constructor(CustomerId, TotalOrders, TotalSales, AvgOrderValue, LastOrderDate, MostRecentHandledBy, TopProductCategoryPurchased, DaysSinceLastOrder, SalesRankWithinCountry, CustomerSegment, CustomerStatus) {
        super();
        this.CustomerId = CustomerId;
        this.TotalOrders = (TotalOrders | 0);
        this.TotalSales = TotalSales;
        this.AvgOrderValue = AvgOrderValue;
        this.LastOrderDate = LastOrderDate;
        this.MostRecentHandledBy = MostRecentHandledBy;
        this.TopProductCategoryPurchased = TopProductCategoryPurchased;
        this.DaysSinceLastOrder = (DaysSinceLastOrder | 0);
        this.SalesRankWithinCountry = (SalesRankWithinCountry | 0);
        this.CustomerSegment = CustomerSegment;
        this.CustomerStatus = CustomerStatus;
    }
}

export function FinalCustomerAnalysisDto_$reflection() {
    return record_type("ElasticApi.Dtos.FinalCustomerAnalysisDto", [], FinalCustomerAnalysisDto, () => [["CustomerId", string_type], ["TotalOrders", int32_type], ["TotalSales", decimal_type], ["AvgOrderValue", decimal_type], ["LastOrderDate", string_type], ["MostRecentHandledBy", string_type], ["TopProductCategoryPurchased", string_type], ["DaysSinceLastOrder", int32_type], ["SalesRankWithinCountry", int32_type], ["CustomerSegment", string_type], ["CustomerStatus", string_type]]);
}

