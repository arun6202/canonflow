namespace SqlHydra.Query

open System

module VersionCheck =

    /// The minimum version of SqlHydra.Cli that is compatible with this runtime.
    let minimumSupportedCli = Version(3, 4, 0)

    /// Asserts that the SqlHydra.Cli version used to generate code is compatible with the runtime.
    let assertIsCompatible (cli: Version) (ns: string) =
        if cli < minimumSupportedCli then
            failwith $"Please update SqlHydra.Cli to a version >= v%O{minimumSupportedCli} and regenerate the types in namespace '{ns}'."
