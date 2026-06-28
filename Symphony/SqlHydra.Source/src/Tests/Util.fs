[<AutoOpen>]
module Util

open NUnit.Framework

/// Sequence length is > 0.
let gt0 (items: 'Item seq) =
    Assert.IsTrue(items |> Seq.length > 0, "Expected more than 0.")


type System.String with
    /// Used to temporarily revert unit tests that were upgraded to test the new v4 `select` behavior.
    member this.RemoveHydraExpr() =
        this.Replace(" AS __hydra_expr_0", "")
