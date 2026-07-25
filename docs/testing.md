# Testing Strategy

## Goals

Validate:

- Correctness of scoped value resolution
- Validation behavior for typed settings
- API correctness and authorization enforcement
- Secret encryption and RBAC-gated access
- CLI behavior
- MCP tool behavior
- Angular UI components
- Conductor embedded mode compatibility

## Test Pyramid

### 1. Unit Tests (`Andy.Settings.Tests.Unit`)

Primary targets:

- **Resolution engine** -- scope precedence, fallback, explanation metadata
- **Definition validation** -- type checks, schema validation, required fields
- **Mutation rules** -- scope enforcement, optimistic concurrency
- **Audit event creation** -- correct event types, secret payloads excluded
- **Secret service** -- encryption/decryption, rotation
- **Import/export serialization** -- round-trip, secret masking
- **Controllers** -- request validation, response codes, auth requirements
- **MCP tools** -- input/output contracts, error handling
- **CLI commands** -- command parsing, output formatting

Testing stack: xUnit 2.9.2, Moq 4.20.72, FluentAssertions 6.12.2, EF Core InMemory 8.0.11

### 2. Integration Tests (`Andy.Settings.Tests.Integration`)

Primary targets:

- EF Core persistence on SQLite and PostgreSQL (via `CustomWebApplicationFactory`)
- API + database end-to-end flows
- Auth and RBAC enforcement
- MCP endpoint
- Import/export round-trip with real database
- Secret encryption with Data Protection

Uses `WebApplicationFactory<Program>` with in-memory SQLite for fast, isolated tests, or a real PostgreSQL server when `ANDY_SETTINGS_TEST_POSTGRES` is set (see Database Providers below).

### 3. Frontend Tests (`client/`)

- Component tests (Jasmine/Karma)
- Service tests with `HttpClientTestingModule`
- Auth guard tests
- Interceptor tests

## Unit Test Coverage

### Resolution Engine

| Test Case | Expected |
|-----------|----------|
| No assignment | Returns default from definition |
| Machine scope set | Overrides default |
| User scope set | Overrides machine |
| Team scope set | Overrides user (where configured) |
| Workspace scope set | Overrides team |
| Runtime override set | Wins over all |
| Unsupported scope | Rejected |
| Deleted assignment | Falls back correctly |
| Explanation metadata | Shows full source chain |

### Definition Validation

| Test Case | Expected |
|-----------|----------|
| Required fields missing | Rejected |
| Enum value not in allowed set | Rejected |
| URI value invalid | Rejected |
| Integer out of range | Rejected |
| JSON payload invalid against schema | Rejected |
| Deprecated definition | Warning surfaced |
| Secret definition via non-secret path | Rejected |

### Secret Service

| Test Case | Expected |
|-----------|----------|
| Set secret | Value encrypted before storage |
| Get secret with `secret:read` | Value decrypted |
| Get secret without `secret:read` | 403 Forbidden |
| Rotate secret | New encrypted value, audit event emitted |
| Export with secrets | Values masked unless `--include-secrets` + permission |
| Audit for secret change | Metadata only, no payload |

### Audit

| Test Case | Expected |
|-----------|----------|
| Create mutation | Audit event with `Created` type |
| Update mutation | Audit event with `Updated` type, before/after |
| Delete mutation | Audit event with `Deleted` type |
| Secret rotation | Audit event with metadata only |
| Actor metadata | Preserved from JWT claims |

## Integration Tests

### API Endpoints

Test all CRUD flows:

- Definitions: create, read, update, delete, search
- Values: set, get, delete by scope
- Effective resolution: single, batch, explain
- Secrets: set, read (authorized), read (unauthorized → 403), rotate
- Audit: query by key, date range, actor
- Import/export: round-trip, preview

### Auth & RBAC

- Unauthenticated request → 401
- Missing permission → 403
- User accessing another user's scope → 403
- Dev mode bypass works in Development environment

### Database Providers

Both shipped providers are tested, because several defects found in the 2026-07
audit were provider-divergent and a SQLite-only suite could not see them — most
sharply a negative SQL `OFFSET`, which SQLite clamps (silently serving page 1)
and PostgreSQL rejects with a 500 (rivoli-ai/andy-settings#148).

- **SQLite** (default): migrations apply, CRUD works, resolution queries correct.
  No setup required — this is what `dotnet test` runs.
- **PostgreSQL**: the same integration suite, against a real server. CI runs it
  as a second step using an ephemeral `postgres:16-alpine` service container.

To run the PostgreSQL pass locally, point the suite at any server:

```bash
docker run -d --name pg-test -e POSTGRES_PASSWORD=test -e POSTGRES_USER=test \
  -e POSTGRES_DB=andy_settings_test -p 55432:5432 postgres:16-alpine

ANDY_SETTINGS_TEST_POSTGRES="Host=localhost;Port=55432;Database=andy_settings_test;Username=test;Password=test" \
  dotnet test tests/Andy.Settings.Tests.Integration
```

Each factory instance creates and drops its own schema, so xUnit's parallel
class execution can share one server safely.

## CLI Tests

- Command parsing correctness
- JSON output stability
- Table output formatting
- Non-zero exit codes on validation failure
- Auth state handling

## MCP Tests

For each tool:

- Input schema correctness
- Output schema correctness
- Authorization handling
- Secret access denied without permission
- Mutation behavior with audit trail

## Coverage Targets

| Layer | Target |
|-------|--------|
| Domain | 90%+ |
| Application (resolution engine) | 90%+ |
| Application (services) | 85%+ |
| API controllers | 80%+ |
| Infrastructure | Scenario coverage |
| Frontend | Critical flow coverage |

## CI Test Stages

1. **Fast** -- build, lint, unit tests
2. **Integration** -- SQLite integration tests, API tests, CLI tests
3. **Integration (PostgreSQL)** -- the same integration suite against an
   ephemeral `postgres:16-alpine` service container
4. **UI** -- Angular unit tests, Angular build
5. **Smoke** -- Docker build, Compose startup, health checks
