# Andy Settings

Centralized configuration and settings management service for the Andy ecosystem.

## Architecture

```
andy-settings/
├── src/
│   ├── Andy.Settings.Domain/           # Domain entities, enums (no dependencies)
│   ├── Andy.Settings.Application/      # Interfaces, DTOs, options (→ Domain)
│   ├── Andy.Settings.Infrastructure/   # EF Core, repositories, services (→ Domain, Application)
│   ├── Andy.Settings.Api/              # ASP.NET Core REST + MCP API (→ Application, Infrastructure)
│   └── Andy.Settings.Shared/           # Shared types
├── tools/
│   └── Andy.Settings.Cli/             # CLI tool (System.CommandLine + Spectre.Console)
├── tests/
│   ├── Andy.Settings.Tests.Unit/      # Unit tests (xUnit, Moq, FluentAssertions)
│   └── Andy.Settings.Tests.Integration/ # Integration tests (WebApplicationFactory)
├── client/                            # Angular 18 SPA
├── docs/                              # Architecture and stories
├── certs/                             # Corporate CA certificates (gitignored)
├── Dockerfile                         # Multi-stage: Node + .NET + runtime
└── docker-compose.yml                 # PostgreSQL + API
```

## Tech Stack

- **Backend:** .NET 8, ASP.NET Core, Entity Framework Core
- **Frontend:** Angular 18, SCSS
- **Database:** PostgreSQL (primary), SQLite (embedded mode)
- **Auth:** Andy Auth (JWT Bearer), Andy RBAC
- **API:** REST + Swagger/OpenAPI + MCP (Model Context Protocol)
- **CLI:** System.CommandLine, Spectre.Console
- **Telemetry:** OpenTelemetry (OTLP)
- **Logging:** Serilog

## Quick Start

```bash
# Start with Docker Compose
docker compose up

# API: https://localhost:5300
# Swagger: https://localhost:5300/swagger
```

## Development

```bash
# Backend
dotnet build
dotnet run --project src/Andy.Settings.Api

# Frontend
cd client
npm install
npm start
# Angular dev server: https://localhost:4200

# CLI
dotnet run --project tools/Andy.Settings.Cli -- --help
```

## Default Ports

| Service    | Port |
|------------|------|
| API HTTPS  | 5300 |
| API HTTP   | 5301 |
| PostgreSQL | 5438 |
| Angular    | 4200 |
| Andy Auth  | 5001 |
| Andy RBAC  | 5003 |

## Stories

See [docs/stories.md](docs/stories.md) for the full implementation backlog.
