# Andy Settings — Implementation Plan

## Implementation Strategy

Implement Andy Settings in vertical slices so the product is usable early in local desktop mode and naturally evolves into a team-capable shared service.

Recommended sequence:

1. core domain and persistence
2. API and typed resolution
3. .NET options integration
4. Angular web client
5. CLI
6. MCP server
7. auth/rbac hardening
8. import/export and audit refinements
9. PostgreSQL shared-mode support

## Technology Stack

### Backend

- .NET 8
- ASP.NET Core
- EF Core
- SQLite (initial)
- PostgreSQL (shared mode)
- OpenAPI / Swagger
- gRPC
- SignalR or SSE for live updates

### Frontend

- Angular
- npm-based workflow
- component library and auth patterns aligned with Andy UI approach
- OIDC/OAuth2 client integration

### Security / Identity

- Andy Auth for authentication
- Andy RBAC for authorization
- macOS Keychain for local secret storage

### Tooling

- Docker
- Docker Compose
- GitHub Actions
- xUnit / FluentAssertions / integration test stack

## Solution Structure

Suggested solution/projects:

```text
src/
  Andy.Settings/
  Andy.Settings.Abstractions/
  Andy.Settings.Api/
  Andy.Settings.Client/
  Andy.Settings.AspNetCore/
  Andy.Settings.EntityFramework/
  Andy.Settings.Secrets.Keychain/
  Andy.Settings.Cli/
  Andy.Settings.Mcp/

tests/
  Andy.Settings.Tests/
  Andy.Settings.Api.Tests/
  Andy.Settings.Client.Tests/
  Andy.Settings.IntegrationTests/
  Andy.Settings.Mcp.Tests/

client/
  andy-settings-web/
```

## Backend Projects

### `Andy.Settings.Abstractions`

Contains:

- core interfaces
- DTO contracts
- scope models
- setting definition models
- resolution result models
- audit abstractions
- secret store abstractions

Should have no infrastructure dependencies.

### `Andy.Settings`

Contains:

- resolution engine
- validation logic
- definition registry services
- mutation orchestration
- audit event creation
- import/export orchestration

### `Andy.Settings.EntityFramework`

Contains:

- EF Core DbContext
- entity mappings
- repository implementations
- migrations
- SQLite and PostgreSQL provider support

### `Andy.Settings.Secrets.Keychain`

Contains:

- Keychain secret store implementation
- local secret reference creation
- retrieval/rotation logic
- test doubles for non-macOS environments

### `Andy.Settings.Client`

Contains:

- REST client
- gRPC client wrappers
- auth token integration hooks
- convenience APIs for resolution and mutation

### `Andy.Settings.AspNetCore`

Contains:

- DI registration helpers
- `AddAndySettingsClient(...)`
- configuration provider bridge
- typed options binding helpers
- live-refresh background integration

### `Andy.Settings.Api`

Contains:

- REST controllers or minimal APIs
- gRPC services
- OpenAPI generation
- authn/authz integration
- health checks
- optional SignalR hub or SSE endpoints

### `Andy.Settings.Cli`

Contains:

- command parsing
- auth flows
- JSON/table output
- shared client SDK usage
- import/export commands
- secret mutation commands

### `Andy.Settings.Mcp`

Contains:

- MCP server host
- tool definitions
- auth/token acquisition strategy
- mapping from tools to application services

## Frontend Implementation

## Angular App

Suggested app structure:

```text
client/andy-settings-web/
  src/app/
    core/
    features/
      definitions/
      values/
      effective/
      secrets/
      audit/
      services/
      import-export/
    shared/
```

Feature areas:

- settings definitions list/detail
- settings values editor
- effective value explorer
- audit log explorer
- secret rotation UI
- service registry UI
- import/export UI

## UI Components

Needed component set:

- settings search bar
- category tree
- scope picker
- actor/context header
- type-aware editors
- secret editor
- value resolution panel
- audit timeline
- validation summary panel
- diff panel

## Backend Domain Interfaces

Suggested interfaces:

```csharp
public interface ISettingDefinitionRegistry
{
    Task RegisterAsync(SettingDefinition definition, CancellationToken ct = default);
    Task<SettingDefinition?> GetAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<SettingDefinition>> SearchAsync(DefinitionQuery query, CancellationToken ct = default);
}

public interface ISettingsResolver
{
    Task<ResolvedSetting> ResolveAsync(string key, SettingsContext context, CancellationToken ct = default);
    Task<IReadOnlyList<ResolvedSetting>> ResolveManyAsync(IEnumerable<string> keys, SettingsContext context, CancellationToken ct = default);
}

public interface ISettingsWriter
{
    Task SetAsync(SettingMutation mutation, CancellationToken ct = default);
    Task DeleteAsync(SettingDeleteCommand command, CancellationToken ct = default);
}

public interface ISecretStore
{
    Task<SecretReference> PutAsync(SecretWriteCommand command, CancellationToken ct = default);
    Task<SecretPayload?> GetAsync(SecretReadCommand command, CancellationToken ct = default);
    Task RotateAsync(SecretRotateCommand command, CancellationToken ct = default);
}
```

## Database Implementation Notes

### EF Core Entities

Implement separate entities for:

- definitions
- assignments
- secret references
- audit events
- service registrations

### Concurrency

Use optimistic concurrency for setting assignments.

Suggested fields:

- `version`
- `etag`
- rowversion equivalent where supported

### Migrations

- keep EF migrations under source control
- support SQLite-specific and PostgreSQL-compatible migrations
- initial migration should target local-first mode first

## Resolution Engine Implementation

### Algorithm

For a given key and context:

1. load setting definition
2. determine allowed scopes
3. query all matching assignments ordered by precedence
4. apply precedence rules
5. validate resolved value against definition
6. attach explanation metadata
7. return resolved object

### Explanation Model

Each resolution result should include:

- requested key
- resolved value
- winning scope
- source chain
- overridden entries
- validation outcome
- timestamp metadata

This is important for API, CLI, MCP, and UI parity.

## API Implementation Details

### REST Routes

Suggested initial routes:

```text
GET    /api/definitions
GET    /api/definitions/{key}
POST   /api/definitions
PUT    /api/definitions/{key}
DELETE /api/definitions/{key}

GET    /api/values/{key}
POST   /api/values/{key}
DELETE /api/values/{key}

POST   /api/effective/resolve
POST   /api/effective/resolve-batch

POST   /api/secrets/{key}
POST   /api/secrets/{key}/rotate
DELETE /api/secrets/{key}

GET    /api/audit
GET    /api/audit/{id}

POST   /api/import
GET    /api/export
```

### gRPC Services

Define protobuf contracts for:

- list/search definitions
- get/set/delete assignments
- resolve effective values
- list audit entries
- register services

Keep protobufs in `proto/` so other services can consume them directly.

## CLI Implementation Details

Suggested command groups:

- `auth`
- `definitions`
- `values`
- `effective`
- `secrets`
- `audit`
- `services`
- `import`
- `export`
- `bootstrap`

Example:

```bash
andy-settings bootstrap init
andy-settings auth login
andy-settings definitions search --app andy-codeindex
andy-settings values set andy.docs.defaultSource filesystem --scope machine
andy-settings effective get andy.auth.authority --user alice@example.com
andy-settings secrets rotate andy.llm.openai.apiKey --scope user
```

## MCP Implementation Details

### Server Host

Build the MCP server as a .NET host using the shared client/domain services.

### Tool Design

Tools should be explicit, typed, and policy-aware.

Examples:

- `settings_search_definitions`
- `settings_get_effective_value`
- `settings_set_scoped_value`
- `settings_explain_value_resolution`
- `settings_get_audit_events`

### Safety Controls

- mutation tools must validate authorization
- secret reads must be explicitly permission-gated
- destructive changes should require explicit parameters

## Auth and RBAC Implementation

### Andy Auth

Implement:

- bearer token validation in API
- SPA login for Angular app
- CLI login flow compatible with public clients
- local bootstrap bypass for localhost-only first run

### Andy RBAC

Implement policy checks in the application layer, not just controllers.

Examples:

- definition writes require `andy-settings:definition:write`
- value writes require `andy-settings:value:write`
- secret reads require `andy-settings:secret:read`
- team mutations require `andy-settings:team:admin`

## Docker Implementation

### Containers

Provide at least:

- API container
- Web container
- optional PostgreSQL container

### Local Dev Compose

Compose should support:

- API
- Web
- PostgreSQL
- optional Auth dependency
- optional RBAC dependency

For purely local-first mode, API can run with SQLite and Keychain access outside full containerized production-like flows.

## Implementation Phases

### Phase 1 — Core Local MVP

- definitions
- assignments
- SQLite persistence
- resolution engine
- REST API
- basic Angular app
- local bootstrap

### Phase 2 — .NET Integration + CLI

- `Andy.Settings.Client`
- `Andy.Settings.AspNetCore`
- `IOptions<T>` bridge
- CLI support
- import/export

### Phase 3 — MCP + Live Refresh

- MCP server
- SignalR/SSE refresh pipeline
- richer diagnostics and explanation

### Phase 4 — Auth/RBAC Hardening

- full OIDC login flows
- RBAC-backed authorization checks
- team-scope operations
- audit refinement

### Phase 5 — Shared Deployment Mode

- PostgreSQL provider
- remote deployment model
- sync/export improvements

## Definition Bootstrapping

Ship seed definitions for:

- `andy.auth.*`
- `andy.rbac.*`
- `andy.containers.*`
- `andy.codeindex.*`
- `andy.devpilot.*`
- `andy.docs.*`
- `andy.host.*`

These seed definitions should be loaded through startup registration or migration seeding.

## Backward Compatibility Strategy

- allow services to continue using `appsettings.json` during migration
- add an Andy Settings configuration provider that overlays or replaces static configuration
- migrate service-by-service rather than big-bang replacement

## Deliverables

Initial repo deliverables should include:

- working .NET 8 solution
- API container
- Angular web client
- CLI tool
- MCP server
- Docker Compose local development workflow
- seed definitions
- integration tests
- OpenAPI and protobuf contracts
