# Features

## Overview

Andy Settings is the configuration control plane for the Andy ecosystem. It provides a schema-driven, policy-aware configuration system for the Conductor macOS app and Andy backend services, with a path from single-user embedded operation to shared team deployments.

## Product Goals

- Provide a single source of truth for configuration across Andy services.
- Run embedded inside Conductor on macOS without requiring external infrastructure.
- Expose the same settings through API, CLI, MCP, and Web UI.
- Support typed settings, validation, scope inheritance, auditability, and encrypted secrets.
- Make user-, team-, application-, and service-scoped configuration first-class.
- Integrate with Andy Auth and Andy RBAC for authentication and authorization.

## Core Features

### 1. Typed Configuration Definitions

Strongly typed setting definitions with:

- Supported types: string, integer, decimal, boolean, enum, duration, URI, JSON object, string list, secret
- Validation rules per definition (JSON schema)
- Required/optional semantics
- Default values
- UI metadata for automatic form generation
- Tags and categories for organization
- Deprecation metadata

### 2. Scoped Configuration Resolution

Settings can be assigned at multiple scopes with deterministic precedence:

1. built-in default
2. machine
3. application
4. service
5. user
6. team
7. workspace
8. runtime override

The resolution engine returns effective values with full explanation metadata -- "why is this value active?" is a first-class query.

### 3. Encrypted Secrets

Secret-bearing settings are encrypted using ASP.NET Core Data Protection API (AES-256-GCM):

- Encrypted at rest in the database
- Only decrypted for users with `secret:read` RBAC permission
- Masked in list views, exports, and audit logs
- Rotation support without exposing prior values
- All secret access is audited

### 4. Conductor Integration

Andy Settings runs as the 8th embedded .NET service inside Conductor:

- Port 9107, proxy prefix `/settings`
- SQLite backend at `~/Library/Application Support/ai.rivoli.conductor/db/`
- Consumer services (auth, rbac, containers, code-index, devpilot, docs) read configuration from andy-settings
- Conductor's ActionBus integration for audit trail
- AppPreferences (UserDefaults) remains for UI chrome only

### 5. REST API

- Setting definition CRUD
- Scoped assignment CRUD
- Effective value resolution with explanation
- Encrypted secret set/rotate/delete
- Audit history lookup
- Bulk import/export with preview
- Health and readiness probes
- Swagger/OpenAPI documentation

### 6. MCP Support

Model Context Protocol tools for AI assistants:

- `settings_list_definitions` -- browse by app/category
- `settings_get_effective` -- resolve effective value
- `settings_set_value` -- set scoped value with auth
- `settings_explain` -- explain resolution chain
- `settings_audit` -- recent change history
- `settings_search` -- search definitions

### 7. CLI

First-party CLI in the same repo:

```bash
andy-settings auth login
andy-settings definitions list --app andy-containers
andy-settings get andy.containers.defaultProvider --scope user
andy-settings set andy.codeindex.embedding.provider openai --scope team --team platform
andy-settings explain andy.auth.authority --user alice@example.com
andy-settings export --format json > settings-export.json
andy-settings import settings.json --preview
```

### 8. Angular Web Front-End

- Form-driven editing generated from setting definitions
- Scope-aware editing with visual scope chain
- Secret masking with RBAC-gated reveal
- Audit timeline and diff views
- Environment comparison (side-by-side)
- Responsive layout matching Andy Containers / Code Index style

### 9. Authentication via Andy Auth

- OAuth 2.0 / OIDC integration
- Embedded bootstrap mode for Conductor first-run (localhost-only)
- PKCE public client support for Angular and CLI
- JWT Bearer token API access

### 10. Authorization via Andy RBAC

All access is RBAC-gated -- users only see settings they are authorized to view.

Permissions:

- `definition:read` / `definition:write` / `definition:delete`
- `value:read` / `value:write` / `value:delete`
- `secret:read` / `secret:write`
- `audit:read`
- `import:write` / `export:read`

Scope-aware enforcement: users can only access their own user-scoped settings, team admins manage team settings, etc.

### 11. Audit and Change History

- Append-only history of all setting changes
- Who changed what, when, and at which scope
- Before/after metadata (secrets excluded from payload)
- API, CLI, and UI views for audit history
- Filterable by date range, actor, action type

### 12. Import / Export

- JSON and YAML export with scope/app/environment filters
- Import with validation and dry-run preview mode
- Diff view showing additions, modifications, deletions before applying
- Suitable for migration, backup, and environment promotion

### 13. Multi-App / Multi-Service Support

Serves all Andy services from one configuration authority:

- `andy.auth.*`
- `andy.rbac.*`
- `andy.containers.*`
- `andy.codeindex.*`
- `andy.devpilot.*`
- `andy.docs.*`
- `andy.settings.*`

### 14. Multi-Language Examples

The `examples/` directory demonstrates API and MCP consumption from:

- C# (.NET)
- Python
- JavaScript / TypeScript
- Go
- Rust
- PowerShell

### 15. OpenTelemetry

- Tracing with custom ActivitySource
- Metrics: resolution count/latency, mutation count, secret rotation count
- OTLP exporter for production, console exporter for development
- ASP.NET Core + EF Core + HTTP client instrumentation

## Non-Goals (Initial Release)

- Multi-datacenter consensus or distributed coordination
- Complex policy engines beyond RBAC and definition validation
- General-purpose secret management replacement (use dedicated vaults for production secrets)
- gRPC API (may be added later if needed for performance)
- SignalR/SSE live refresh (may be added later)
