namespace Elastic.FSharp.Query.Operators

open Elastic.FSharp.Query.Types

[<AutoOpen>]
module Comparison =
    /// <summary>
    /// Greater than numeric comparison (`range` &gt;).
    /// </summary>
    let (>.) (f: NumericField<'S, 'U>) (v: float) : Predicate<'S> =
        // Cast to obj for the type erasure boundary in AST
        let erasedF : Field<'S, Numeric, obj> = { Path = f.Path; Kind = f.Kind }
        Predicate.Range (FieldRef.NUM erasedF, Bound.Gt (FieldValue.VFloat v), Bound.Unbounded)

    /// <summary>
    /// Less than numeric comparison (`range` &lt;).
    /// </summary>
    let (<.) (f: NumericField<'S, 'U>) (v: float) : Predicate<'S> =
        let erasedF : Field<'S, Numeric, obj> = { Path = f.Path; Kind = f.Kind }
        Predicate.Range (FieldRef.NUM erasedF, Bound.Unbounded, Bound.Lt (FieldValue.VFloat v))
