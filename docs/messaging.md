# Messaging

andy-settings publishes typed events whenever a setting assignment is
created, updated, or deleted. The event surface is intentionally narrow:
**notifications, not authoritative values.** Consumers GET the resolved
setting over REST so the audit trail stays tamper-evident on the
publisher side.

The wire contract follows [ADR 0001 — Messaging in the Andy
Ecosystem](https://github.com/rivoli-ai/andy-tasks/blob/main/docs/adr/0001-messaging.md).
Read that document first if you're plumbing a new service on the bus.

## Subject taxonomy

```
andy.settings.events.config.<application_code>.<kind>
```

| Token | Meaning | Example |
|---|---|---|
| `andy.settings.events.config` | Fixed prefix for this service's domain events. Publisher-exclusive. | — |
| `<application_code>` | The owning application of the changed `SettingDefinition`. Dots are sanitized to dashes for the wire (the payload carries the original value). | `mcp-proxy`, `andy-containers`, `andy-tasks` |
| `<kind>` | Past-tense verb describing the change. | `created`, `updated`, `deleted` |

Consumers should subscribe via NATS wildcards rather than enumerating
subjects:

- All config changes for a single app: `andy.settings.events.config.mcp-proxy.>`
- All updates across every app: `andy.settings.events.config.*.updated`
- Everything: `andy.settings.events.config.>`

## Stream + retention

| Stream | Subjects | Retention | Class |
|---|---|---|---|
| `ANDY_DOMAIN` | `andy.settings.events.>` (this service) plus the rest of the ecosystem's domain events | 90 days | Domain |

Per ADR 0001 AK5, `NatsStreamProvisioner` runs idempotently on every
boot — andy-settings will not corrupt a stream provisioned by another
service.

## Payload

```jsonc
{
  "schema_version": 1,
  "assignment_id": "8f49a4c0-...",
  "key": "andy.mcp-proxy.fronted_services",
  "application_code": "mcp-proxy",
  "scope_type": "Application",
  "scope_id": "mcp-proxy",
  "kind": "updated",
  "new_value_digest": "9a3c…",       // sha256 of new ValueJson; null on delete
  "etag": "5e0c…",
  "version": 7,
  "updated_by": "alice@rivoli.ai",
  "updated_at": "2026-05-11T22:49:13.4567890+00:00"
}
```

Field-by-field:

- **`schema_version`** — integer bumped on any breaking change. Always
  present. v1 is the initial release.
- **`new_value_digest`** — SHA-256 of the new `ValueJson` bytes,
  lowercase hex. `null` when `kind == "deleted"`. Lets consumers detect
  re-delivery vs. a true change without fetching the value.
- **`scope_type`** — the `ScopeType` enum value (`Machine`,
  `Application`, `Service`, `User`, `Team`, `Workspace`,
  `RuntimeOverride`). Same casing as the REST API.
- **`scope_id`** — opaque identifier within the scope (e.g. the user
  email, the workspace id). Nullable for scopes that don't take an id.
- **`updated_by`** — actor that made the change. Echoes whatever the
  API surface put into `SettingAssignment.UpdatedBy`.

Headers (set by `NatsMessageBus`, parsed and re-emitted by every
ADR-0001-compliant consumer):

| Header | Meaning |
|---|---|
| `Nats-Msg-Id` | UUID. Use for consumer-side dedup. |
| `Andy-Correlation-Id` | UUID. Trace ID for this causation chain. |
| `Andy-Causation-Id` | UUID or empty. The `msg-id` of the message that caused this one. |
| `Andy-Generation` | Integer hop counter. Drop on `> 10`. |

## Burst handling

Configuration edits are bursty: a tenant admin saves a dozen keys at
once, or a CI job applies a batch import. The publisher side is built
for that:

- `Messaging:Outbox:BatchSize` defaults to **200** rows per drain (vs.
  100 in andy-issues / andy-tasks).
- The dispatcher loops immediately when a drain returns a non-empty
  batch and only sleeps on an empty poll, so a backlog of N rows
  finishes in ⌈N / 200⌉ drains with no per-row delay.
- The transactional outbox + JetStream backing combine to give
  at-least-once delivery — consumers must be idempotent (see below).

If a consumer can't keep up under a burst, scale by adding parallel
consumers with distinct durable names, or pause the consumer on the
NATS server (`nats consumer pause`) and re-enable later. **Do not gate
consumers behind configuration flags** — per AK4 that's forbidden, and
operational pause is the right tool.

## Consumer onboarding

1. **Pick a durable name.** Format: `<your-service>.<purpose>` —
   e.g. `mcp-proxy.config-watcher`. JetStream keys offsets by this
   name, so it must be stable across restarts.
2. **Dedupe on `msg-id`.** Use a DB-backed `seen_messages` table per
   AK3. In-memory ring buffers are insufficient: they don't survive
   restarts and don't dedupe across replicas.
3. **Implement the handler.** Reference the
   `ConfigEventConsumerBase` template in
   `Andy.Settings.Infrastructure/Messaging/Consumers/`. Your concrete
   class:
   - Sets `SubjectFilter = "andy.settings.events.config.<your-app>.>"`.
   - Sets `DurableName` to the value from step 1.
   - Implements `IsDuplicateAsync` against your `seen_messages` table.
   - Implements `HandleAsync(ConfigChangedPayload, ...)` — typically
     "invalidate my cache, GET the new value from
     `GET /settings/api/effective/resolve?key=...`".
4. **Register as a hosted service** in your `Program.cs`. Per AK4 do
   **not** gate it on a feature flag.
5. **Verify with `nats sub`.** Run
   `nats sub 'andy.settings.events.config.>'` and trigger a write
   through the API to see the message live before wiring your handler.

## Failure modes

- **Generation overflow (≥ 11 hops).** `NatsMessageBus` drops the
  message, emits the AK6 counter
  `rivoli_nats_generation_limit_breach_total{service="andy-settings",direction,subject_root}`,
  and logs an `ERROR` with the full causation chain. Baseline is zero;
  any non-zero rate is a bug.
- **Malformed headers.** Dropped + DLQ'd + acked to prevent redelivery
  loops. The original `NatsHeaders` are preserved on the DLQ so
  post-mortem causation tracing still works.
- **DLQ subject.** `andy.settings.dlq.<original-subject>`. Always
  preserves the original headers; the payload is the raw bytes.
- **Publish failure.** Outbox row stays pending. `AttemptCount`
  increments, `LastError` records the exception. The dispatcher
  exponentially backs off (`BackoffBase * 2^(n-1)`, capped at
  `BackoffMax`) so a poison message does not spin the worker at full
  speed.

## Operational invariants

andy-settings on-ramped to ADR 0001 under Epic AL, so the AK
invariants from the 2026-04-24 amendment apply:

1. **AK1 — fail-loud on non-Nats provider.** Program.cs throws at boot
   in any non-Development environment if `Messaging:Provider` ≠ `Nats`.
2. **AK2 — poll interval ≤ 2s.** `OutboxDispatcher` warns at boot when
   the configured `Messaging:Outbox:PollInterval` exceeds the cap.
3. **AK3 — persistent dedup.** Consumers MUST use a `SeenMessages`
   table.
4. **AK4 — consumers always on.** No per-consumer feature flags.
5. **AK5 — stream retention codified.** `ANDY_DOMAIN`, 90 days, logged
   at startup.
6. **AK6 — generation-limit metric.** Alerts fire on the first
   non-zero data point.

## Configuration

`appsettings.json` (Docker / Embedded / Production):

```jsonc
"Messaging": {
  "Provider": "Nats",
  "Nats": {
    "Url": "nats://localhost:4222",
    "StreamName": "ANDY_DOMAIN",
    "StreamSubjects": [ "andy.settings.events.>" ],
    "DlqPrefix": "andy.settings.dlq"
  },
  "Outbox": {
    "PollInterval": "00:00:01",
    "BatchSize": 200,
    "BackoffBase": "00:00:01",
    "BackoffMax": "00:05:00"
  },
  "SeenMessages": {
    "PurgeInterval": "00:15:00",
    "PurgeBatchSize": 1000,
    "DefaultTtl": "90.00:00:00"
  }
}
```

`appsettings.Development.json` overrides `Messaging:Provider` to
`InMemory` so `dotnet run` does not require a local NATS server. The
in-memory bus is identical in behavior to the JetStream one for the
contract enforced by `IMessageBus`; it just doesn't persist or cross
processes.
