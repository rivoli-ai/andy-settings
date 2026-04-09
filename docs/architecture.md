# Andy Settings — Architecture

## Overview

Andy Settings is a local-first configuration platform for the Andy ecosystem. It provides typed setting definitions, scoped assignments, effective-value resolution, secrets indirection, audit history, and access through Web UI, REST, gRPC, MCP, and CLI.

The architecture is designed to satisfy two deployment modes:

1. **Local Desktop Mode**
   - single Mac
   - local API
   - local UI
   - SQLite
   - macOS Keychain
   - localhost services

2. **Shared Service Mode**
   - hosted API
   - Angular client
   - PostgreSQL
   - team/user scopes
   - service-to-service consumption

## Architectural Principles

- Local-first before remote-first.
- Typed configuration, not just stringly typed key/value.
- Schema and validation live with definitions.
- Secrets are separate from general values.
- A single domain model is exposed consistently over all interfaces.
- Scope resolution must be deterministic and explainable.
- Same repo contains API, client SDKs, CLI, MCP server, and web UI.
- Docker and Docker Compose remain first-class for local development.

## Context Diagram

```text
+---------------------------------------------------------------+
|                        Andy Settings Repo                      |
|                                                               |
|  +------------------+   +------------------+   +------------+  |
|  | ASP.NET Core API |   | Angular Web App  |   | CLI        |  |
|  | REST + gRPC      |   | OIDC + RBAC      |   | dotnet tool|  |
|  +---------+--------+   +---------+--------+   +------+-----+  |
|            |                      |                    |        |
|            +----------+-----------+--------------------+        |
|                       |                                        |
|                 +-----v--------------------+                   |
|                 | Settings Domain Layer    |                   |
|                 | definitions / resolver   |                   |
|                 | audit / secrets / policy |                   |
|                 +-----+--------------------+                   |
|                       |                                        |
|        +--------------+---------------+                        |
|        |                              |                        |
|  +-----v-----------------+      +-----v------------------+     |
|  | Persistence Layer     |      | Secret Backend         |     |
|  | SQLite / PostgreSQL   |      | macOS Keychain         |     |
|  +-----------------------+      +------------------------+     |
|                                                               |
+---------------------------------------------------------------+

External Integrations:
- Andy Auth (OIDC / OAuth2)
- Andy RBAC (authorization)
- Andy Containers / DevPilot / Code Index / Docs / host app
- MCP clients / AI assistants
```

## Logical Components

### 1. Domain Layer

The domain layer is the heart of the system and contains the core abstractions.

Responsibilities:

- setting definitions
- setting assignments
- scope hierarchy
- effective value resolution
- validation
- secret reference handling
- audit events
- import/export model

Suggested projects:

- `Andy.Settings.Abstractions`
- `Andy.Settings`

### 2. API Layer

The API layer is an ASP.NET Core application exposing:

- REST endpoints
- gRPC services
- authn/authz integration
- health probes
- OpenAPI
- server-side validation

Suggested project:

- `Andy.Settings.Api`

### 3. Client SDK Layer

Client SDKs simplify integration from Andy services.

Suggested projects:

- `Andy.Settings.Client`
- `Andy.Settings.AspNetCore`

The ASP.NET Core integration package should support:

- client registration
- typed options binding
- live refresh subscription integration
- startup validation

### 4. Persistence Layer

The persistence layer uses EF Core and supports multiple providers.

Suggested project:

- `Andy.Settings.EntityFramework`

Backends:

- SQLite for local-first mode
- PostgreSQL for shared/team mode

### 5. Secret Layer

Secrets should be stored outside the main settings tables.

Suggested project:

- `Andy.Settings.Secrets.Keychain`

Responsibilities:

- macOS Keychain integration
- secret reference generation
- secret retrieval and rotation
- future secret backend abstraction

### 6. CLI Layer

A first-party CLI in the same repo should use the same client SDKs.

Suggested project:

- `Andy.Settings.Cli`

### 7. MCP Layer

The MCP server should expose the settings domain as tools.

Suggested project:

- `Andy.Settings.Mcp`

Responsibilities:

- expose safe tools for listing, resolving, updating, and auditing settings
- enforce auth and RBAC where applicable
- support local desktop assistant workflows

### 8. Web UI Layer

Angular application in the same repo.

Suggested path:

- `web/andy-settings-web` or `client/andy-settings-web`

Responsibilities:

- definitions browser
- values editor
- scope inspector
- secrets workflows
- audit views
- auth-aware UX

## Domain Model

### Setting Definition

A definition describes what a setting is.

Fields:

- key
- application code
- service code
- display name
- description
- category
- data type
- allowed scopes
- default value
- validation rules
- UI schema metadata
- secret flag
- tags
- deprecation metadata

### Setting Assignment

An assignment stores a value at a specific scope.

Fields:

- definition id
- scope type
- scope id
- subject type
- subject id
- value payload
- version
- etag
- actor metadata
- timestamps

### Secret Reference

Secret-bearing settings store a reference rather than the secret payload.

Fields:

- logical key
- backend kind
- secret handle
- created at
- rotated at

### Audit Event

Append-only record of config operations.

Fields:

- event id
- event type
- actor
- definition key
- scope
- before metadata
- after metadata
- trace id / correlation id
- timestamp

## Scope Resolution Engine

The resolution engine computes effective values for a context.

Inputs:

- subject context
- app context
- service context
- workspace context
- requested setting key(s)

Outputs:

- effective value
- winning assignment
- source chain
- validation result
- explanation metadata

Suggested scope precedence:

1. built-in default
2. machine
3. application
4. service
5. user
6. team
7. workspace
8. runtime override

This should be configurable at the framework level but stable in the product.

## Storage Model

### Tables

#### `setting_definitions`

- `id`
- `key`
- `application_code`
- `service_code`
- `display_name`
- `description`
- `category`
- `data_type`
- `default_value_json`
- `validation_json`
- `ui_schema_json`
- `is_secret`
- `allowed_scopes_json`
- `tags_json`
- `is_deprecated`
- `created_at`
- `updated_at`

#### `setting_assignments`

- `id`
- `definition_id`
- `scope_type`
- `scope_id`
- `subject_type`
- `subject_id`
- `value_json`
- `etag`
- `version`
- `updated_by`
- `updated_at`

#### `secret_references`

- `id`
- `definition_id`
- `scope_type`
- `scope_id`
- `subject_type`
- `subject_id`
- `backend_kind`
- `secret_handle`
- `updated_by`
- `updated_at`

#### `audit_events`

- `id`
- `event_type`
- `definition_key`
- `scope_type`
- `scope_id`
- `actor_type`
- `actor_id`
- `before_json`
- `after_json`
- `correlation_id`
- `created_at`

#### `service_registrations`

- `id`
- `application_code`
- `service_code`
- `display_name`
- `capabilities_json`
- `health_status`
- `last_seen_at`

## API Architecture

### REST Endpoints

Suggested resource groups:

- `/api/definitions`
- `/api/values`
- `/api/effective`
- `/api/secrets`
- `/api/audit`
- `/api/import`
- `/api/export`
- `/api/services`
- `/api/health`

### gRPC Services

Suggested services:

- `DefinitionsService`
- `ValuesService`
- `ResolutionService`
- `AuditService`
- `SecretsService`
- `ServiceRegistryService`

### MCP Tooling

The MCP layer should map directly to the domain model and not invent a parallel settings abstraction.

Example tool groups:

- definition discovery
- effective resolution
- scoped value inspection
- authorized mutation
- audit lookup
- troubleshooting / explanation

## Authentication and Authorization Flow

### Authentication

- Web UI uses OIDC with Andy Auth.
- CLI supports browser-based sign-in or device-code style flow.
- API accepts bearer tokens.
- Local bootstrap mode may be enabled for first-run localhost setup.

### Authorization

Authorization is enforced per operation via Andy RBAC.

Examples:

- read definition
- write definition
- read effective values
- write scoped values
- rotate secret
- read audit
- administer team scopes

## Runtime Update Model

Two consumer modes are supported.

### Snapshot Mode

- resolve on startup
- bind to strongly typed options
- good for stable infrastructure settings

### Subscription Mode

- live update channel via SignalR or Server-Sent Events
- optional in-memory cache
- good for dynamic flags and non-critical runtime settings

## Deployment Modes

### Local Desktop Mode

- API runs on localhost
- Angular UI runs locally or is served by API
- SQLite local database
- Keychain-backed secrets
- service consumers run on the same machine

### Local Containerized Dev Mode

- Docker Compose starts API, UI, and optional PostgreSQL
- useful for repo contributors and integration testing
- mirrors Andy Containers style local dev workflow

### Shared Team Mode

- API in container
- PostgreSQL database
- centrally accessible UI
- team/user scopes authoritative

## Repo Layout

Suggested repo layout:

```text
andy-settings/
├── .github/
├── docs/
├── openapi/
├── proto/
├── scripts/
├── src/
│   ├── Andy.Settings/
│   ├── Andy.Settings.Abstractions/
│   ├── Andy.Settings.Api/
│   ├── Andy.Settings.Client/
│   ├── Andy.Settings.AspNetCore/
│   ├── Andy.Settings.EntityFramework/
│   ├── Andy.Settings.Secrets.Keychain/
│   ├── Andy.Settings.Cli/
│   └── Andy.Settings.Mcp/
├── tests/
│   ├── Andy.Settings.Tests/
│   ├── Andy.Settings.Api.Tests/
│   ├── Andy.Settings.Client.Tests/
│   ├── Andy.Settings.IntegrationTests/
│   └── Andy.Settings.Mcp.Tests/
├── client/
│   └── andy-settings-web/
├── certs/
├── config/
├── docker-compose.yml
├── Dockerfile.api
├── Dockerfile.web
└── Andy.Settings.sln
```

## Key Design Decisions

### Why SQLite first?

- ideal for local desktop mode
- low operational overhead
- excellent for local-first bootstrapping
- easy testability

### Why PostgreSQL later?

- aligns with broader Andy backend patterns
- supports shared/team deployments
- supports audit-heavy workloads and flexible querying

### Why typed definitions instead of only key/value?

- better validation
- better UX generation
- safer migrations
- better compatibility with strongly typed .NET options

### Why separate secrets?

- minimizes accidental exposure
- supports OS-native secure storage
- allows future secret backend replacement

### Why put CLI and MCP in the same repo?

- shared contracts and behavior
- consistent authn/authz
- easier release coordination
- same operational story as the API and UI
