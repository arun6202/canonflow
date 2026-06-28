namespace SqlHydra.Query

open System

type CommandOptions =
    {
        /// The wait time before terminating the attempt to execute a command and generating an error.
        CommandTimeout: TimeSpan option
    }
    static member Default = { CommandTimeout = None }

/// Comparison operators used in WHERE/HAVING/ON clauses.
type ComparisonOp =
    | Eq
    | NotEq
    | Gt
    | GtEq
    | Lt
    | LtEq

/// Logical connective for combining predicates.
type LogicalOp =
    | And
    | Or

/// JOIN type.
type JoinKind =
    | InnerJoin
    | LeftJoin

/// ORDER BY direction.
type OrderDirection =
    | Asc
    | Desc

/// An ORDER BY clause.
type OrderByClause =
    | OrderByColumn of column: string * direction: OrderDirection
    | OrderByRaw of fragment: string

/// A SELECT column.
type SelectColumn =
    /// Select all columns from a table alias: alias.*
    | AllColumns of tableAlias: string
    /// Select a specific column: alias.column
    | SpecificColumn of qualifiedName: string
    /// Raw SQL expression (e.g., COUNT(*), aggregate AS alias)
    | RawColumn of fragment: string

// ─── Mutually recursive types ───
// SqlValue, WhereClause, JoinClause, and SelectQueryIR reference each other.

/// A SQL expression value (right-hand side of a comparison).
type SqlValue =
    /// A parameter value (may be a QueryParameter wrapping provider type info)
    | Parameter of value: obj
    /// SQL NULL
    | Null
    /// A reference to another column (fully qualified: alias.column or schema.table.column)
    | ColumnRef of qualifiedName: string
    /// A subquery reference
    | SubQuery of SelectQueryIR
    /// Raw SQL fragment with parameter bindings
    | RawSql of fragment: string * parameters: obj[]

/// A predicate in a WHERE, HAVING, or JOIN ON clause.
and WhereClause =
    /// column op value (e.g., a.City = @p0)
    | Compare of column: string * op: ComparisonOp * value: SqlValue
    /// column op column (e.g., a.Id = b.Id)
    | CompareColumns of left: string * op: ComparisonOp * right: string
    /// column IS NULL
    | IsNull of column: string
    /// column IS NOT NULL
    | IsNotNull of column: string
    /// column IN (value1, value2, ...)
    | InValues of column: string * values: obj[]
    /// column IN (subquery)
    | InSubQuery of column: string * subquery: SelectQueryIR
    /// column NOT IN (value1, value2, ...)
    | NotInValues of column: string * values: obj[]
    /// column NOT IN (subquery)
    | NotInSubQuery of column: string * subquery: SelectQueryIR
    /// column LIKE pattern
    | Like of column: string * pattern: obj
    /// column NOT LIKE pattern
    | NotLike of column: string * pattern: obj
    /// NOT (clause)
    | Not of WhereClause
    /// left AND/OR right
    | Combined of left: WhereClause * op: LogicalOp * right: WhereClause
    /// Raw WHERE SQL fragment with parameter bindings
    | RawWhere of fragment: string * parameters: obj[]
    /// Boolean column check (e.g., WHERE a.IsActive = true)
    | BoolColumn of column: string * value: bool
    /// Wraps a clause in parentheses (used by WHERE builder to group conditions)
    | Grouped of WhereClause
    /// Identity element - no condition
    | Empty

/// A JOIN clause.
and JoinClause = {
    Kind: JoinKind
    /// Table spec string, e.g. "Sales.SalesOrderDetail AS d"
    Table: string
    /// Join conditions
    Condition: WhereClause
}

/// The complete SELECT query IR.
and SelectQueryIR = {
    /// Table spec: "Schema.Table as alias" or "Schema.Table"
    From: string option
    /// Columns to select. Empty list = SELECT *
    Select: SelectColumn list
    /// WHERE clause. Empty = no WHERE.
    Where: WhereClause
    /// JOIN clauses
    Joins: JoinClause list
    /// GROUP BY column names
    GroupBy: string list
    /// HAVING clause. Empty = no HAVING.
    Having: WhereClause
    /// ORDER BY clauses
    OrderBy: OrderByClause list
    /// OFFSET (skip) value
    Skip: int option
    /// LIMIT (take) value
    Take: int option
    /// DISTINCT flag
    Distinct: bool
    /// SELECT COUNT(*) flag
    IsCount: bool
    /// Options for the command executing the query.
    CommandOptions: CommandOptions
}

/// Helpers for composing WhereClause values.
module WhereClause =
    /// Combines two WHERE clauses with AND, wrapping each side in Grouped for proper parenthesization.
    let combineAnd (existing: WhereClause) (newClause: WhereClause) =
        match existing, newClause with
        | Empty, c | c, Empty -> c
        | l, r -> Combined(Grouped l, And, Grouped r)

    /// Combines two WHERE clauses with OR, wrapping each side in Grouped for proper parenthesization.
    let combineOr (existing: WhereClause) (newClause: WhereClause) =
        match existing, newClause with
        | Empty, c | c, Empty -> c
        | l, r -> Combined(Grouped l, Or, Grouped r)

    /// Combines two clauses with AND without grouping (flat, for JOIN ON conditions).
    let combineAndFlat (existing: WhereClause) (newClause: WhereClause) =
        match existing, newClause with
        | Empty, c | c, Empty -> c
        | l, r -> Combined(l, And, r)

module SelectQueryIR =
    let empty = {
        From = None
        Select = []
        Where = Empty
        Joins = []
        GroupBy = []
        Having = Empty
        OrderBy = []
        Skip = None
        Take = None
        Distinct = false
        IsCount = false
        CommandOptions = CommandOptions.Default
    }

// ─── Insert-related types ───

type InsertType =
    | Insert
    | InsertOrReplace
    | OnConflictDoUpdate of conflictFields: string list * updateFields: string list
    | OnConflictDoNothing of conflictFields: string list
    | InsertOrUpdateOnUnique of keyFields: string list * updateFields: string list

type Nullability =
    | IsOptional
    | IsNullable
    | NotNullable

type OutputField =
    {
        ColumnName: string
        PropertyType: Type
        Nullability: Nullability
    }

/// INSERT query IR.
type InsertQueryIR = {
    Table: string
    Columns: string list
    /// Each row is an array of parameter values (may include QueryParameter wrappers)
    Rows: obj[] list
    IdentityField: string option
    InsertType: InsertType
    OutputFields: OutputField list
    /// Options for the command executing the query.
    CommandOptions: CommandOptions
}

/// UPDATE query IR.
type UpdateQueryIR = {
    Table: string
    /// Column name * parameter value pairs
    SetColumns: (string * obj) list
    Where: WhereClause
    OutputFields: OutputField list
    /// Options for the command executing the query.
    CommandOptions: CommandOptions
}

/// DELETE query IR.
type DeleteQueryIR = {
    Table: string
    Where: WhereClause
    /// Options for the command executing the query.
    CommandOptions: CommandOptions
}
