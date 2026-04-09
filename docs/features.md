# Andy Settings — Features

## Overview

Andy Settings is the configuration control plane for the Andy ecosystem. It provides a local-first, schema-driven, policy-aware configuration system for desktop applications and backend services, with a path from single-user local development on macOS to shared, team-scoped, remotely managed deployments.

The system is designed for:

- Andy DevPilot
- Andy Code Index
- Andy Docs
- Andy Containers
- Andy Auth integration (OAuth 2.0 / OIDC)
- Andy RBAC integration (authorization and scoped administration)
- local macOS application settings
- local and remote service settings
- CLI and MCP access patterns used across the broader Andy backend portfolio

## Product Goals

- Provide a single source of truth for configuration across Andy services.
- Support local-only operation on a Mac without requiring cloud infrastructure.
- Expose the same settings through API, CLI, MCP, and Web UI.
- Support typed settings, validation, inheritance, auditability, and secrets.
- Make user-, team-, application-, and service-scoped configuration first-class.
- Preserve compatibility with .NET configuration patterns and Angular clients.

## Core Feature Set

### 1. Local-First Configuration Store

- SQLite-backed configuration store for local development and local desktop deployments.
- Optional PostgreSQL backend for shared/team deployments later.
- Full local operation without requiring distributed coordination.
- File-backed bootstrap configuration for first-run setup.

### 2. Typed Configuration Definitions

- Strongly typed setting definitions.
- Supported types:
  - string
  - integer
  - decimal
  - boolean
  - enum
  - duration
  - URI
  - JSON object
  - string list
  - secret reference
- Validation rules per definition.
- Required/optional semantics.
- Default values.
- UI metadata for form generation.

### 3. Scoped Configuration Resolution

Supported scopes:

- machine
- application
- service
- user
- team
- workspace
- session/runtime override

Resolution order is deterministic and explainable.

Example precedence:

1. built-in default
2. machine
3. application
4. service
5. user
6. team
7. workspace
8. runtime override

### 4. Effective Value Calculation

- Resolve effective settings for a given context.
- Return:
  - resolved value
  - winning scope
  - overridden values
  - validation status
  - last updated metadata
- Support “why is this the active value?” UX in API and UI.

### 5. Configuration Definitions Registry

- Central registry of setting definitions.
- Discoverable by application, service, category, and tag.
- Supports versioned evolution of definitions.
- Allows services to register definitions at startup or via migration.

### 6. Secrets Handling

- Separate secret metadata from general configuration values.
- Use macOS Keychain for local secret material in local-first mode.
- Keep secret references in the main settings store.
- Support future remote secret backends.
- Masked retrieval and rotation workflows.

### 7. .NET 8 Configuration Integration

- Integrates with `IConfiguration`.
- Supports `IOptions<T>`, `IOptionsSnapshot<T>`, and `IOptionsMonitor<T>`.
- Bridges Andy Settings to `Andy.Configuration` style strongly typed options.
- Startup validation and runtime refresh support.

### 8. REST API

Primary API capabilities:

- setting definition CRUD
- setting assignment CRUD
- effective settings resolution
- bulk reads/writes
- secret set/rotate/delete
- audit history lookup
- health and readiness
- import/export
- service registration

### 9. gRPC API

gRPC endpoints for backend and agent-style consumers.

Use cases:

- high-performance resolution
- batch reads
- bulk writes
- local desktop integration
- local service communication
- future sync agent integration

### 10. MCP Support

Andy Settings exposes a Model Context Protocol interface for AI assistants and tool hosts.

Supported MCP capabilities:

- browse setting definitions
- resolve effective settings for a service or user context
- inspect scopes and inheritance
- modify allowed settings with authorization checks
- rotate secrets where policy allows
- explain why a value is active
- export diagnostics for troubleshooting

Example MCP tool concepts:

- `settings.list_definitions`
- `settings.get_effective`
- `settings.get_value`
- `settings.set_value`
- `settings.delete_value`
- `settings.list_scopes`
- `settings.explain_resolution`
- `settings.list_audit_events`

### 11. CLI Support

Andy Settings includes a first-party CLI in the same repo.

CLI capabilities:

- login / auth integration
- local bootstrap
- list definitions
- get effective values
- set values per scope
- bulk import/export
- secret rotation
- audit history queries
- environment/profile switching
- JSON and table output

Example commands:

```bash
andy-settings login
andy-settings definitions list --app andy-containers
andy-settings get andy.containers.defaultProvider --scope user
andy-settings set andy.codeindex.embedding.provider openai --scope team --team platform
andy-settings explain andy.auth.authority --user alice@example.com
andy-settings export --format json > settings-export.json
```

### 12. Angular Web Front-End

- Angular web client in the same repo.
- Shared identity and authorization patterns aligned with the broader Andy UI approach.
- Form-driven editing generated from setting definitions.
- Scope-aware editing.
- Secret-specific workflows.
- Audit and diff views.
- Responsive layout for desktop-class browser usage.

### 13. Authorization via Andy RBAC

Permissions are enforced for:

- read definitions
- write definitions
- read values
- write values
- read secrets
- write secrets
- administer team settings
- view audit
- manage imports/exports

Suggested permission namespace:

- `andy-settings:definition:read`
- `andy-settings:definition:write`
- `andy-settings:value:read`
- `andy-settings:value:write`
- `andy-settings:secret:read`
- `andy-settings:secret:write`
- `andy-settings:audit:read`
- `andy-settings:team:admin`
- `andy-settings:sync:admin`

### 14. Authentication via Andy Auth

- OAuth 2.0 / OIDC integration.
- Local bootstrap mode for first-run setup.
- PKCE-friendly public client support for Angular and CLI flows.
- Token-based API access for web, CLI, and MCP-adjacent consumers.

### 15. Audit and Change History

- Append-only history of setting changes.
- Who changed what, when, and at which scope.
- Before/after metadata.
- Secret changes log metadata without exposing secret payloads.
- API and UI views for audit history.

### 16. Import / Export

- JSON export of definitions and/or values.
- Selective export by app, service, scope, or team.
- Import with validation and dry-run mode.
- Good fit for migration, backup, and environment promotion.

### 17. Service Registration

Services can register:

- service code
- supported configuration domains
- setting definitions
- desired refresh behavior
- health status
- sync capabilities

### 18. Runtime Refresh

Two supported consumption models:

- startup snapshot
- live refresh / subscription

Suitable for:

- feature flags
- LLM provider switching
- polling intervals
- runtime limits
- UI preferences

### 19. Multi-App / Multi-Service Support

The system is designed to serve multiple Andy services from one configuration authority.

Examples:

- `andy-auth.*`
- `andy-rbac.*`
- `andy-containers.*`
- `andy-codeindex.*`
- `andy-devpilot.*`
- `andy-docs.*`
- `andy-host.*`

### 20. Local Desktop Host Settings

Specific support for the macOS host application:

- local app settings
- startup preferences
- update channels
- theme and UX preferences
- local service endpoints
- local paths and workspace folders
- provider credentials references

## Non-Goals (Initial Release)

- Multi-datacenter consensus or distributed coordination.
- Complex policy engines beyond RBAC and definition validation.
- General-purpose secret-management replacement.
- Kubernetes-first delivery as the primary initial deployment target.

## Initial MVP

The MVP should include:

- .NET 8 API
- SQLite persistence
- macOS Keychain secret backend
- Angular web client
- CLI
- MCP server support
- REST and gRPC APIs
- OIDC integration with Andy Auth
- RBAC integration with Andy RBAC
- typed definitions and scoped resolution
- Docker / Docker Compose local development workflow
