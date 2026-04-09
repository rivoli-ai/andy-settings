# Andy Settings - Implementation Stories

Stories organized by epic. Each story has acceptance criteria and references the corresponding layer/project. Stories should be implemented roughly in epic order, though some stories within an epic can be parallelized.

---

## Epic 1: Domain Model & Core Abstractions

### Story 1.1: Define domain entities

**Project:** `Andy.Settings.Domain`

Create the core domain entities for settings management:

- `Setting` - A configuration key-value pair with metadata (key, value, type, description, category, isSecret, createdAt, updatedAt, createdBy)
- `SettingCategory` - Logical grouping of settings (code, name, description, sortOrder)
- `Environment` - Target environment for settings (code, name, description, isDefault, sortOrder)
- `SettingOverride` - Environment-specific value override (settingId, environmentId, value, createdAt, updatedAt, createdBy)
- `SettingHistory` - Audit trail for all changes (settingId, environmentId, previousValue, newValue, changedBy, changedAt, changeReason)
- `Application` - Registered application that consumes settings (code, name, description, apiKey)
- `SettingSchema` - JSON schema validation rules per setting (settingId, jsonSchema, validationRules)

**Acceptance criteria:**
- [ ] All entities have proper ID fields (Guid)
- [ ] Timestamp fields use DateTimeOffset
- [ ] Navigation properties are defined
- [ ] Value types use appropriate enums (SettingValueType: String, Integer, Boolean, Json, Secret)

### Story 1.2: Define domain enums

**Project:** `Andy.Settings.Domain`

- `SettingValueType` - String, Integer, Boolean, Decimal, Json, Secret, ConnectionString, Url
- `ChangeAction` - Created, Updated, Deleted, Restored
- `SettingScope` - Global, Application, Environment, User

**Acceptance criteria:**
- [ ] Enums cover all needed states
- [ ] JSON serialization works correctly

### Story 1.3: Define application interfaces

**Project:** `Andy.Settings.Application`

Create service interfaces:

- `ISettingService` - CRUD operations for settings (Get, List, Create, Update, Delete, GetByCategory, GetByApplication)
- `IEnvironmentService` - Environment management (List, Create, Update, Delete)
- `ICategoryService` - Category management (List, Create, Update, Delete, Reorder)
- `ISettingOverrideService` - Override management (Get, Set, Delete, ListByEnvironment)
- `ISettingHistoryService` - Audit trail queries (GetHistory, GetHistoryByDateRange)
- `IApplicationService` - Application registration (Register, Unregister, GetSettings, RegenerateApiKey)
- `ISettingExportService` - Export/import settings (ExportJson, ExportYaml, ImportJson, ImportYaml)
- `ISettingValidationService` - Validate setting values against schemas

**Acceptance criteria:**
- [ ] All interfaces follow async patterns (Task/ValueTask)
- [ ] DTOs defined for input/output contracts
- [ ] Cancellation token parameters included

### Story 1.4: Define DTOs and options

**Project:** `Andy.Settings.Application`

- Request/Response DTOs for all API contracts
- `DatabaseOptions` with Provider property (PostgreSql/Sqlite)
- Configuration options classes for all appsettings sections

**Acceptance criteria:**
- [ ] DTOs are records where appropriate
- [ ] Validation attributes on request DTOs
- [ ] Options classes map to appsettings.json sections

---

## Epic 2: Database & Infrastructure

### Story 2.1: Create EF Core DbContext with dual provider support

**Project:** `Andy.Settings.Infrastructure`

Implement `SettingsDbContext` with:

- DbSets for all domain entities
- Fluent API configuration (indexes, relationships, constraints)
- PostgreSQL-specific config (jsonb columns)
- SQLite compatibility (TEXT for JSON columns)
- `DatabaseProviderExtensions` class for provider switching via config

**Acceptance criteria:**
- [ ] DbContext registers all entities
- [ ] Unique constraints on Setting.Key + Application scope
- [ ] Indexes on frequently queried fields (Key, Category, Environment)
- [ ] Provider switching works via `Database:Provider` config key
- [ ] SQLite default path: `~/.andy-settings/andy-settings.sqlite`

### Story 2.2: Create initial migration

**Project:** `Andy.Settings.Infrastructure`

- Generate `InitialCreate` migration
- Verify migration applies cleanly on both PostgreSQL and SQLite
- Include `DesignTimeDbContextFactory` for migration tooling

**Acceptance criteria:**
- [ ] `dotnet ef migrations add` works from Infrastructure project
- [ ] Migration applies on fresh PostgreSQL database
- [ ] Migration applies on fresh SQLite database

### Story 2.3: Implement repository layer

**Project:** `Andy.Settings.Infrastructure`

Implement repository classes:

- `SettingRepository`
- `EnvironmentRepository`
- `CategoryRepository`
- `SettingOverrideRepository`
- `SettingHistoryRepository`
- `ApplicationRepository`
- Base `RepositoryBase<T>` with common CRUD operations

**Acceptance criteria:**
- [ ] Repositories implement application interfaces
- [ ] Include queries (navigation properties) are efficient
- [ ] Pagination support on list operations
- [ ] Filtering and search support

### Story 2.4: Implement data seeder

**Project:** `Andy.Settings.Api`

Create `DataSeeder` to seed:

- Default environment (Development, Staging, Production)
- Default categories (General, Security, Database, Logging, Features, Integration)
- Sample settings for development

**Acceptance criteria:**
- [ ] Seeder is idempotent (safe to run multiple times)
- [ ] Only seeds if tables are empty
- [ ] Runs on startup in Development environment

---

## Epic 3: API Layer

### Story 3.1: Configure Program.cs with full middleware pipeline

**Project:** `Andy.Settings.Api`

Set up the ASP.NET Core pipeline matching Andy Containers/Code Index patterns:

- Controller registration
- Swagger/OpenAPI with JWT security definition
- CORS configuration from appsettings
- Static file serving (Angular SPA from wwwroot)
- SPA fallback routing
- Health check endpoint (`/health`)
- Auto-migration in development mode
- Serilog logging

**Acceptance criteria:**
- [ ] `dotnet run` starts the API successfully
- [ ] Swagger UI accessible at `/swagger` in development
- [ ] Health endpoint returns 200
- [ ] SPA fallback serves index.html for non-API routes
- [ ] CORS allows configured origins

### Story 3.2: Implement SettingsController

**Project:** `Andy.Settings.Api`

REST endpoints:

- `GET /api/settings` - List all settings (with filtering, pagination)
- `GET /api/settings/{id}` - Get setting by ID
- `GET /api/settings/key/{key}` - Get setting by key
- `POST /api/settings` - Create setting
- `PUT /api/settings/{id}` - Update setting
- `DELETE /api/settings/{id}` - Delete setting (soft delete)
- `GET /api/settings/category/{categoryCode}` - List by category
- `GET /api/settings/search?q={query}` - Search settings

**Acceptance criteria:**
- [ ] All endpoints return proper HTTP status codes
- [ ] Request validation with model binding
- [ ] `[ProducesResponseType]` attributes for Swagger documentation
- [ ] RBAC attributes (`[RequirePermission("setting:read")]`, etc.)

### Story 3.3: Implement EnvironmentsController

**Project:** `Andy.Settings.Api`

- `GET /api/environments` - List environments
- `GET /api/environments/{id}` - Get environment
- `POST /api/environments` - Create environment
- `PUT /api/environments/{id}` - Update environment
- `DELETE /api/environments/{id}` - Delete environment
- `GET /api/environments/{id}/settings` - Get resolved settings for environment

**Acceptance criteria:**
- [ ] Cannot delete default environment
- [ ] Resolved settings merge base + overrides
- [ ] RBAC permission checks

### Story 3.4: Implement CategoriesController

**Project:** `Andy.Settings.Api`

- `GET /api/categories` - List categories
- `POST /api/categories` - Create category
- `PUT /api/categories/{id}` - Update category
- `DELETE /api/categories/{id}` - Delete category
- `PUT /api/categories/reorder` - Reorder categories

**Acceptance criteria:**
- [ ] Category with settings cannot be deleted (or settings must be reassigned)
- [ ] Reorder updates sortOrder for all categories in batch

### Story 3.5: Implement OverridesController

**Project:** `Andy.Settings.Api`

- `GET /api/overrides?environmentId={envId}` - List overrides for environment
- `PUT /api/overrides` - Set override (upsert)
- `DELETE /api/overrides/{id}` - Remove override
- `POST /api/overrides/bulk` - Bulk set overrides

**Acceptance criteria:**
- [ ] Setting history recorded on every override change
- [ ] Validate value against setting schema if defined

### Story 3.6: Implement HistoryController

**Project:** `Andy.Settings.Api`

- `GET /api/history?settingId={id}` - Get history for a setting
- `GET /api/history/recent` - Recent changes across all settings
- `GET /api/history/diff?from={date}&to={date}` - Changes in date range

**Acceptance criteria:**
- [ ] History is read-only
- [ ] Supports pagination
- [ ] Includes who made the change (from JWT claims)

### Story 3.7: Implement ApplicationsController

**Project:** `Andy.Settings.Api`

- `GET /api/applications` - List registered applications
- `POST /api/applications` - Register application
- `DELETE /api/applications/{id}` - Unregister
- `GET /api/applications/{id}/settings` - Get settings for application
- `POST /api/applications/{id}/api-key/regenerate` - Regenerate API key

**Acceptance criteria:**
- [ ] API key is hashed before storage
- [ ] Application can retrieve its settings via API key auth (alternative to JWT)

### Story 3.8: Implement ExportImportController

**Project:** `Andy.Settings.Api`

- `GET /api/export?format=json&environmentId={id}` - Export settings
- `GET /api/export?format=yaml&environmentId={id}` - Export as YAML
- `POST /api/import` - Import settings from JSON/YAML
- `POST /api/import/preview` - Preview import without applying

**Acceptance criteria:**
- [ ] Export includes metadata (timestamp, environment, version)
- [ ] Import validates all settings before applying
- [ ] Preview shows what would change (added, modified, removed)
- [ ] Secrets are masked in export unless explicitly requested

---

## Epic 4: Authentication & Authorization

### Story 4.1: Integrate Andy Auth (JWT Bearer)

**Project:** `Andy.Settings.Api`

Configure JWT Bearer authentication matching the Andy Containers/Code Index pattern:

- JWT Bearer with Andy Auth authority
- Token validation with configurable issuers
- Development fallback (auto-assign dev user with admin claims)
- SSL validation bypass for development
- `CurrentUserService` to extract user info from JWT claims

**Acceptance criteria:**
- [ ] Protected endpoints return 401 without valid token
- [ ] Development mode allows unauthenticated access with dev user
- [ ] User info (sub, name, email) extracted from claims
- [ ] MCP OAuth protected resource metadata endpoints

### Story 4.2: Integrate Andy RBAC

**Project:** `Andy.Settings.Api`

Configure RBAC with the following permissions:

- `setting:read` - View settings
- `setting:write` - Create/update settings
- `setting:delete` - Delete settings
- `environment:read` - View environments
- `environment:write` - Manage environments
- `category:read` - View categories
- `category:write` - Manage categories
- `application:read` - View registered applications
- `application:write` - Manage applications
- `history:read` - View audit history
- `export:read` - Export settings
- `import:write` - Import settings

**Acceptance criteria:**
- [ ] RBAC client configured with application code "settings"
- [ ] `[RequirePermission]` attributes on all controller actions
- [ ] Development mode bypasses RBAC with `AllowAllDevPolicyProvider`
- [ ] 403 returned when user lacks required permission

---

## Epic 5: MCP Integration

### Story 5.1: Implement MCP tools

**Project:** `Andy.Settings.Api`

Create `SettingsMcpTools` class with `[McpServerToolType]`:

- `settings_list` - List settings with optional category/environment filter
- `settings_get` - Get a specific setting by key
- `settings_set` - Create or update a setting
- `settings_delete` - Delete a setting
- `settings_search` - Search settings by keyword
- `settings_environments` - List available environments
- `settings_resolve` - Get resolved settings for an environment
- `settings_history` - Get recent change history
- `settings_categories` - List categories
- `settings_export` - Export settings as JSON

**Acceptance criteria:**
- [ ] MCP server registered with HTTP transport at `/mcp`
- [ ] All tools have proper `[McpServerTool]` attributes with Name and Description
- [ ] Tools reuse the same service layer as REST controllers
- [ ] MCP endpoint protected by auth (RequireAuthorization)
- [ ] CORS configured for MCP clients

### Story 5.2: Configure MCP OAuth metadata

**Project:** `Andy.Settings.Api`

- Well-known endpoints (`/.well-known/oauth-protected-resource`, `/mcp/.well-known/oauth-protected-resource`)
- Authorization server redirect endpoints
- Token endpoint proxy

**Acceptance criteria:**
- [ ] OAuth metadata discoverable by MCP clients
- [ ] Andy Auth authority correctly referenced
- [ ] Supports standard MCP authentication flow

---

## Epic 6: CLI Tool

### Story 6.1: Implement auth commands

**Project:** `Andy.Settings.Cli`

- `andy-settings auth login` - OAuth Device Flow login
- `andy-settings auth logout` - Clear stored token

**Acceptance criteria:**
- [ ] Device flow works with Andy Auth
- [ ] Token stored securely
- [ ] Subsequent commands use stored token

### Story 6.2: Implement settings commands

**Project:** `Andy.Settings.Cli`

- `andy-settings list [--category <cat>] [--environment <env>]` - List settings
- `andy-settings get <key> [--environment <env>]` - Get setting value
- `andy-settings set <key> <value> [--type <type>] [--category <cat>]` - Set setting
- `andy-settings delete <key>` - Delete setting
- `andy-settings search <query>` - Search settings
- `andy-settings history <key>` - Show change history

**Acceptance criteria:**
- [ ] Table output via Spectre.Console
- [ ] `--format json` option for machine-readable output
- [ ] Secrets are masked in output
- [ ] `--api-url` global option (default: https://localhost:5300)

### Story 6.3: Implement environment commands

**Project:** `Andy.Settings.Cli`

- `andy-settings env list` - List environments
- `andy-settings env create <code> <name>` - Create environment
- `andy-settings env delete <code>` - Delete environment
- `andy-settings env resolve <code>` - Show resolved settings

**Acceptance criteria:**
- [ ] Clear table output
- [ ] Resolved view shows base vs override values

### Story 6.4: Implement export/import commands

**Project:** `Andy.Settings.Cli`

- `andy-settings export [--environment <env>] [--format json|yaml] [--output <file>]`
- `andy-settings import <file> [--preview] [--environment <env>]`

**Acceptance criteria:**
- [ ] Export to stdout or file
- [ ] Preview mode shows diff before applying
- [ ] Import requires confirmation (unless `--yes` flag)

---

## Epic 7: OpenTelemetry

### Story 7.1: Configure OpenTelemetry tracing and metrics

**Project:** `Andy.Settings.Infrastructure`

Create `SettingsTelemetry` class:

- ActivitySource: "Andy.Settings"
- Custom counters: settings.created, settings.updated, settings.deleted, settings.read
- Custom histograms: settings.resolve.duration
- Instrument service methods with activities

**Acceptance criteria:**
- [ ] Traces visible in OTLP collector when endpoint configured
- [ ] Console exporter for development
- [ ] ASP.NET Core instrumentation (requests)
- [ ] EF Core instrumentation (queries)
- [ ] HTTP client instrumentation (outbound calls)

### Story 7.2: Wire up telemetry in Program.cs

**Project:** `Andy.Settings.Api`

- Register OpenTelemetry services
- Configure OTLP exporter from settings
- Add ASP.NET Core + EF Core + HTTP client instrumentation

**Acceptance criteria:**
- [ ] `OpenTelemetry:OtlpEndpoint` config key controls exporter
- [ ] `OpenTelemetry:ServiceName` config key sets service name
- [ ] Telemetry works without OTLP endpoint (console only)

---

## Epic 8: Frontend (Angular)

### Story 8.1: Set up core services and auth

**Project:** `client/`

- `AuthService` - OIDC authentication with angular-auth-oidc-client
- `ApiService` - HTTP client for the settings API
- `AuthInterceptor` - JWT token injection
- Auth guard for protected routes

**Acceptance criteria:**
- [ ] Login redirects to Andy Auth
- [ ] Token refresh works
- [ ] API calls include Authorization header
- [ ] Unauthenticated users redirected to login

### Story 8.2: Create layout and navigation

**Project:** `client/`

- Sidebar navigation (matching Andy Containers/Code Index style)
- Top bar with user info and logout
- Dashboard page (setting counts, recent changes, environment overview)

**Acceptance criteria:**
- [ ] Responsive layout
- [ ] Active route highlighted in sidebar
- [ ] Same look and feel as Andy Containers / Code Index

### Story 8.3: Settings list and detail views

**Project:** `client/`

- Settings list with table view (sortable, filterable, paginated)
- Category filter sidebar/dropdown
- Environment selector
- Setting detail/edit view
- Create setting dialog/page
- Secret values masked with reveal toggle

**Acceptance criteria:**
- [ ] Search/filter by key, category, type
- [ ] Inline editing for simple values
- [ ] JSON editor for JSON-type settings
- [ ] History tab on setting detail view

### Story 8.4: Environment management views

**Project:** `client/`

- Environment list
- Create/edit environment
- Resolved settings view per environment (shows base + overrides)
- Override editor (set/remove overrides per environment)

**Acceptance criteria:**
- [ ] Visual diff between base and override values
- [ ] Bulk override editing
- [ ] Environment comparison view (side-by-side)

### Story 8.5: Category management

**Project:** `client/`

- Category list with drag-and-drop reorder
- Create/edit category

**Acceptance criteria:**
- [ ] Reorder persists to API
- [ ] Settings count shown per category

### Story 8.6: History and audit views

**Project:** `client/`

- Recent changes timeline
- Per-setting history with diff view
- Filter by date range, user, action type

**Acceptance criteria:**
- [ ] Shows who changed what and when
- [ ] Previous/new value comparison
- [ ] Pagination for long histories

### Story 8.7: Export/Import views

**Project:** `client/`

- Export dialog with format and environment selection
- Import page with file upload
- Preview/diff before applying import

**Acceptance criteria:**
- [ ] Download export as file
- [ ] Drag-and-drop file upload for import
- [ ] Preview shows additions, modifications, deletions

### Story 8.8: Application management views

**Project:** `client/`

- Registered applications list
- Register new application
- API key display (show once, then masked)
- Per-application settings view

**Acceptance criteria:**
- [ ] API key shown only once on creation
- [ ] Regenerate key with confirmation dialog

---

## Epic 9: Testing

### Story 9.1: Unit tests - Domain and Application layers

**Project:** `Andy.Settings.Tests.Unit`

- Entity validation tests
- Service logic tests (with mocked repositories)
- DTO mapping tests
- Validation service tests

**Acceptance criteria:**
- [ ] 90%+ coverage on Domain layer
- [ ] 85%+ coverage on Application layer
- [ ] Tests use FluentAssertions
- [ ] Tests use Moq for mocking

### Story 9.2: Unit tests - API controllers

**Project:** `Andy.Settings.Tests.Unit`

Test each controller:

- SettingsController tests
- EnvironmentsController tests
- CategoriesController tests
- OverridesController tests
- HistoryController tests
- ApplicationsController tests
- ExportImportController tests

**Acceptance criteria:**
- [ ] Test success and error paths
- [ ] Test model validation
- [ ] Test authorization requirements
- [ ] Use in-memory database or mocked services

### Story 9.3: Unit tests - MCP tools

**Project:** `Andy.Settings.Tests.Unit`

- Test each MCP tool method
- Test error handling
- Test parameter validation

**Acceptance criteria:**
- [ ] All MCP tools have corresponding test methods
- [ ] Error scenarios covered

### Story 9.4: Unit tests - CLI commands

**Project:** `Andy.Settings.Tests.Unit`

- Test command parsing
- Test output formatting
- Test API client integration (with mocked HTTP)

**Acceptance criteria:**
- [ ] Command options parsed correctly
- [ ] Error messages are user-friendly

### Story 9.5: Integration tests - API endpoints

**Project:** `Andy.Settings.Tests.Integration`

Using `WebApplicationFactory`:

- `CustomWebApplicationFactory` with in-memory SQLite
- Test all CRUD operations end-to-end
- Test authentication flows
- Test MCP endpoint
- Test export/import round-trip

**Acceptance criteria:**
- [ ] All endpoints tested with real HTTP requests
- [ ] Auth bypass for test environment
- [ ] Database seeded for tests
- [ ] 50+ integration tests

### Story 9.6: Frontend tests

**Project:** `client/`

- Component tests (Jasmine/Karma)
- Service tests
- Guard tests
- Interceptor tests

**Acceptance criteria:**
- [ ] All components have spec files
- [ ] Services tested with HttpClientTestingModule
- [ ] 50+ frontend tests

---

## Epic 10: Documentation & Polish

### Story 10.1: API documentation

Generate OpenAPI spec and add XML comments to controllers.

**Acceptance criteria:**
- [ ] Swagger UI shows all endpoints with descriptions
- [ ] Request/response examples
- [ ] Authentication documented in Swagger

### Story 10.2: README and getting started

Update README.md with:

- Project description
- Architecture overview
- Prerequisites
- Quick start (docker-compose up)
- Development setup
- CLI usage

**Acceptance criteria:**
- [ ] Someone can clone and run with `docker compose up`
- [ ] Development workflow documented

### Story 10.3: Architecture documentation

Create `docs/ARCHITECTURE.md`:

- System context diagram
- Layer architecture
- Domain model
- Database schema
- API reference
- Security model

**Acceptance criteria:**
- [ ] Comprehensive but concise
- [ ] Includes diagrams (mermaid)

---

## Implementation Order (Suggested)

1. **Epic 1** (Domain) - Foundation, no dependencies
2. **Epic 2** (Database) - Depends on Epic 1
3. **Epic 4** (Auth) - Can parallel with Epic 2
4. **Epic 3** (API) - Depends on Epic 2 + 4
5. **Epic 5** (MCP) - Depends on Epic 3
6. **Epic 7** (Telemetry) - Can parallel with Epic 5
7. **Epic 6** (CLI) - Depends on Epic 3
8. **Epic 8** (Frontend) - Depends on Epic 3
9. **Epic 9** (Tests) - Progressive, stories can start as soon as their target layer exists
10. **Epic 10** (Docs) - Final polish
