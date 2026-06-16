# AGENTS.md — restme

> **⚠️ REQUIRED READING**: Before starting, use your Read tool to load the root platform context:
> - `../../AGENTS.md` — Platform context, roles, workflow chains, OElite framework patterns
> - `../../CLAUDE.md` — Additional platform standards and conventions
> 
> This file is repo-specific orientation and does NOT contain the full platform context.

## What This Repo Is

OElite.Restme is the unified data-infrastructure library for the entire OElite Platform. It provides a single `Rest` class and provider-pattern abstractions (`IHttpProvider`, `ICacheProvider`, `IStorageProvider`, `IQueueProvider`, etc.) that back HTTP, Redis, RabbitMQ, MongoDB, S3, Azure Blob, ClickHouse, Kafka, and OpenSearch. Every OElite backend service depends on Restme for data access — it is never called via raw driver APIs.

## Tech Stack

- **Primary**: .NET 10 / C# (multi-target: `net10.0;net9.0;net8.0`)
- **Secondary**: NuGet library packages published to nuget.org (main) and packages.phanes.ltd (develop/uat)
- **Data**: Abstractions over MongoDB, Redis, RabbitMQ, S3, Azure Blob, ClickHouse, Kafka, OpenSearch

## Key Paths & Structure

```
[REPO_ROOT]/
├── OElite.Restme/               # Core library: Rest class, provider abstractions, HTTP client
├── OElite.Restme.Utils/         # BaseEntity, EntityStatus, DbObjectId, extensions
├── OElite.Restme.Redis/         # Redis cache/queue provider
├── OElite.Restme.RabbitMQ/      # RabbitMQ message queue provider
├── OElite.Restme.MongoDb/       # MongoDB provider (MongoDbCentre, LINQ, aggregation)
├── OElite.Restme.S3/            # S3-compatible storage provider
├── OElite.Restme.Azure/         # Azure Blob Storage provider
├── OElite.Restme.ClickHouse/    # ClickHouse analytics provider
├── OElite.Restme.Kafka/         # Kafka streaming provider
├── OElite.Restme.OpenSearch/    # OpenSearch search/analytics provider
├── OElite.Restme.Hosting/       # ASP.NET Core DI extensions, IDistributedCache adapters
├── OElite.Restme.RateLimiting/  # Rate limiting middleware
├── OElite.Restme.GoogleUtils/   # Google Cloud integrations
├── tests/                       # Integration and unit test projects
├── OElite.Restme.sln            # Solution file
└── NuGet.config                 # NuGet feed configuration
```

**Critical files**:
- `OElite.Restme.Utils/BaseEntity.cs` — Base entity class used by all OElite entities (Id, Status, Region, MetaData)
- `OElite.Restme/OElite.Restme.csproj` — Core package definition (version 2.1.1)
- `.gitlab-ci.yml` — CI pipeline: build, pack, deploy to NuGet

## Build & Test Commands

```bash
# Build
dotnet restore --configfile NuGet.config && dotnet build --configuration Release --configfile NuGet.config

# Test (currently disabled in CI to reduce build time; test projects exist under tests/)
dotnet test --configuration Release

# Pack
dotnet pack OElite.Restme/OElite.Restme.csproj --configuration Release --output ./packages
```

## OElite-Specific Patterns

This repo **defines** the patterns used by every other OElite backend:
- **BaseEntity**: `OElite.Restme.Utils/BaseEntity.cs` — `DbObjectId Id`, `EntityStatus Status`, `string? Region`, `MetaData`
- **Provider pattern**: `rest.GetProvider<ICacheProvider>()`, `rest.GetProvider<IStorageProvider>("name")` for named providers
- **Entity attributes**: `[DbCollection("snake_case")]`, `[DbId]`, `[DbField]`, `[DbFieldIgnore]`, `[DenormalizedField(...)]`
- **Extension methods**: `CachemeAsync`, `FindmeAsync`, `ExpiremeAsync` on `ICacheProvider` and `IDistributedCache`

## Standards & Overrides

This repo does not currently have `.ai/standards/`. If this repo has special requirements (security-critical, non-OElite patterns, unique architecture), create `.ai/standards/` files that extend (never contradict) the global standards at `/coding-standards/`.

**Always verify before "done"**: rebuild affected + referencing projects; run tests; confirm the service starts and its health endpoint returns 200.

## Deprecated / Do Not Touch

- `uranus/restme-wildduck/` — deprecated WildDuck mail integration; do not modify
