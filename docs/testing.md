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

- EF Core persistence on SQLite (via `CustomWebApplicationFactory`)
- API + database end-to-end flows
- Auth and RBAC enforcement
- MCP endpoint
- Import/export round-trip with real database
- Secret encryption with Data Protection

Uses `WebApplicationFactory<Program>` with in-memory SQLite for fast, isolated tests.

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

- SQLite: migrations apply, CRUD works, resolution queries correct
- PostgreSQL: same test suite runs against real Postgres (CI with ephemeral container)

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
3. **UI** -- Angular unit tests, Angular build
4. **Smoke** -- Docker build, Compose startup, health checks
