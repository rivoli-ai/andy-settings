# Andy Settings — Security

## Security Objectives

Andy Settings manages configuration that may include service endpoints, credentials references, runtime controls, and user/team-scoped settings. Security is therefore a primary design concern.

Objectives:

- authenticate all non-bootstrap access
- authorize all reads and writes by operation and scope
- isolate secrets from ordinary settings values
- maintain auditable change history
- minimize local attack surface in desktop mode
- support secure containerized deployment patterns

## Threat Model

Primary risks include:

- unauthorized reading of settings values
- unauthorized modification of settings
- privilege escalation through scope misuse
- secret disclosure
- insecure local bootstrap flows
- token misuse in CLI or MCP contexts
- audit tampering
- insecure default Docker configuration

## Security Principles

- secure by default
- least privilege
- separation of duties
- explicit scope boundaries
- deny-by-default authorization
- no plaintext secrets in general settings tables
- immutable audit trail where practical

## Authentication

## Primary Authentication Provider

Andy Settings should rely on Andy Auth for OAuth 2.0 / OIDC authentication.

Supported client types:

- browser SPA
- CLI public client
- backend confidential client
- local desktop or host app public client
- MCP-adjacent tool host integration where appropriate

## Local Bootstrap Mode

Because the initial system is local-first, bootstrap mode is required for first-run setup.

Rules:

- available only on localhost
- disabled automatically once initial admin/bootstrap state is established
- heavily logged to audit
- cannot be exposed in shared deployment mode

Bootstrap capabilities should be minimal:

- initialize data store
- create initial local admin
- configure authority / auth mode

## Token Handling

Rules:

- API accepts bearer tokens
- Angular client uses OIDC best practices for SPA flows
- CLI stores tokens securely using OS-native secure storage where possible
- access tokens are never logged
- refresh tokens are handled only where required and securely stored

## Authorization

Authorization should be enforced using Andy RBAC.

### Permission Model

Suggested permissions:

- `andy-settings:definition:read`
- `andy-settings:definition:write`
- `andy-settings:value:read`
- `andy-settings:value:write`
- `andy-settings:secret:read`
- `andy-settings:secret:write`
- `andy-settings:audit:read`
- `andy-settings:team:admin`
- `andy-settings:bootstrap:admin`

### Scope-Aware Authorization

Permission checks must include scope context.

Examples:

- a user may read their own user-scoped settings but not another user’s
- team admins may mutate team-scoped values for their team only
- machine scope changes may require elevated local admin permissions
- secret reads should be more restricted than ordinary value reads

### Application Layer Enforcement

Authorization must not live only in controllers.

Enforce checks in:

- application services
- mutation handlers
- secret access flows
- import/export flows
- MCP tool handlers
- CLI server-side operations

## Secrets Security

## Secret Storage Strategy

Do not store secret payloads in plaintext settings tables.

Instead:

- store setting metadata and secret references in main persistence
- store actual secret material in macOS Keychain in local-first mode
- allow future backends for shared deployments

## Secret Read Rules

- reading a secret requires explicit permission
- UI should mask secrets by default
- CLI should avoid printing secret values unless explicitly requested and authorized
- audit should capture secret access metadata without logging the payload

## Secret Rotation

- support rotation without exposing prior values
- rotation should emit audit metadata
- rotations should invalidate relevant caches

## Data Protection

### At Rest

Local-first mode:

- SQLite file protected by OS file permissions
- secret payloads in Keychain
- optional database encryption may be added later if required

Shared mode:

- PostgreSQL disk encryption handled by deployment platform
- secret backend chosen according to shared deployment architecture

### In Transit

- HTTPS required outside strictly local development scenarios
- secure cookies / token transport as applicable
- gRPC over TLS in shared mode
- local HTTP allowances only for developer workflows where explicitly configured

## Audit and Accountability

Every mutation of settings should produce audit metadata including:

- actor identity
- operation type
- setting key
- target scope
- timestamp
- correlation id

Audit records for secrets should not include plaintext secret values.

Suggested audit event types:

- definition created
- definition updated
- definition deleted
- value created
- value updated
- value deleted
- secret written
- secret rotated
- export generated
- import attempted
- bootstrap enabled/disabled

## Input Validation

All mutation paths must validate:

- known definition exists
- value shape matches definition type
- scope is allowed for definition
- actor is allowed for target scope
- secret operations use secret-aware path only
- bulk import payloads are schema-valid

Reject unknown or malformed inputs explicitly.

## Concurrency and Integrity

Use optimistic concurrency for settings updates.

Measures:

- etag or version field on assignments
- conflict detection on update
- explicit error response for stale writes
- audit correlation for concurrent write failures where useful

## MCP Security

MCP introduces a tool-mediated access path and should be treated as a privileged integration surface.

Rules:

- same auth and RBAC model as API-backed mutations
- mutation tools are permission-gated
- secret access tools are strongly restricted
- no hidden side effects
- tool schemas should be explicit and typed
- audit every mutation performed via MCP

## CLI Security

The CLI should:

- store tokens securely
- support logout and token revocation workflows where possible
- redact secrets in normal output
- require explicit flags for dangerous operations
- avoid shell-history leakage for secrets where possible

Examples:

- support reading secret values from stdin
- discourage passing secrets directly on command line

## Web UI Security

The Angular client should:

- use OIDC best practices
- protect routes with auth guards
- hide unauthorized actions in the UI while still relying on server enforcement
- use CSRF protections as appropriate for cookie-based admin flows
- sanitize user-generated display content

## Docker / Container Security

### API Container

- run as non-root where practical
- minimal runtime image
- no secret material baked into image
- environment-based bootstrap only for non-secret values
- mount certificates and config explicitly

### Web Container

- static assets served by hardened web server or reverse proxy
- no runtime secrets in frontend bundle
- environment-specific API endpoints injected safely

### Compose Defaults

- local dev compose should be clearly marked non-production
- avoid exposing services unnecessarily beyond localhost in dev
- separate dev certificates from production certs

## Logging Security

Never log:

- access tokens
- refresh tokens
- secret payloads
- raw authorization headers
- full Keychain handles if sensitive

Do log:

- auth success/failure events
- authorization denials
- bootstrap usage
- mutation metadata
- security configuration warnings

## Security Testing Requirements

Security-focused tests should validate:

- unauthorized requests rejected
- cross-scope access prevented
- secret payload not persisted in wrong store
- audit trails exist for mutations
- token-less access denied after bootstrap completion
- MCP unauthorized tools denied
- CLI dangerous operations require explicit inputs

## Hardening Roadmap

### MVP

- authn via Andy Auth
- authz via Andy RBAC
- Keychain secrets
- audit logging
- optimistic concurrency
- basic rate limiting on API

### Later

- stricter anomaly detection
- export signing / integrity metadata
- optional database encryption
- secret backend pluggability for hosted mode
- stronger bootstrap lock-down workflows
- advanced admin approval for high-risk changes
