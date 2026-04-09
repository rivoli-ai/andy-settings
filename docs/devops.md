# Andy Settings — DevOps and Containerization

## Overview

Andy Settings should follow the same general operational posture as the broader Andy backend projects:

- source-controlled infrastructure and local development workflow
- Docker-first local orchestration
- ASP.NET Core backend container
- Angular web frontend container
- optional PostgreSQL in Compose
- development certificates and config folders checked into repo as appropriate for local development

The goal is to make local startup easy while keeping a clean path to hosted deployment.

## Repository Operations Layout

Suggested top-level repo layout:

```text
andy-settings/
├── .github/
│   └── workflows/
├── certs/
├── config/
├── docs/
├── openapi/
├── proto/
├── scripts/
├── src/
├── tests/
├── client/
│   └── andy-settings-web/
├── docker-compose.yml
├── Dockerfile.api
├── Dockerfile.web
├── Directory.Build.props
├── Directory.Packages.props
└── Andy.Settings.sln
```

## Local Development Modes

### Mode 1 — Native Local Dev

Use when developing quickly on macOS.

- API runs with `dotnet run`
- web runs with Angular dev server
- SQLite used locally
- optional local Andy Auth / Andy RBAC dependencies

### Mode 2 — Docker Compose Dev

Use when validating end-to-end containerized behavior.

- API container
- web container
- PostgreSQL container optional
- Andy Auth and Andy RBAC optional as external/local dependencies

This mode should resemble the local developer workflow used in other Andy backends.

## Containerization Strategy

## API Container

### Goals

- reproducible .NET 8 runtime image
- production-style startup path
- minimal runtime surface
- configuration by env vars and mounted config

### Suggested Dockerfile Pattern

Multi-stage build:

1. SDK build stage
2. publish stage
3. slim ASP.NET runtime stage

Key points:

- restore separately for cache efficiency
- publish trimmed only if it does not complicate diagnostics too early
- run as non-root where practical
- mount certs/config as volumes in local Compose

## Web Container

### Goals

- Angular application built once
- served as static assets via Nginx or another minimal web server
- environment-specific API URL handling

Suggested pattern:

1. Node build stage
2. static runtime stage

## Database Strategy

### Local-First Baseline

- SQLite is the first-class local persistence backend
- useful for native local dev and desktop mode

### Shared / Integration Mode

- PostgreSQL container for integration and production-like local testing

Compose should allow switching between:

- SQLite mode
- PostgreSQL mode

## Docker Compose

### Compose Goals

- one command startup for developers
- mirrors backend/frontend separation
- optional inclusion of auth/rbac dependencies
- simple health-check-driven startup

### Suggested Services

- `andy-settings-api`
- `andy-settings-web`
- `postgres` (optional/profile-based)
- optional external references to `andy-auth` and `andy-rbac`

### Example Environment Variables

#### API

- `ASPNETCORE_ENVIRONMENT=Development`
- `ASPNETCORE_URLS=https://+:5443;http://+:5442`
- `AndySettings__Database__Provider=Sqlite`
- `AndySettings__Database__ConnectionString=Data Source=/data/andy-settings.db`
- `AndySettings__Auth__Authority=https://host.docker.internal:5001`
- `AndySettings__Rbac__BaseUrl=https://host.docker.internal:7003`

#### Web

- `API_BASE_URL=https://localhost:5443`
- `OIDC_AUTHORITY=https://localhost:5001`
- `OIDC_CLIENT_ID=andy-settings-web`

## Certificates

Use a `certs/` directory for local development certificates.

Recommendations:

- local dev certs only
- clear documentation that they are not for production
- API and web trust instructions in docs
- explicit mount into containers for local HTTPS

## Config Directory

Use `config/` for:

- example appsettings files
- seed definition files
- local profile examples
- optional import/export examples

Suggested contents:

```text
config/
  appsettings.Development.json
  settings-seed.json
  local-profiles/
    host-app.json
    containers.json
    codeindex.json
```

## Build and Release Pipeline

## CI Workflow Stages

### 1. Build and Lint

- restore .NET dependencies
- restore Node dependencies
- build solution
- build Angular client
- run analyzers/lint

### 2. Test

- unit tests
- integration tests
- CLI tests
- MCP tests
- Angular tests

### 3. Container Build

- build API image
- build web image
- smoke startup checks

### 4. Optional Publish

- publish container images
- publish NuGet packages for shared libraries if versioned release
- publish CLI artifacts

## Suggested GitHub Actions Workflows

- `ci.yml`
- `containers.yml`
- `release.yml`
- `docs.yml` (optional)

## Release Artifacts

Potential release outputs:

- API container image
- web container image
- NuGet packages:
  - `Andy.Settings`
  - `Andy.Settings.Client`
  - `Andy.Settings.AspNetCore`
- CLI package or executable artifact
- OpenAPI spec
- protobuf contracts

## Environment Strategy

Suggested environments:

- local
- dev
- test
- staging
- production

### Configuration by Environment

Use environment overlays for:

- database provider
- connection strings
- auth authority
- RBAC endpoint
- logging levels
- feature toggles

## Health Checks and Observability

## Health Checks

Implement:

- liveness
- readiness
- database connectivity
- Keychain backend availability (where meaningful)
- auth dependency reachability (optional warning-level in local mode)
- RBAC dependency reachability (optional warning-level in local mode)

## Logging

Use structured logging.

Log categories:

- API requests
- authorization denials
- configuration mutations
- resolution diagnostics
- CLI interactions where applicable
- MCP tool usage metadata

Do not log secrets or tokens.

## Metrics

Recommended future metrics:

- resolution count / latency
- mutation count
- audit event count
- secret rotation count
- auth failures
- RBAC denials
- import/export counts

## Development Scripts

Suggested helper scripts in `scripts/`:

- `dev-up.sh`
- `dev-down.sh`
- `seed-settings.sh`
- `run-api.sh`
- `run-web.sh`
- `run-cli-smoke.sh`
- `run-mcp-smoke.sh`
- `reset-local-db.sh`

## Local Compose Profiles

Recommended Compose profiles:

- `default`: API + web + SQLite volume
- `postgres`: API + web + postgres
- `full`: API + web + postgres + auth + rbac dependencies if bundled for local testing

## Data Volumes

Suggested volumes:

- API data volume for SQLite/local data
- PostgreSQL data volume when using Postgres profile
- optional logs volume in local debugging scenarios

## Hosted Deployment Considerations

Later shared deployments should support:

- managed PostgreSQL
- reverse proxy / ingress
- TLS termination
- external secret backend if needed
- externalized auth and rbac URLs

The application should remain portable between local Docker Compose and hosted container environments.

## Database Migration Operations

Provide:

- startup migration option for development
- explicit migration job or command for shared deployments
- rollback guidance in docs

Recommended command support:

```bash
dotnet ef database update --project src/Andy.Settings.EntityFramework --startup-project src/Andy.Settings.Api
```

## Bootstrapping and Seeding

Seeding should support:

- base setting definitions
- sample applications/services
- optional local admin bootstrap metadata

Seeding modes:

- automatic dev seed in development only
- explicit CLI/API seed command for controlled environments

## Operational Readiness Checklist

Before first usable release:

- API container builds successfully
- web container builds successfully
- Compose local environment starts cleanly
- health checks pass
- migrations apply in SQLite and PostgreSQL modes
- auth and RBAC integration configurable
- CLI can reach running API
- MCP server can reach running API

## Recommended Initial Commands

### Native Development

```bash
# API
cd src/Andy.Settings.Api
dotnet run

# Web
cd client/andy-settings-web
npm install
npm start
```

### Docker Compose

```bash
docker compose up --build
```

### Optional PostgreSQL Profile

```bash
docker compose --profile postgres up --build
```

## Documentation Expectations

Operational docs should eventually include:

- local setup
- compose setup
- certificates and HTTPS
- environment variables
- auth/rbac wiring
- migration operations
- backup/export guidance
- troubleshooting
