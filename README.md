# Andy Settings

Centralized configuration and settings management for the Andy ecosystem.

## Architecture

```
andy-settings/
├── src/
│   ├── Andy.Settings.Domain/           # Entities, enums (no dependencies)
│   ├── Andy.Settings.Application/      # Interfaces, DTOs, options (-> Domain)
│   ├── Andy.Settings.Infrastructure/   # EF Core, repositories, services (-> Domain, Application)
│   ├── Andy.Settings.Api/              # ASP.NET Core REST + MCP + Swagger (-> all)
│   └── Andy.Settings.Shared/           # Shared types
├── tools/
│   └── Andy.Settings.Cli/             # CLI tool (System.CommandLine + Spectre.Console)
├── tests/
│   ├── Andy.Settings.Tests.Unit/      # Unit tests (xUnit, Moq, FluentAssertions)
│   └── Andy.Settings.Tests.Integration/ # Integration tests (WebApplicationFactory)
├── client/                            # Angular 18 SPA
├── examples/                          # Multi-language API/MCP examples (C#, Python, JS, Go, Rust, PowerShell)
├── docs/                              # MkDocs Material documentation
├── certs/                             # Corporate CA certificates (gitignored)
├── Dockerfile                         # Multi-stage: Node + .NET + runtime
└── docker-compose.yml                 # PostgreSQL + API
```

## Key Features

- **Typed Definitions** -- strongly typed setting schemas with validation and UI metadata
- **Scoped Resolution** -- 8-level scope precedence with deterministic resolution and explanation
- **Encrypted Secrets** -- AES-256-GCM via Data Protection API, RBAC-gated access
- **Conductor Integration** -- embedded as 8th service in Conductor macOS app (port 9107, SQLite)
- **Auth & RBAC** -- Andy Auth (OIDC) + Andy RBAC with scope-aware permissions
- **REST + Swagger + MCP** -- full API with OpenAPI docs and Model Context Protocol tools
- **CLI** -- command-line tool with OAuth device flow, table/JSON output
- **Angular Web UI** -- definitions browser, scope editor, secret management, audit views
- **Dual Database** -- PostgreSQL (shared) or SQLite (embedded/Conductor)

## Quick Start

```bash
# Docker Compose (PostgreSQL mode)
docker compose up --build
# API: https://localhost:5300 | Swagger: https://localhost:5300/swagger

# Native development (SQLite mode)
dotnet run --project src/Andy.Settings.Api
cd client && npm install && npm start
```

## Default Ports

| Service | Port |
|---------|------|
| API HTTPS | 5300 |
| API HTTP | 5301 |
| PostgreSQL | 5438 |
| Angular dev | 4200 |
| Conductor proxy | 9100 |
| Conductor settings | 9107 |
| Andy Auth | 5001 |
| Andy RBAC | 5003 |

## Documentation

Full documentation: [rivoli-ai.github.io/andy-settings](https://rivoli-ai.github.io/andy-settings/)

- [Architecture](docs/architecture.md) -- system design, domain model, scope resolution
- [Features](docs/features.md) -- complete feature catalog
- [Security](docs/security.md) -- auth, RBAC, secret encryption
- [Stories](docs/stories.md) -- implementation backlog (11 epics, ~45 stories)
- [Examples](docs/examples.md) -- multi-language API/MCP examples
