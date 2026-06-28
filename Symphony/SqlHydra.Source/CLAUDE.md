# CLAUDE.md

## Build Commands

From the Build directory (`src/Build/`):
```bash
cd src/Build
dotnet run -- Build     # Builds all projects for all frameworks
dotnet run -- Test      # Runs all tests for all frameworks
dotnet run -- Pack      # Creates NuGet packages
dotnet run -- Publish   # Publishes to NuGet (requires SQLHYDRA_NUGET_KEY env var)
```

For specific framework testing:
```bash
dotnet run -- TestNet8  # Test on .NET 8.0
dotnet run -- TestNet9  # Test on .NET 9.0
```
