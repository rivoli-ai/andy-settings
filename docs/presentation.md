---
marp: true
theme: default
paginate: true
size: 16:9
header: 'Andy Settings — End-to-End Walkthrough'
footer: 'Rivoli AI · andy-settings'
style: |
  section { font-size: 24px; }
  section h1 { color: #1f4e79; }
  section h2 { color: #2e75b6; border-bottom: 2px solid #2e75b6; padding-bottom: 4px; }
  code { background: #f4f4f4; padding: 2px 4px; border-radius: 3px; }
  pre { font-size: 18px; }
  table { font-size: 20px; }
---

<!-- _class: lead -->
<!-- _paginate: false -->

# Andy Settings
## End-to-End System Walkthrough

Centralized, typed, scoped configuration + secret management for every Andy service.

*Designed for engineers who have never seen this service before.*

---

## What is Andy Settings?

A **centralized configuration service**. Every other Andy service defines its tunables as **typed, documented settings**, and users can assign values at different **scopes** (Machine → Application → Service → User → Team → Workspace → RuntimeOverride).

- Typed schemas + JSON-schema validation
- **Scope-precedence resolution** with source-chain explanation
- **Encrypted secrets** (Data Protection API, AES-256-GCM)
- **Append-only audit trail** of every change
- Export / import for environment promotion
- MCP tools for AI-driven configuration

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 8.0 |
| API | REST + MCP |
| Frontend | Angular 18 (standalone) |
| Database | PostgreSQL (default) / SQLite (embedded) |
| ORM | Entity Framework Core 8 |
| Auth | OAuth2/OIDC via Andy Auth |
| Authorization | Andy RBAC |
| Secrets | ASP.NET Core Data Protection |
| Testing | xUnit + Moq + FluentAssertions |

---

## Solution Layout

```
andy-settings/
├── src/
│   ├── Andy.Settings.Domain/         ← entities, enums
│   ├── Andy.Settings.Application/    ← interfaces, DTOs
│   ├── Andy.Settings.Infrastructure/ ← EF, services, repos
│   ├── Andy.Settings.Api/            ← REST + MCP + Swagger
│   └── Andy.Settings.Shared/         ← shared types
├── tools/Andy.Settings.Cli/          ← dotnet tool
├── client/                           ← Angular 18 SPA
└── tests/
    ├── Andy.Settings.Tests.Unit/
    └── Andy.Settings.Tests.Integration/
```

---

## Domain Model — Definitions

**`SettingDefinition`** — the *schema* of one setting:

- `Key` — e.g. `andy.containers.defaultProvider`
- `ApplicationCode` — owner (`containers`, `issues`, …)
- `DisplayName`, `Description`, `Category`
- `DataType` (enum)
- `DefaultValueJson` — fallback when nothing is assigned
- `ValidationJson` — JSON schema
- `UiSchemaJson` — form hints
- `IsSecret` (bool)
- `AllowedScopesJson`, `TagsJson`, `IsDeprecated`

**DataType**: String · Integer · Boolean · Decimal · Enum · Duration · Uri · Json · StringList · Secret.

---

## Domain Model — Scopes

**`ScopeType`** (precedence low → high):

```
Machine (0) < Application (1) < Service (2) < User (3)
< Team (4) < Workspace (5) < RuntimeOverride (6)
```

Resolution picks the **highest-precedence** assignment that matches the current `ResolutionContext`.

Everything else is traceable via the **source chain** returned by `Explain`.

---

## Domain Model — Assignments, Secrets, Audit

**`SettingAssignment`**:

- FK → DefinitionId, `ScopeType`, `ScopeId?` (null for Machine)
- `ValueJson`
- `Etag` (optimistic concurrency — 409 on mismatch)
- `Version` (monotonic)
- `UpdatedBy`, timestamps

**`EncryptedSecret`** — same scope shape; `EncryptedValue` is `IDataProtector.Protect()` output, never plaintext on the wire.

**`AuditEvent`** — append-only: `EventType` (Created/Updated/Deleted/SecretRotated/Imported/Exported/AccessDenied), `BeforeJson`/`AfterJson` (secrets never logged), `CorrelationId`.

---

## Application Layer — Service Contracts

- **`IDefinitionService`** — `SearchAsync`, `GetAsync`, CRUD
- **`IAssignmentService`** — `SetAsync`, `DeleteAsync`, `ListByScopeAsync`, `BulkSetAsync`
- **`IResolutionService`** — `ResolveAsync(key, ctx)` + **`ExplainAsync(key, ctx)`** (with source chain) + `ResolveBatchAsync`
- **`ISecretService`** — set/get/rotate/delete with encryption
- **`IAuditService`** — append + query
- **`IValidationService`** — JSON-schema validation
- **`IExportImportService`** — bulk promotion between environments

All DTOs are `record` types.

---

## Infrastructure — DbContext & Repositories

**`SettingsDbContext`** — 4 DbSets:

- `SettingDefinitions`
- `SettingAssignments`
- `EncryptedSecrets`
- `AuditEvents`

Indexes on `(Key, ApplicationCode)`, `(DefinitionId, ScopeType, ScopeId)`, enum→string conversions.

Migration: `20260410031443_InitialCreate`.

**`AssignmentRepository.SetAsync`** — upsert with Etag check + auto-audit on every write, single transaction with `SaveChangesAsync`.

---

## Resolution — The Hot Path

**`ResolutionService.ResolveAsync`**:

1. Build scope candidates from `ResolutionContext`
2. Query all matching assignments for the definition
3. Walk lowest → highest precedence
4. Return the winner (+ optional explain chain)

**`ResolvedSetting`**:

- `EffectiveValue`
- `WinningScopeType` / `WinningScopeId`
- `SourceChain[]` with `IsWinner` flags
- `IsDefault`, `IsValid`

---

## Secrets — Storage & Access

**`SecretService`**:

- `SetSecretAsync` → `IDataProtector.Protect(plaintext)` → store `EncryptedSecret`
- `GetSecretAsync` → RBAC check → `Unprotect` → plaintext
- `RotateSecretAsync` → new encrypted value + audit event
- `DeleteSecretAsync` → cascade delete

Plaintext values **never** appear in list endpoints, exports, or audit logs. Only in the direct `GET` of a single secret, with permission.

---

## API Layer — REST Controllers

| Controller | Purpose |
|-----------|---------|
| `DefinitionsController` | CRUD schemas |
| `ValuesController` | CRUD assignments, bulk set |
| `EffectiveController` | Resolve + **Explain** |
| `SecretsController` | Encrypted value ops |
| `AuditController` | Query trail |
| `ImportExportController` | JSON bulk I/O |

All routes under `/api` and `[Authorize]` with RBAC checks.

---

## API Layer — MCP Tools

`Mcp/SettingsMcpTools.cs` — around 20 tools at `/mcp`:

- `settings_list_definitions`, `settings_search`
- `settings_get_effective`, `settings_explain`
- `settings_set_value`, `settings_delete_value`
- `settings_set_secret`, `settings_get_secret`, `settings_rotate_secret`
- `settings_audit`
- `settings_export`, `settings_import`, `settings_import_preview`

AI agents can browse + tune configuration without a human operator.

---

## How Other Services Consume It

**Three patterns in practice:**

1. **Direct REST** — `GET /api/effective?key=…` with a `ResolutionContext`
2. **MCP** — AI assistants call `settings_get_effective` etc.
3. **CLI** — operators use `andy-settings` (System.CommandLine + Spectre.Console)

Some services (e.g. andy-agents) run a **`SettingsRefreshService`** background worker that polls tracked keys every 30s and updates an in-memory snapshot.

No dedicated SDK yet — `Andy.Settings.Shared` is the shared contract.

---

## CLI

```bash
andy-settings auth login                       # OAuth device flow
andy-settings definitions list --app containers
andy-settings get andy.containers.defaultProvider --scope team --scope-id t1
andy-settings explain andy.containers.defaultProvider --format json
andy-settings set andy.containers.defaultProvider docker-compose --scope team
andy-settings secrets set andy.openai.apiKey
andy-settings audit --key andy.containers.defaultProvider --since 2026-04-01
andy-settings export --app containers > containers.json
andy-settings import containers.json --preview
```

Output as `table` or `json` via `--format`.

---

## Angular Admin UI

**Routes** (`app.routes.ts`):

- `/dashboard`
- `/definitions` — browse all schemas
- `/values` — ValueEditor (scope picker)
- `/effective` — EffectiveExplorer (shows source chain)
- `/secrets` — SecretManager (metadata only, no plaintext in list)
- `/audit` — timeline view
- `/import-export`

OIDC via auth interceptor. All writes optimistic-locked via Etag.

---

## Data Flow — Admin Updates a Setting

1. User edits value in Angular `ValueEditorComponent`
2. **`POST /api/values`** with `SetValueDto { definitionKey, scopeType, scopeId, valueJson }`
3. Controller: validate → extract actorId from JWT
4. **`AssignmentRepository.SetAsync`**:
   - Upsert: new row or update existing
   - Etag increments, Version++
5. **`AuditService.RecordAsync`** appends an `AuditEvent` in the same transaction
6. `SaveChangesAsync` → atomic commit
7. Returns `AssignmentDto { id, etag, version, updatedBy, … }`

Consumers discover the change on next `/api/effective` call or next refresh tick.

---

## Subscriber Model (Today)

- **Polling model** is the current design — services periodically call `/api/values` or `/api/effective` (e.g. every 30s via `SettingsRefreshService` in andy-agents).
- The **audit endpoint** (`/api/audit?definitionKey=…`) lets UIs show recent changes and lets ops scripts detect drift.
- **No SignalR / WebSocket push** yet — a future addition could turn the audit stream into a live feed.

Tradeoff: simplicity & resilience (stateless reads) vs. freshness.

---

## Configuration & Ports

| Port | Purpose |
|------|---------|
| 5300 | API HTTPS (native `dotnet run`) |
| 5301 | API HTTP (native `dotnet run`) |
| 7300 | API HTTPS (docker compose) |
| 7301 | API HTTP (docker compose) |
| 7438 | PostgreSQL (docker compose) |
| 4200 | Angular dev |
| 9111 | Conductor embedded (settings) |

```json
"ConnectionStrings": { "DefaultConnection": "Host=localhost;Port=7438;Database=andy_settings;…" },
"Database": { "Provider": "PostgreSql" },
"AndyAuth": { "Authority": "https://localhost:5001", "Audience": "urn:andy-settings-api" },
"Rbac": { "ApiBaseUrl": "https://localhost:5003", "ApplicationCode": "settings" }
```

---

## Docker

Multi-stage Dockerfile:

1. Node — build Angular
2. .NET 8 SDK — `dotnet publish`
3. ASP.NET runtime — run

`docker-compose.yml` — PostgreSQL 16 + API. Healthchecks on `/health` and `pg_isready`.

Embedded/Conductor mode: `Database__Provider=Sqlite` + connection-string override. Angular bundle served by the same host.

---

## Testing

**Unit tests** (`Tests.Unit`):

- `ValuesControllerTests`, `EffectiveControllerTests`, `SecretsControllerTests`
- `SettingsMcpToolsTests`
- Pattern: Moq for services, FluentAssertions, AAA structure

**Integration tests** (`Tests.Integration`):

- `WebApplicationFactory` with TestServer
- Real DB operations + RBAC policies

Goal: lock the resolution precedence semantics — they are the product.

---

<!-- _class: lead -->

# Where to start reading

1. `src/Andy.Settings.Domain/Entities/SettingDefinition.cs` — the schema
2. `src/Andy.Settings.Domain/Entities/SettingAssignment.cs` — the scope model
3. `src/Andy.Settings.Infrastructure/Services/ResolutionService.cs` — the winner
4. `src/Andy.Settings.Infrastructure/Repositories/AssignmentRepository.cs` — upsert + audit
5. `src/Andy.Settings.Api/Controllers/EffectiveController.cs` — the read path
6. `src/Andy.Settings.Api/Mcp/SettingsMcpTools.cs` — agent surface

Swagger at `/swagger` · MCP at `/mcp`.
