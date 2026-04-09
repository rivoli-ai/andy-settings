# Implementation Stories

Stories organized by epic. Each story has acceptance criteria and references the corresponding project. Stories should be implemented roughly in epic order, though many stories within an epic can be parallelized.

---

## Epic 1: Domain Model & Core Abstractions

### Story 1.1: Define domain entities

**Project:** `Andy.Settings.Domain`

Create the core domain entities:

- `SettingDefinition` -- schema for a setting (key, applicationCode, displayName, description, category, dataType, defaultValueJson, validationJson, uiSchemaJson, isSecret, allowedScopesJson, tagsJson, isDeprecated, timestamps)
- `SettingAssignment` -- a value at a specific scope (definitionId, scopeType, scopeId, valueJson, etag, version, updatedBy, timestamps)
- `EncryptedSecret` -- encrypted secret value (definitionId, scopeType, scopeId, encryptedValue, updatedBy, timestamps)
- `AuditEvent` -- append-only change record (eventType, definitionKey, scopeType, scopeId, actorType, actorId, beforeJson, afterJson, correlationId, timestamp)

**Acceptance criteria:**
- [ ] All entities use Guid IDs and DateTimeOffset timestamps
- [ ] Navigation properties defined between Definition and Assignment/Secret
- [ ] `SettingDefinition.Key` is unique per application code
- [ ] `EncryptedSecret.EncryptedValue` stores Data Protection API output

### Story 1.2: Define domain enums

**Project:** `Andy.Settings.Domain`

- `SettingDataType` -- String, Integer, Boolean, Decimal, Enum, Duration, Uri, Json, StringList, Secret
- `ScopeType` -- Machine, Application, Service, User, Team, Workspace, RuntimeOverride
- `AuditEventType` -- Created, Updated, Deleted, SecretRotated, Imported, Exported

**Acceptance criteria:**
- [ ] Enums cover all scope levels and data types
- [ ] JSON serialization works correctly

### Story 1.3: Define application interfaces

**Project:** `Andy.Settings.Application`

Create service interfaces:

- `IDefinitionService` -- CRUD for setting definitions (Get, Search, Create, Update, Delete)
- `IResolutionService` -- resolve effective values (Resolve, ResolveBatch, Explain)
- `IAssignmentService` -- manage scoped values (Set, Delete, ListByScope)
- `ISecretService` -- encrypted secret management (SetSecret, GetSecret, RotateSecret)
- `IAuditService` -- query audit history (Query, GetById)
- `IExportImportService` -- bulk operations (Export, PreviewImport, Import)
- `IValidationService` -- validate values against definition schemas

**Acceptance criteria:**
- [ ] All interfaces use async patterns with CancellationToken
- [ ] DTOs defined for all input/output contracts
- [ ] `IResolutionService.Explain` returns the full source chain with explanation

### Story 1.4: Define DTOs and options

**Project:** `Andy.Settings.Application`

- Request/response DTOs for all API contracts (records where appropriate)
- `ResolutionContext` -- subject context for scope resolution (userId, teamId, workspaceId, applicationCode, serviceCode)
- `ResolvedSetting` -- resolution result (effectiveValue, winningScopeType, winningScopeId, sourceChain, validationResult, explanation)
- `DatabaseOptions` with Provider property (PostgreSql/Sqlite)
- Configuration options for all appsettings.json sections

**Acceptance criteria:**
- [ ] Validation attributes on request DTOs
- [ ] `ResolvedSetting` includes full explanation metadata
- [ ] Options classes match appsettings.json structure

---

## Epic 2: Database & Infrastructure

### Story 2.1: Create EF Core DbContext with dual provider support

**Project:** `Andy.Settings.Infrastructure`

Implement `SettingsDbContext` with:

- DbSets: SettingDefinitions, SettingAssignments, EncryptedSecrets, AuditEvents
- Fluent API: indexes on Key+ApplicationCode, ScopeType+ScopeId, audit timestamps
- PostgreSQL: `jsonb` for JSON columns
- SQLite: `TEXT` for JSON columns
- `DatabaseProviderExtensions` for switching via `Database:Provider` config
- SQLite default path: `~/.andy-settings/andy-settings.sqlite`
- Conductor path: `~/Library/Application Support/ai.rivoli.conductor/db/andy-settings.sqlite`

**Acceptance criteria:**
- [ ] Provider switching works via config key
- [ ] Unique constraint on `SettingDefinition.Key` + `ApplicationCode`
- [ ] Composite index on `SettingAssignment(DefinitionId, ScopeType, ScopeId)`
- [ ] AuditEvents table has no UPDATE/DELETE operations

### Story 2.2: Create initial migration

**Project:** `Andy.Settings.Infrastructure`

- Generate `InitialCreate` migration
- Include `DesignTimeDbContextFactory`
- Verify on both PostgreSQL and SQLite

**Acceptance criteria:**
- [ ] Migration applies cleanly on fresh PostgreSQL and SQLite

### Story 2.3: Implement repositories

**Project:** `Andy.Settings.Infrastructure`

- `DefinitionRepository` -- CRUD with search/filter/pagination
- `AssignmentRepository` -- CRUD with scope-based queries
- `SecretRepository` -- encrypted value storage
- `AuditRepository` -- append-only writes, read with filtering
- `RepositoryBase<T>` -- common CRUD operations

**Acceptance criteria:**
- [ ] Efficient include queries
- [ ] Pagination on all list operations
- [ ] AuditRepository has no Update/Delete methods

### Story 2.4: Implement resolution engine

**Project:** `Andy.Settings.Infrastructure`

Implement `ResolutionService`:

1. Load setting definition
2. Determine allowed scopes
3. Query all matching assignments ordered by precedence
4. Apply precedence rules (RuntimeOverride > Workspace > Team > User > Service > Application > Machine > Default)
5. Validate resolved value against definition
6. Build explanation metadata (source chain, winning scope, overridden entries)

**Acceptance criteria:**
- [ ] Deterministic precedence order
- [ ] Returns default from definition when no assignments exist
- [ ] Explanation includes all considered assignments
- [ ] Batch resolution is efficient (single query per scope level)

### Story 2.5: Implement secret encryption service

**Project:** `Andy.Settings.Infrastructure`

Using ASP.NET Core Data Protection API:

- Encrypt values before storage
- Decrypt only when caller has `secret:read` permission (checked in application layer)
- Support rotation (new encrypted value, old discarded)

**Acceptance criteria:**
- [ ] Values are AES-256-GCM encrypted
- [ ] Decryption requires authorization check
- [ ] Rotation emits audit event without payload

### Story 2.6: Implement data seeder

**Project:** `Andy.Settings.Api`

Seed definitions for all Andy services:

- `andy.auth.*` -- authority, audience, token lifetimes
- `andy.rbac.*` -- API base URL, application codes
- `andy.containers.*` -- default provider, resource limits
- `andy.codeindex.*` -- embedding provider, model
- `andy.devpilot.*` -- LLM provider, agent settings
- `andy.docs.*` -- source paths, rendering options
- `andy.settings.*` -- meta-configuration

**Acceptance criteria:**
- [ ] Idempotent (safe to run multiple times)
- [ ] Only seeds definitions, not values
- [ ] Runs on startup in Development environment

---

## Epic 3: API Layer

### Story 3.1: Configure Program.cs

**Project:** `Andy.Settings.Api`

Full middleware pipeline:

- Controllers, Swagger with JWT security definition, CORS
- Static files (Angular SPA from wwwroot) with SPA fallback
- Health endpoint (`/health`)
- Auto-migration in development
- Serilog structured logging
- Data Protection API registration

**Acceptance criteria:**
- [ ] `dotnet run` starts successfully
- [ ] Swagger UI at `/swagger` in development
- [ ] Health endpoint returns 200
- [ ] SPA fallback serves index.html for non-API routes

### Story 3.2: Implement DefinitionsController

- `GET /api/definitions` -- list with filtering/pagination
- `GET /api/definitions/{key}` -- get by key
- `POST /api/definitions` -- create
- `PUT /api/definitions/{key}` -- update
- `DELETE /api/definitions/{key}` -- delete

**Acceptance criteria:**
- [ ] RBAC: `[RequirePermission("definition:read/write/delete")]`
- [ ] Search by application code, category, tags
- [ ] Proper HTTP status codes and `[ProducesResponseType]`

### Story 3.3: Implement ValuesController

- `GET /api/values` -- list with scope filters
- `POST /api/values` -- set value (upsert)
- `DELETE /api/values/{id}` -- remove value
- `POST /api/values/bulk` -- bulk set

**Acceptance criteria:**
- [ ] RBAC: `[RequirePermission("value:read/write/delete")]`
- [ ] Scope-aware auth (users can only access own user-scoped values)
- [ ] Validates value against definition schema
- [ ] Optimistic concurrency via etag
- [ ] Audit event emitted on every mutation

### Story 3.4: Implement EffectiveController

- `POST /api/effective/resolve` -- resolve single key for context
- `POST /api/effective/resolve-batch` -- resolve multiple keys
- `POST /api/effective/explain` -- full explanation with source chain

**Acceptance criteria:**
- [ ] RBAC: `[RequirePermission("value:read")]`
- [ ] Returns `ResolvedSetting` with winning scope and explanation
- [ ] Batch is efficient (not N+1)

### Story 3.5: Implement SecretsController

- `POST /api/secrets/{definitionKey}` -- set encrypted secret
- `GET /api/secrets/{definitionKey}` -- get decrypted secret (requires `secret:read`)
- `POST /api/secrets/{definitionKey}/rotate` -- rotate secret
- `DELETE /api/secrets/{definitionKey}` -- remove secret

**Acceptance criteria:**
- [ ] RBAC: `[RequirePermission("secret:read/write")]`
- [ ] Values encrypted via Data Protection API
- [ ] 403 if caller lacks `secret:read` on GET
- [ ] Audit on all operations (payload excluded)

### Story 3.6: Implement AuditController

- `GET /api/audit` -- query with filters (key, dateRange, actor, eventType)
- `GET /api/audit/{id}` -- get single event

**Acceptance criteria:**
- [ ] RBAC: `[RequirePermission("audit:read")]`
- [ ] Pagination support
- [ ] Read-only (no mutation endpoints)

### Story 3.7: Implement ImportExportController

- `GET /api/export` -- export with format/scope/app filters
- `POST /api/import` -- import from JSON/YAML
- `POST /api/import/preview` -- preview without applying

**Acceptance criteria:**
- [ ] RBAC: `[RequirePermission("export:read")]` / `[RequirePermission("import:write")]`
- [ ] Secrets masked in export unless `includeSecrets=true` + `secret:read` permission
- [ ] Preview returns additions/modifications/deletions
- [ ] Import validates all entries before applying

---

## Epic 4: Authentication & Authorization

### Story 4.1: Integrate Andy Auth

**Project:** `Andy.Settings.Api`

- JWT Bearer with Andy Auth authority
- Development fallback (auto-assign dev user)
- SSL validation bypass for development
- `CurrentUserService` extracting claims from JWT
- MCP OAuth protected resource metadata endpoints

**Acceptance criteria:**
- [ ] 401 without valid token (non-dev mode)
- [ ] Dev mode allows unauthenticated access
- [ ] MCP well-known endpoints configured

### Story 4.2: Integrate Andy RBAC

**Project:** `Andy.Settings.Api`

- RBAC client with application code `settings`
- `[RequirePermission]` on all controller actions
- `AllowAllDevPolicyProvider` for development
- Scope-aware checks in application services (not just controllers)

**Acceptance criteria:**
- [ ] 403 when user lacks permission
- [ ] User can only access own user-scoped values
- [ ] Team admins can manage team-scoped values for their team
- [ ] Secret access requires explicit `secret:read`

---

## Epic 5: MCP Integration

### Story 5.1: Implement MCP tools

**Project:** `Andy.Settings.Api`

`SettingsMcpTools` class with `[McpServerToolType]`:

- `settings_list_definitions` -- browse by app/category
- `settings_get_effective` -- resolve for key + context
- `settings_set_value` -- set scoped value
- `settings_delete_value` -- delete scoped value
- `settings_explain` -- explain resolution chain
- `settings_search` -- search definitions
- `settings_audit` -- recent change history
- `settings_categories` -- list categories
- `settings_export` -- export as JSON

**Acceptance criteria:**
- [ ] MCP at `/mcp` with HTTP transport
- [ ] Tools reuse application service layer
- [ ] Auth/RBAC enforced on all tools
- [ ] CORS configured for MCP clients

### Story 5.2: MCP OAuth metadata

- `/.well-known/oauth-protected-resource`
- `/mcp/.well-known/oauth-protected-resource`
- Authorization server redirect endpoints

---

## Epic 6: CLI Tool

### Story 6.1: Auth commands

- `andy-settings auth login` -- OAuth Device Flow
- `andy-settings auth logout` -- clear stored token

### Story 6.2: Definition commands

- `andy-settings definitions list [--app <code>] [--category <cat>]`
- `andy-settings definitions search <query>`

### Story 6.3: Value commands

- `andy-settings get <key> [--scope <type>] [--scope-id <id>]`
- `andy-settings set <key> <value> [--scope <type>] [--type <dataType>]`
- `andy-settings delete <key> [--scope <type>]`
- `andy-settings explain <key> [--user <id>] [--team <id>]`

### Story 6.4: Export/import commands

- `andy-settings export [--app <code>] [--format json|yaml] [--output <file>]`
- `andy-settings import <file> [--preview] [--yes]`

### Story 6.5: Formatting

- `--format json|table` global option (default: table)
- `--api-url` global option (default: `https://localhost:5300`)
- Secrets masked in output
- Spectre.Console tables

---

## Epic 7: OpenTelemetry

### Story 7.1: Telemetry instrumentation

**Project:** `Andy.Settings.Infrastructure`

`SettingsTelemetry` class:

- ActivitySource: `Andy.Settings`
- Counters: `settings.definitions.created`, `settings.values.set`, `settings.values.deleted`, `settings.secrets.rotated`, `settings.resolutions`
- Histograms: `settings.resolution.duration`
- Instrument service methods with activities

### Story 7.2: Wire up in Program.cs

- OpenTelemetry services registration
- ASP.NET Core + EF Core + HTTP client instrumentation
- OTLP exporter from `OpenTelemetry:OtlpEndpoint` config
- Console exporter for development

---

## Epic 8: Frontend (Angular)

### Story 8.1: Core services and auth

- `AuthService` with angular-auth-oidc-client
- `ApiService` HTTP client
- `AuthInterceptor` for JWT injection
- Auth guard for protected routes

### Story 8.2: Layout and navigation

- Sidebar navigation (matching Andy Containers / Code Index style)
- Top bar with user info and logout
- Dashboard (definition counts, recent changes, scope overview)

### Story 8.3: Definitions browser

- List view with search, filter by app/category/tags
- Definition detail view with metadata
- Create/edit definition form (type-aware)
- Delete with confirmation

### Story 8.4: Values editor

- Scope-aware value editor
- Type-aware input components (string, integer, boolean, enum, JSON, etc.)
- Scope picker (machine, app, service, user, team, workspace)
- Visual scope chain showing inheritance

### Story 8.5: Effective value explorer

- Resolve effective values for a context
- Explanation view showing full source chain
- "Why is this value active?" visualization
- Side-by-side scope comparison

### Story 8.6: Secret management

- Secret editor with masked input
- RBAC-gated reveal toggle
- Rotation with confirmation
- Indicator for secret-type definitions

### Story 8.7: Audit timeline

- Recent changes timeline
- Per-definition history with before/after diff
- Filter by date range, actor, event type
- Pagination

### Story 8.8: Import/export views

- Export dialog with format, scope, app filters
- Import with file upload (drag-and-drop)
- Preview diff before applying
- Secret masking indicator

---

## Epic 9: Examples

### Story 9.1: C# (.NET) example

- REST API client using `HttpClient`
- List definitions, resolve effective value, set value

### Story 9.2: Python example

- REST API client using `requests`
- Same operations as C#

### Story 9.3: JavaScript/TypeScript example

- REST API client using `fetch`
- MCP client example

### Story 9.4: Go example

- REST API client using `net/http`

### Story 9.5: Rust example

- REST API client using `reqwest`

### Story 9.6: PowerShell example

- REST API client using `Invoke-RestMethod`

---

## Epic 10: Testing

### Story 10.1: Unit tests -- Domain and Application

- Resolution engine tests (all precedence cases)
- Definition validation tests
- Mutation rule tests
- Audit event creation tests
- Secret encryption tests

**Target:** 90%+ coverage on Domain, 85%+ on Application

### Story 10.2: Unit tests -- Controllers

- All 6 controllers tested
- Success and error paths
- Model validation
- Authorization requirements

### Story 10.3: Unit tests -- MCP tools and CLI

- MCP tool contracts
- CLI command parsing
- Output formatting

### Story 10.4: Integration tests

- `CustomWebApplicationFactory` with in-memory SQLite
- All CRUD endpoints end-to-end
- Auth/RBAC enforcement
- Secret encryption round-trip
- Import/export round-trip
- MCP endpoint

**Target:** 50+ integration tests

### Story 10.5: Frontend tests

- Component tests (Jasmine/Karma)
- Service tests
- Auth guard and interceptor tests

**Target:** 50+ frontend tests

---

## Epic 11: Documentation & Polish

### Story 11.1: MkDocs documentation site

- `mkdocs.yml` with Material theme
- `docs/index.md` landing page
- Navigation structure for all docs
- GitHub Actions deployment to GitHub Pages

### Story 11.2: API documentation

- Swagger XML comments on all controllers
- Request/response examples
- Authentication documented

### Story 11.3: Update README

- Architecture overview
- Quick start (docker compose up)
- Development setup
- CLI usage
- Port table

### Story 11.4: Conductor integration guide

- Document `SettingsServiceConfig` setup
- Seed definition loading
- Swift `SettingsService` protocol example
- ActionBus integration example

---

## Implementation Order

1. **Epic 1** (Domain) -- foundation, no dependencies
2. **Epic 2** (Database) -- depends on Epic 1
3. **Epic 3** (API) -- depends on Epic 2
4. **Epic 4** (Auth/RBAC) -- parallel with Epic 3
5. **Epic 5** (MCP) -- depends on Epic 3
6. **Epic 7** (Telemetry) -- parallel with Epic 5
7. **Epic 6** (CLI) -- depends on Epic 3
8. **Epic 8** (Frontend) -- depends on Epic 3
9. **Epic 9** (Examples) -- depends on Epic 3
10. **Epic 10** (Tests) -- progressive, start as soon as target layer exists
11. **Epic 11** (Docs) -- progressive, finalize last
