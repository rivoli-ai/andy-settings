# Architecture

## Overview

Andy Settings is the configuration control plane for the Andy ecosystem. It provides typed setting definitions, scoped assignments, effective-value resolution, encrypted secrets, audit history, and access through Web UI, REST, MCP, and CLI.

The architecture supports two deployment modes:

1. **Embedded Mode** -- runs inside the Conductor macOS app as the 8th embedded .NET service (port 9107, prefix `/settings`, SQLite backend)
2. **Shared Service Mode** -- hosted API, Angular client, PostgreSQL, team/user scopes, service-to-service consumption

## Architectural Principles

- Embedded-first, shared-second.
- Typed configuration, not just stringly typed key/value.
- Schema and validation live with definitions.
- Secrets are encrypted and RBAC-gated.
- A single domain model is exposed consistently over all interfaces.
- Scope resolution is deterministic and explainable.
- Same repo contains API, CLI, MCP server, web UI, and examples.

## Context Diagram

```text
+---------------------------------------------------------------+
|                     Andy Settings Repo                         |
|                                                                |
|  +------------------+   +------------------+   +------------+  |
|  | ASP.NET Core API |   | Angular Web App  |   | CLI        |  |
|  | REST + Swagger   |   | OIDC + RBAC      |   | dotnet tool|  |
|  +---------+--------+   +---------+--------+   +------+-----+  |
|            |                      |                    |        |
|            +----------+-----------+--------------------+        |
|                       |                                         |
|            +----------v-----------+                             |
|            | Application Layer    |                             |
|            | resolver / validator |                             |
|            | audit / secrets      |                             |
|            +----------+-----------+                             |
|                       |                                         |
|        +--------------+---------------+                         |
|        |                              |                         |
|  +-----v-----------------+    +-------v--------------+          |
|  | Infrastructure Layer  |    | MCP Tools            |          |
|  | SQLite / PostgreSQL   |    | Model Context Protocol|         |
|  +-----------------------+    +----------------------+          |
+---------------------------------------------------------------+

External Integrations:
- Conductor (macOS host app, Swift, embeds andy-settings as service)
- Andy Auth (OIDC / OAuth2)
- Andy RBAC (authorization)
- Andy Containers / DevPilot / Code Index / Docs (consumers)
- MCP clients / AI assistants
```

## Solution Structure

```text
andy-settings/
├── src/
│   ├── Andy.Settings.Domain/             # Entities, enums (no dependencies)
│   ├── Andy.Settings.Application/        # Interfaces, DTOs, options (→ Domain)
│   ├── Andy.Settings.Infrastructure/     # EF Core, repositories, services (→ Domain, Application)
│   ├── Andy.Settings.Api/                # ASP.NET Core REST + MCP + Swagger (→ all)
│   ├── Andy.Settings.Client/             # Consumer HTTP client + OBO/M2M token providers
│   ├── Andy.Settings.Client.TestSupport/ # In-memory fakes for client consumers
│   └── Andy.Settings.Shared/             # Shared types for client libraries
├── tools/
│   └── Andy.Settings.Cli/                # CLI tool
├── tests/
│   ├── Andy.Settings.Tests.Unit/         # Unit tests
│   ├── Andy.Settings.Tests.Integration/  # Integration tests
│   ├── Andy.Settings.Api.Tests/          # API tests
│   └── Andy.Settings.Client.Tests/       # Client library tests
├── client/                            # Angular SPA
├── examples/                          # Multi-language API/MCP examples
├── docs/                              # MkDocs documentation
├── certs/                             # Corporate CA certificates
├── Dockerfile                         # Multi-stage build
└── docker-compose.yml                 # PostgreSQL + API
```

## Domain Model

### Setting Definition

A definition describes what a setting is. Definitions are the schema layer.

Fields:

- key (unique, dot-separated, e.g. `andy.containers.defaultProvider`)
- application code (which Andy service owns this definition)
- display name
- description
- category
- data type (string, integer, boolean, decimal, enum, duration, uri, json, stringList, secret)
- allowed scopes
- default value
- validation rules (JSON schema)
- UI metadata for form generation
- is secret flag
- tags
- deprecation metadata

### Setting Assignment

An assignment stores a value at a specific scope.

Fields:

- definition ID
- scope type (machine, application, service, user, team, workspace, runtimeOverride)
- scope ID
- value payload (JSON)
- version / etag (optimistic concurrency)
- actor metadata
- timestamps

### Encrypted Secret

Secret-bearing settings store encrypted values that are only accessible to authorized users.

Fields:

- definition ID
- scope type / scope ID
- encrypted value (AES-256-GCM via ASP.NET Core Data Protection)
- created at / rotated at

Secrets are:

- Encrypted at rest using ASP.NET Core Data Protection API
- Only decrypted when the requesting user has the `secret:read` RBAC permission
- Never returned in plain text in list/export operations unless explicitly requested with `secret:read`
- Rotation creates a new encrypted value and emits an audit event (without logging the payload)

### Audit Event

Append-only record of configuration operations.

Fields:

- event type (created, updated, deleted, secretRotated, imported, exported)
- actor (from JWT claims)
- definition key
- scope
- before/after metadata (secrets excluded)
- correlation ID / trace ID
- timestamp

## Scope Resolution Engine

The resolution engine computes effective values for a given context.

Scope precedence (highest wins):

1. runtime override
2. workspace
3. team
4. user
5. service
6. application
7. machine
8. built-in default (from definition)

Each resolution result includes:

- effective value
- winning scope
- full source chain (all assignments considered)
- validation result
- explanation metadata

This powers the "why is this value active?" UX in the API, CLI, MCP, and web UI.

## Conductor Integration

Andy Settings runs as the 8th embedded .NET service inside the Conductor macOS app.

| Property | Value |
|----------|-------|
| Port | 9107 |
| Proxy prefix | `/settings` |
| Database | SQLite at `~/Library/Application Support/ai.rivoli.conductor/db/andy-settings.sqlite` |
| Service config | `SettingsServiceConfig` in `Conductor/Core/ServiceHost/Services/` |

Integration points in Conductor:

1. **ServiceOrchestrator** launches andy-settings after auth + rbac (same as other services)
2. **UnifiedProxy** routes `/settings/*` requests to port 9107
3. **SettingsService** protocol in Swift calls the API via `APIClient`
4. **ActionBus** integration: `UpdateSettingsAction` for audit trail
5. **AppPreferences** (UserDefaults) remains for UI chrome; andy-settings handles all service/team/user configuration

Consumer services (containers, code-index, devpilot, docs) read their settings from andy-settings at startup and optionally subscribe for live updates.

## Storage Model

### Tables

#### `setting_definitions`

- `id`, `key`, `application_code`, `display_name`, `description`, `category`
- `data_type`, `default_value_json`, `validation_json`, `ui_schema_json`
- `is_secret`, `allowed_scopes_json`, `tags_json`, `is_deprecated`
- `created_at`, `updated_at`

#### `setting_assignments`

- `id`, `definition_id`, `scope_type`, `scope_id`
- `value_json`, `etag`, `version`
- `updated_by`, `updated_at`

#### `encrypted_secrets`

- `id`, `definition_id`, `scope_type`, `scope_id`
- `encrypted_value` (Data Protection API)
- `updated_by`, `updated_at`

#### `audit_events`

- `id`, `event_type`, `definition_key`, `scope_type`, `scope_id`
- `actor_type`, `actor_id`
- `before_json`, `after_json` (secrets excluded)
- `correlation_id`, `created_at`

## API Architecture

### REST Endpoints

- `/api/definitions` -- setting definition CRUD
- `/api/values` -- scoped assignment CRUD
- `/api/effective` -- resolve effective values for a context
- `/api/secrets` -- encrypted secret set/rotate/delete
- `/api/audit` -- change history queries
- `/api/import` -- bulk import with preview
- `/api/export` -- bulk export with scope/app filters
- `/api/health` -- health probes

### MCP Tools

MCP tools map directly to the domain model:

- `settings_list_definitions` -- browse definitions by app/category
- `settings_get_effective` -- resolve effective value for a key + context
- `settings_set_value` -- set scoped value (with auth)
- `settings_explain` -- explain why a value is active
- `settings_audit` -- recent change history
- `settings_search` -- search definitions by keyword

### Swagger / OpenAPI

Swagger UI at `/swagger` (development only). Full OpenAPI spec with JWT security definition.

## Authentication and Authorization

### Authentication

- Web UI: OIDC with Andy Auth
- CLI: OAuth Device Flow
- API: JWT Bearer tokens
- Conductor: tokens forwarded through UnifiedProxy
- Embedded mode: local bootstrap for first-run (localhost-only, auto-disabled after setup)

### Authorization

Andy RBAC with application code `settings`. All access is RBAC-gated -- users only see settings they are authorized to view.

Permissions:

- `definition:read` / `definition:write` / `definition:delete`
- `value:read` / `value:write` / `value:delete`
- `secret:read` / `secret:write`
- `audit:read`
- `import:write` / `export:read`

Scope-aware checks:

- Users can only read/write their own user-scoped settings
- Team admins can mutate team-scoped values for their team only
- Secret reads require explicit `secret:read` permission
- All mutations are audited

## Deployment Modes

### Embedded (Conductor)

- API runs inside Conductor's ServiceOrchestrator
- SQLite database in `~/Library/Application Support/`
- No separate container needed
- Consumer services access via UnifiedProxy at `localhost:9100/settings`

### Docker Compose (Development)

- API container + PostgreSQL container
- Angular dev server proxied
- Corporate cert support via `certs/` directory

### Hosted (Production)

- API in container with PostgreSQL
- Angular SPA served from wwwroot or separate nginx container
- External Andy Auth and Andy RBAC
