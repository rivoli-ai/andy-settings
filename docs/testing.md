# Andy Settings — Testing Strategy

## Goals

The testing strategy for Andy Settings must validate:

- correctness of scoped value resolution
- validation behavior for typed settings
- API correctness
- authorization enforcement
- secret handling safety
- CLI behavior
- MCP tool behavior
- Angular UI integration
- Docker-based local development workflows

The system should be tested as both a library and an application platform.

## Testing Principles

- Test the domain before the transport.
- Test explanation metadata, not just final values.
- Test security boundaries as first-class behavior.
- Test local-first mode independently from shared mode.
- Make contract tests reusable across REST, gRPC, CLI, and MCP.

## Test Pyramid

### 1. Unit Tests

Primary targets:

- definition validation
- scope precedence rules
- effective value resolution
- value mutation rules
- audit event creation
- import/export serialization
- secret reference handling

Suggested projects:

- `Andy.Settings.Tests`
- `Andy.Settings.Mcp.Tests`
- `Andy.Settings.Client.Tests`

### 2. Integration Tests

Primary targets:

- EF Core persistence
- SQLite behavior
- PostgreSQL behavior
- API + database integration
- auth and RBAC integration boundaries
- CLI against a running API
- MCP host against a test API

Suggested projects:

- `Andy.Settings.Api.Tests`
- `Andy.Settings.IntegrationTests`

### 3. End-to-End Tests

Primary targets:

- Angular UI login and navigation
- settings editing workflows
- effective value inspection
- secret rotation flows
- import/export flows
- local Docker Compose environment startup

## Unit Test Coverage Areas

### Resolution Engine

Test cases:

- no assignment returns default
- machine scope overrides default
- user scope overrides machine
- team scope overrides user where configured
- workspace scope overrides team
- runtime override wins over all others
- unsupported scope is rejected
- deleted assignment falls back correctly
- explanation metadata shows full source chain

### Definition Validation

Test cases:

- required fields enforced
- enum values restricted correctly
- URI values validated
- integer ranges validated
- JSON payload schema validation
- deprecated definition warnings surfaced
- secret definitions reject plain non-secret write paths

### Mutation Rules

Test cases:

- value set at allowed scope succeeds
- value set at forbidden scope fails
- secret write stores only reference in primary store
- optimistic concurrency conflict is handled
- bulk mutations validate atomically where intended

### Audit

Test cases:

- create mutation emits audit event
- update mutation emits audit event
- delete mutation emits audit event
- secret rotation emits metadata-only audit entry
- actor metadata is preserved

## Persistence Tests

### SQLite Integration Tests

Verify:

- migrations apply successfully
- CRUD operations work
- resolution queries return expected results
- audit queries work correctly
- secret reference persistence works

### PostgreSQL Integration Tests

Verify:

- schema compatibility
- indexing assumptions
- concurrency behavior
- audit queries under realistic load

Use ephemeral PostgreSQL containers in CI.

## API Tests

### REST API

Test:

- definitions endpoints
- values endpoints
- effective resolution endpoints
- import/export endpoints
- health endpoints
- auth-required routes
- RBAC-denied routes
- validation errors and problem details

### gRPC API

Test:

- unary resolution calls
- batch resolution calls
- mutation calls
- audit lookup calls
- auth token forwarding

## CLI Tests

CLI tests should cover:

- command parsing
- auth login state handling
- JSON output stability
- table output formatting
- non-zero exit codes on validation failure
- bulk import/export behavior
- interaction with a test API

Recommended split:

- unit tests for command handlers
- integration tests for real CLI process execution

## MCP Tests

### Tool Contract Tests

For each tool, validate:

- input schema correctness
- output schema correctness
- authorization handling
- validation error handling
- mutation behavior where supported

### Safety Tests

- forbidden secret read is denied
- write without permission is denied
- malformed mutation payload fails safely
- scope escalation attempts are rejected

## Angular Front-End Tests

### Unit Tests

Test:

- settings list components
- scope picker behavior
- value editor behavior
- audit timeline rendering
- auth guard integration
- RBAC-aware UI state

### Component/Integration Tests

Test:

- definitions search and filtering
- effective value inspection flow
- editing typed settings
- secret masking and rotation flow
- import/export interactions

### E2E Tests

Use Playwright or Cypress.

Scenarios:

- login
- browse settings by application
- edit user-scoped setting
- inspect effective value explanation
- rotate secret
- verify audit entry appears

## Contract Tests

A shared contract test suite should verify consistent semantics across interfaces.

Examples:

- REST resolve and gRPC resolve return same winning value
- CLI `get` matches API effective resolution
- MCP `settings_get_effective_value` matches REST result

## Performance Tests

Initial performance testing should focus on:

- batch effective resolution latency
- audit query pagination latency
- startup definition loading time
- CLI cold start time
- web UI page load and main list rendering

Not a scale exercise initially, but enough to catch obvious inefficiencies.

## Test Data Strategy

Maintain reusable seed datasets:

- default seed definitions
- sample users
- sample teams
- sample app/service registrations
- sample scope overlays
- sample secret references

Use named fixtures for:

- local desktop mode
- team/shared mode
- RBAC-restricted mode
- anonymous bootstrap mode

## CI Test Stages

### Stage 1 — Fast Validation

- restore
- build
- lint / analyzers
- unit tests

### Stage 2 — Integration

- SQLite integration tests
- API integration tests
- CLI integration tests
- gRPC tests

### Stage 3 — UI

- Angular unit tests
- Angular build
- browser integration / E2E tests

### Stage 4 — Containerized Smoke Tests

- Docker build API
- Docker build web
- Docker Compose startup
- health checks
- smoke API calls

## Coverage Expectations

Recommended thresholds:

- domain libraries: high coverage emphasis
- API application layer: moderate to high coverage
- infrastructure: meaningful scenario coverage over raw percentage targets
- UI: critical flow coverage rather than chasing superficial numbers

Suggested minimums for the initial release:

- core domain: 85%+
- resolution engine: 90%+
- API application services: 80%+

## Failure Injection / Negative Testing

Required negative tests:

- invalid definitions
- unsupported scope writes
- stale etag / concurrency mismatch
- invalid auth token
- missing RBAC permission
- Keychain unavailable or erroring
- database unavailable
- malformed import payload
- conflicting import data

## Regression Test Focus Areas

Every release should guard against regressions in:

- scope precedence
- secret handling
- authz enforcement
- import/export compatibility
- MCP tool contracts
- CLI argument compatibility

## Release Gates

Before release:

- all unit and integration tests green
- API and web containers build successfully
- smoke environment passes
- migration path validated
- key auth/RBAC scenarios pass
- MCP tool contract tests pass
- CLI smoke commands pass
