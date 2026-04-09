# Implementation Plan

## Strategy

Implement Andy Settings in vertical slices so the product is usable early in embedded mode (Conductor) and naturally evolves into a shared team service.

Recommended sequence:

1. Core domain and persistence (SQLite first)
2. API, typed resolution, and Swagger
3. Auth and RBAC integration
4. Angular web client
5. CLI
6. MCP server
7. Import/export and audit refinements
8. Conductor integration
9. PostgreSQL shared-mode support
10. Examples and documentation

## Technology Stack

### Backend

- .NET 8
- ASP.NET Core
- Entity Framework Core 8
- SQLite (embedded/Conductor mode)
- PostgreSQL (shared/team mode)
- Swagger / OpenAPI (Swashbuckle)
- Serilog (structured logging)
- ASP.NET Core Data Protection (secret encryption)

### Frontend

- Angular 18 (standalone components)
- SCSS
- angular-auth-oidc-client
- Proxy to backend API

### Security / Identity

- Andy Auth (OAuth 2.0 / OIDC)
- Andy RBAC (authorization)
- Data Protection API for secret encryption

### Tooling

- Docker / Docker Compose
- MkDocs Material (documentation)
- xUnit / FluentAssertions / Moq
- GitHub Actions

## Solution Structure

```text
src/
  Andy.Settings.Domain/            # Entities, enums
  Andy.Settings.Application/       # Interfaces, DTOs, options, validation
  Andy.Settings.Infrastructure/    # EF Core, repositories, services, telemetry
  Andy.Settings.Api/               # REST controllers, MCP tools, Program.cs
  Andy.Settings.Shared/            # Shared types for external consumers

tools/
  Andy.Settings.Cli/               # CLI tool (System.CommandLine + Spectre.Console)

tests/
  Andy.Settings.Tests.Unit/        # Unit tests
  Andy.Settings.Tests.Integration/ # Integration tests (WebApplicationFactory)

client/                            # Angular 18 SPA
examples/                          # Multi-language API/MCP examples
docs/                              # MkDocs documentation
```

## Domain Interfaces

```csharp
public interface IDefinitionService
{
    Task<SettingDefinition?> GetAsync(string key, CancellationToken ct = default);
    Task<PagedResult<SettingDefinition>> SearchAsync(DefinitionQuery query, CancellationToken ct = default);
    Task<SettingDefinition> CreateAsync(CreateDefinitionDto dto, CancellationToken ct = default);
    Task UpdateAsync(string key, UpdateDefinitionDto dto, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}

public interface IResolutionService
{
    Task<ResolvedSetting> ResolveAsync(string key, ResolutionContext context, CancellationToken ct = default);
    Task<IReadOnlyList<ResolvedSetting>> ResolveManyAsync(IEnumerable<string> keys, ResolutionContext context, CancellationToken ct = default);
}

public interface IAssignmentService
{
    Task SetAsync(SetValueDto dto, CancellationToken ct = default);
    Task DeleteAsync(DeleteValueDto dto, CancellationToken ct = default);
}

public interface ISecretService
{
    Task SetSecretAsync(SetSecretDto dto, CancellationToken ct = default);
    Task<string?> GetSecretAsync(GetSecretDto dto, CancellationToken ct = default);
    Task RotateSecretAsync(RotateSecretDto dto, CancellationToken ct = default);
}

public interface IAuditService
{
    Task<PagedResult<AuditEvent>> QueryAsync(AuditQuery query, CancellationToken ct = default);
}

public interface IExportImportService
{
    Task<ExportResult> ExportAsync(ExportOptions options, CancellationToken ct = default);
    Task<ImportPreview> PreviewImportAsync(Stream data, CancellationToken ct = default);
    Task<ImportResult> ImportAsync(Stream data, ImportOptions options, CancellationToken ct = default);
}
```

## REST Routes

```text
GET    /api/definitions
GET    /api/definitions/{key}
POST   /api/definitions
PUT    /api/definitions/{key}
DELETE /api/definitions/{key}

GET    /api/values?definitionKey={key}&scopeType={scope}&scopeId={id}
POST   /api/values
DELETE /api/values/{id}

POST   /api/effective/resolve
POST   /api/effective/resolve-batch
POST   /api/effective/explain

POST   /api/secrets/{definitionKey}
POST   /api/secrets/{definitionKey}/rotate
DELETE /api/secrets/{definitionKey}

GET    /api/audit
GET    /api/audit/{id}

POST   /api/import
POST   /api/import/preview
GET    /api/export

GET    /api/health
```

## Implementation Phases

### Phase 1 -- Core Domain and Persistence

- Domain entities and enums
- Application interfaces and DTOs
- EF Core DbContext with dual provider support (SQLite + PostgreSQL)
- Initial migration
- Repository implementations
- Resolution engine with scope precedence
- Validation service
- Data seeder

### Phase 2 -- API Layer

- Program.cs with full middleware pipeline
- DefinitionsController
- ValuesController
- EffectiveController (resolution + explanation)
- SecretsController (encrypted secrets)
- AuditController
- ImportExportController
- Health endpoint
- Swagger configuration

### Phase 3 -- Auth and RBAC

- Andy Auth JWT Bearer integration
- Andy RBAC client with permission checks
- Development fallback (AllowAllDevPolicyProvider)
- Scope-aware authorization (user can only access own user-scoped settings)
- MCP OAuth protected resource metadata

### Phase 4 -- Frontend

- Auth service with OIDC
- Layout and navigation (matching Andy ecosystem style)
- Definitions browser
- Values editor (type-aware, scope-aware)
- Effective value explorer with explanation
- Secret editor (masked, RBAC-gated reveal)
- Audit timeline
- Import/export views

### Phase 5 -- CLI

- Auth commands (login, logout)
- Definition commands (list, search)
- Value commands (get, set, delete, explain)
- Export/import commands
- Table and JSON output via Spectre.Console

### Phase 6 -- MCP

- MCP tools implementation
- OAuth metadata endpoints
- CORS for MCP clients

### Phase 7 -- Conductor Integration

- Verify SQLite embedded mode works end-to-end
- Document Conductor `SettingsServiceConfig` setup
- Seed definitions for all Andy services
- Test with Conductor's ServiceOrchestrator and UnifiedProxy

### Phase 8 -- Polish

- OpenTelemetry instrumentation
- Examples in 6 languages
- MkDocs documentation
- Docker optimization
- PostgreSQL shared-mode verification

## Seed Definitions

Ship seed definitions for all Andy services:

- `andy.auth.*` -- authority, audience, token lifetimes
- `andy.rbac.*` -- API base URL, application codes, cache TTL
- `andy.containers.*` -- default provider, resource limits, template paths
- `andy.codeindex.*` -- embedding provider, model, chunk sizes
- `andy.devpilot.*` -- LLM provider, model, agent settings
- `andy.docs.*` -- source paths, rendering options
- `andy.settings.*` -- self-configuration (meta settings)
