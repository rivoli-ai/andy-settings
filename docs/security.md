# Security

## Objectives

Andy Settings manages configuration that may include service endpoints, credential references, runtime controls, and user/team-scoped settings. Security is a primary design concern.

- Authenticate all non-bootstrap access
- Authorize all reads and writes by operation and scope via RBAC
- Encrypt secrets at rest; only decrypt for authorized users
- Maintain auditable, append-only change history
- Minimize attack surface in embedded (Conductor) mode

## Threat Model

Primary risks:

- Unauthorized reading of settings values
- Unauthorized modification of settings
- Privilege escalation through scope misuse
- Secret disclosure (plaintext leak, log exposure, export leak)
- Token misuse in CLI or MCP contexts
- Audit tampering

## Authentication

### Andy Auth Integration

Andy Settings uses Andy Auth for OAuth 2.0 / OIDC authentication.

Supported client types:

- Browser SPA (Angular, PKCE)
- CLI public client (Device Flow) — PLANNED, not implemented; the CLI currently takes a bearer token from `ANDY_SETTINGS_TOKEN`
- Backend confidential client (service-to-service)
- Conductor (tokens forwarded via UnifiedProxy)

### Auth bypass guard

Two configuration switches disable access control entirely: an empty
`AndyAuth:Authority` turns off authentication, and the `Development` environment
registers a policy provider that satisfies **every** `[RequirePermission]` on
every controller and MCP tool.

Outside `Development`, the service now **refuses to start** when
`AndyAuth:Authority` is empty, unless `ANDY_ALLOW_INSECURE_AUTH=true` makes
running without authentication a deliberate act rather than an environment-name
coincidence (rivoli-ai/andy-settings#144). Whenever either bypass is active, a
warning is logged at startup.

This mirrors the AK1 messaging guard, which fails the boot rather than silently
degrading. Note that `docker-compose.yml` intentionally ships with both bypasses
active — it is a local development stack, not a deployment template.

### TLS in the CLI

The CLI validates server certificates by default. The bypass requires an
explicit `--insecure` flag or `ANDY_SETTINGS_INSECURE=true`, and warns on stderr
when active. It was previously unconditional, which meant every invocation —
carrying bearer tokens and plaintext secret values — accepted any certificate
(rivoli-ai/andy-settings#129).

Secret values may be passed on stdin or entered at a hidden prompt, so they need
not appear in shell history or `ps` output.

### Embedded Bootstrap Mode

For Conductor first-run when Andy Auth is not yet configured:

- Available only on localhost
- Auto-disabled once initial setup is complete
- Heavily logged to audit
- Cannot be enabled in shared deployment mode

### Token Handling

- API accepts JWT Bearer tokens
- Angular client uses OIDC best practices
- CLI stores tokens securely (OS-native storage where possible)
- Access tokens are never logged
- Conductor forwards Authorization headers through UnifiedProxy

## Authorization

### RBAC Model

All access is RBAC-gated via Andy RBAC with application code `settings`. Users only see settings they are authorized to view.

Permissions:

| Permission | Description |
|---|---|
| `definition:read` | View setting definitions |
| `definition:write` | Create/update definitions |
| `definition:delete` | Delete definitions |
| `value:read` | Read setting values |
| `value:write` | Set/update setting values |
| `value:delete` | Delete setting values |
| `secret:read` | Decrypt and view secret values |
| `secret:write` | Set/rotate secrets |
| `audit:read` | View audit history |
| `export:read` | Export settings |
| `import:write` | Import settings |

### Scope-Aware Authorization

Permission checks include scope context:

- Users can read/write their own user-scoped settings but not others'
- Team admins can mutate team-scoped values for their team only
- Machine scope changes require elevated permissions
- Secret reads are more restricted than ordinary value reads

### Application Layer Enforcement

Authorization is enforced in the application service layer, not just controllers. This ensures MCP tools, CLI operations, and any future interfaces enforce the same rules.

## Secret Security

### Encryption Strategy

Secrets are encrypted using ASP.NET Core Data Protection API (AES-256-CBC for confidentiality, HMAC-SHA256 for authentication — the framework default):

- Setting definitions marked with `is_secret = true`
- Secret values are encrypted before storage in the `encrypted_secrets` table
- Decryption only occurs when the requesting user has `secret:read` permission
- Data Protection keys stored in a protected volume (`/app/.aspnet/DataProtection-Keys` in Docker)

### Secret Read Rules

- Reading a secret requires explicit `secret:read` permission
- UI masks secrets by default; RBAC-gated reveal toggle
- CLI avoids printing secret values unless explicitly requested and authorized
- Exports mask secrets unless `--include-secrets` flag and `secret:read` permission
- Every successful decrypt records a `SecretRead` audit event carrying the actor,
  definition key and scope — and **never** the value or a digest of it. A digest
  in the audit log would let anyone with audit-read access confirm a guessed
  secret

### Secret Deletion

Deletion is **per-scope**. `DELETE /api/secrets/{key}` requires either a scope
(`scopeType`, plus `scopeId` for non-Machine scopes) or an explicit
`allScopes=true`; a request with neither is rejected with 400.

This endpoint previously took no scope parameters and always deleted every
scope, so clearing one user's credential also wiped the Machine-scope value and
every other user's (rivoli-ai/andy-settings#138). The ambiguous request is
rejected rather than quietly treated as a narrower delete, which would leave the
caller believing a still-stored secret was gone.

Every deletion records a `SecretDeleted` audit event.

### Secret Rotation

- Rotation creates a new encrypted value without exposing the prior value
- Rotation emits an audit event (metadata only, no payload)
- Consumers should re-read settings after rotation

## Data Protection

### At Rest

Embedded mode (Conductor):

- SQLite file at `~/Library/Application Support/ai.rivoli.conductor/db/andy-settings.sqlite`
- Protected by macOS file permissions and user account isolation
- Data Protection keys in app support directory

Shared mode:

- PostgreSQL with platform-managed disk encryption
- Data Protection keys in mounted Docker volume

### In Transit

- HTTPS required (self-signed certs for development, real certs for production)
- Conductor: HTTP on localhost is acceptable (UnifiedProxy is same-machine)

## Audit and Accountability

Every mutation produces an audit event:

- Actor identity (from JWT claims)
- Operation type (created, updated, deleted, secretRotated, imported, exported)
- Setting key and scope
- Before/after metadata (secret payloads excluded)
- Correlation ID for request tracing
- Timestamp

Audit records are append-only. The audit table has no UPDATE/DELETE operations exposed.

## Input Validation

All mutation paths validate:

- Definition exists for the target key
- Value shape matches definition data type
- Scope is in the definition's allowed scopes
- Actor is authorized for the target scope
- Secret operations use the secret-specific path
- Import payloads are schema-valid before applying

## Concurrency

Optimistic concurrency for setting assignments:

- `version` and `etag` fields on assignments
- Conflict detection on update (409 Conflict response)
- Audit correlation for concurrent write failures

## MCP Security

MCP tools enforce the same auth and RBAC model as the REST API:

- Mutation tools are permission-gated
- Secret access tools require `secret:read`
- Every mutation via MCP is audited
- Tool schemas are explicit and typed

## Docker / Container Security

API container:

- Runs as non-root user (`andysettings`)
- Minimal runtime image (aspnet:8.0)
- No secrets baked into image
- Corporate CA certs mounted as volumes
- Data Protection keys in named volume

## Logging Security

Never log: access tokens, refresh tokens, secret payloads, raw Authorization headers.

Do log: auth success/failure, authorization denials, bootstrap usage, mutation metadata, security warnings.
