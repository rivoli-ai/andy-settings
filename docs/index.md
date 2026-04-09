# Andy Settings

Centralized configuration and settings management for the Andy ecosystem.

## What is Andy Settings?

Andy Settings provides typed setting definitions, scoped value assignments, encrypted secrets, and audit history for all Andy services. It runs embedded inside the Conductor macOS app (SQLite) or as a standalone service (PostgreSQL), and exposes configuration through REST API, MCP tools, CLI, and Angular web UI.

## Key Features

| Feature | Description |
|---------|-------------|
| **Typed Definitions** | Strongly typed setting schemas with validation, defaults, and UI metadata |
| **Scoped Resolution** | 8-level scope precedence with deterministic resolution and explanation |
| **Encrypted Secrets** | AES-256-GCM encryption via Data Protection API, RBAC-gated access |
| **Audit Trail** | Append-only change history with actor, scope, and diff metadata |
| **Conductor Integration** | Embedded as 8th service in Conductor macOS app (port 9107) |
| **REST + Swagger** | Full CRUD API with OpenAPI documentation |
| **MCP Tools** | Model Context Protocol tools for AI assistant integration |
| **CLI** | Command-line tool with table/JSON output and OAuth device flow |
| **Angular Web UI** | Definitions browser, scope editor, secret management, audit views |
| **Auth & RBAC** | Andy Auth (OIDC) + Andy RBAC with scope-aware permissions |
| **Dual Database** | PostgreSQL (shared) or SQLite (embedded/Conductor) |
| **Multi-Language Examples** | C#, Python, JavaScript, Go, Rust, PowerShell |

## Quick Links

- [Getting Started](getting-started.md) -- Set up your development environment
- [Architecture](architecture.md) -- System design, domain model, scope resolution
- [Features](features.md) -- Complete feature catalog
- [Security](security.md) -- Authentication, authorization, secret encryption
- [Docker Setup](devops.md) -- Ports, volumes, certificates, CI/CD
- [Implementation](implementation.md) -- Phases, interfaces, REST routes
- [Stories](stories.md) -- Implementation backlog (11 epics, ~45 stories)
- [Examples](examples.md) -- Multi-language API and MCP usage examples

## Services (docker compose up)

| Service | URL | Description |
|---------|-----|-------------|
| API (HTTPS) | https://localhost:5300 | REST + MCP + Swagger |
| API (HTTP) | http://localhost:5301 | HTTP access |
| PostgreSQL | localhost:5438 | Database |
| Angular | https://localhost:4200 | Dev server (ng serve) |
| Andy Auth | https://localhost:5001 | OAuth 2.0 / OIDC |
| Andy RBAC | https://localhost:5003 | RBAC permission server |

## Conductor Embedded Mode

| Property | Value |
|----------|-------|
| Port | 9107 |
| Proxy prefix | `/settings` |
| Database | SQLite |
| Access | `http://localhost:9100/settings/*` via UnifiedProxy |
