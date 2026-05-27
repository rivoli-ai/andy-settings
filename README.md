# Andy Settings

Centralized configuration and settings management for the Andy ecosystem.

## Architecture

```
andy-settings/
├── src/
│   ├── Andy.Settings.Domain/             # Entities, enums (no dependencies)
│   ├── Andy.Settings.Application/        # Interfaces, DTOs, options (-> Domain)
│   ├── Andy.Settings.Infrastructure/     # EF Core, repositories, services (-> Domain, Application)
│   ├── Andy.Settings.Api/                # ASP.NET Core REST + MCP + Swagger (-> all)
│   ├── Andy.Settings.Client/             # Consumer HTTP client + OBO/M2M token providers
│   ├── Andy.Settings.Client.TestSupport/ # In-memory fakes for client consumers
│   └── Andy.Settings.Shared/             # Shared types
├── tools/
│   └── Andy.Settings.Cli/                # CLI tool (System.CommandLine + Spectre.Console)
├── tests/
│   ├── Andy.Settings.Tests.Unit/         # Unit tests (xUnit, Moq, FluentAssertions)
│   ├── Andy.Settings.Tests.Integration/  # Integration tests (WebApplicationFactory)
│   ├── Andy.Settings.Api.Tests/          # API tests
│   └── Andy.Settings.Client.Tests/       # Client library tests
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
- **Conductor Integration** -- embedded as a service in the Conductor macOS app (port 9111, SQLite)
- **Auth & RBAC** -- Andy Auth (OIDC) + Andy RBAC with scope-aware permissions
- **REST + Swagger + MCP** -- full API with OpenAPI docs and Model Context Protocol tools
- **CLI** -- command-line tool with OAuth device flow, table/JSON output
- **Angular Web UI** -- definitions browser, scope editor, secret management, audit views
- **Dual Database** -- PostgreSQL (shared) or SQLite (embedded/Conductor)

## Quick Start

```bash
# Docker Compose (PostgreSQL mode)
docker compose up --build
# API: https://localhost:7300 | Swagger: https://localhost:7300/swagger

# Native development (SQLite mode)
dotnet run --project src/Andy.Settings.Api
cd client && npm install && npm start
# Native API: https://localhost:5300 | Swagger: https://localhost:5300/swagger
```

## Default Ports

| Service | Port |
|---------|------|
| API HTTPS (native dev) | 5300 |
| API HTTP (native dev) | 5301 |
| API HTTPS (docker) | 7300 |
| API HTTP (docker) | 7301 |
| PostgreSQL (docker) | 7438 |
| Angular dev | 4200 |
| Conductor proxy | 9100 |
| Conductor settings | 9111 |
| Andy Auth | 5001 |
| Andy RBAC | 5003 |

## Commands and events

andy-settings follows the command / event split codified in [ADR 0001 — Messaging](https://github.com/rivoli-ai/andy-tasks/blob/main/docs/adr/0001-messaging.md):

- **Commands stay on HTTP.** Every imperative — set a value, create a definition, fetch a resolved setting — is a synchronous REST call against the API.
- **Events go on NATS.** Each successful write enqueues a `config.*.changed` event on the JetStream `ANDY_DOMAIN` stream. Consumers (e.g. `andy-mcp-proxy`) subscribe to the slice that names them; andy-settings never subscribes to itself.

The full event catalog and consumer onboarding guide is in [docs/messaging.md](docs/messaging.md).

## Documentation

Full documentation: [rivoli-ai.github.io/andy-settings](https://rivoli-ai.github.io/andy-settings/)

- [Architecture](docs/architecture.md) -- system design, domain model, scope resolution
- [Features](docs/features.md) -- complete feature catalog
- [Security](docs/security.md) -- auth, RBAC, secret encryption
- [Messaging](docs/messaging.md) -- NATS event catalog, subscriber onboarding, burst handling
- [Stories](docs/stories.md) -- implementation backlog (11 epics, ~45 stories)
- [Examples](docs/examples.md) -- multi-language API/MCP examples
