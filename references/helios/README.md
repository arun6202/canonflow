# Northwind Elasticsearch F# Query DSL Starter Kit

This repository implements a schema-aware, type-safe query DSL for Elasticsearch in F#, built on top of the Northwind database dataset. It includes data synchronization tools, a REST API, and a client-side Fable/Elmish web frontend.

## Project Structure

- **[Elastic.FSharp.Query](file:///e:/github/Adventureworks/gem/northwind/Elastic.FSharp.Query)**: The Level 3 type-safe, schema-aware query DSL library.
- **[Elastic.FSharp.Query.Tests](file:///e:/github/Adventureworks/gem/northwind/Elastic.FSharp.Query.Tests)**: Unit, integration, and property tests validating the query DSL's lowering logic and edge cases.
- **[ElasticApi](file:///e:/github/Adventureworks/gem/northwind/ElasticApi)**: An ASP.NET Core Web API service exposing search endpoints powered by Elasticsearch.
- **[ElasticSync](file:///e:/github/Adventureworks/gem/northwind/ElasticSync)**: A synchronization engine to ingest data from the SQLite database (`northwind.db`) into Elasticsearch.
- **[Frontend](file:///e:/github/Adventureworks/gem/northwind/Frontend)**: A Fable-based Elmish frontend compiled to JavaScript/React and bundled with Vite.
- **[SharedDomain](file:///e:/github/Adventureworks/gem/northwind/SharedDomain)**: Shared domain logic and data transfer objects (DTOs) used across the system.
- **[instructions](file:///e:/github/Adventureworks/gem/northwind/instructions)**: Design systems, guidelines, and agent specifications for native Claude workflows.

## Scripts & Utilities

- **[setup-env.ps1](file:///e:/github/Adventureworks/gem/northwind/setup-env.ps1)**: Downloads the SQLite Northwind database and downloads/extracts Elasticsearch (v8.14.0) with security disabled for local development.
- **[run-es.ps1](file:///e:/github/Adventureworks/gem/northwind/run-es.ps1)**: Boots up the local Elasticsearch instance.
- **[sync.ps1](file:///e:/github/Adventureworks/gem/northwind/sync.ps1)**: Runs the sync CLI tool (`ElasticSync`) to index the SQLite database data into Elasticsearch.

## Quick Start

### 1. Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) (for Frontend dependencies)
- PowerShell

### 2. Environment Setup
Initialize your local database and Elasticsearch environment by running the setup script:
```powershell
.\setup-env.ps1
```

### 3. Start Elasticsearch
Run Elasticsearch locally using:
```powershell
.\run-es.ps1
```

### 4. Ingest/Sync Data
Index the SQLite dataset into Elasticsearch:
```powershell
.\sync.ps1
```

### 5. Build and Run API
Run the backend web service:
```powershell
cd ElasticApi
dotnet run
```

### 6. Build and Run Frontend
Install dependencies and launch the Fable/Vite dev server:
```powershell
cd Frontend
npm install
npm run dev
```
