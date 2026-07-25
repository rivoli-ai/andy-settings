---
title: Andy Settings Overview
slug: andy-settings-overview
order: 1
tags: [settings, configuration, secrets]
---

# Andy Settings Overview

Andy Settings is the centralized settings registry for the entire Andy ecosystem. It owns setting *definitions* (read from every service's `registration.json` at startup), setting *values* (scoped per installation, user, team and more), and the encrypted secret store that backs every service's credentials.

## What it does

- Reconciles setting definitions from each sibling service's `registration.json` `settings.definitions` block on startup — not just on first run, so a manifest change rolls out on the next boot.
- Stores scoped setting values and serves them over a REST API. .NET consumers use the `Andy.Settings.Client` package (`IAndySettingsClient`), which resolves values and caches a snapshot.
- Acts as the central secret store — PATs, API keys, and other shared credentials live here exactly once, encrypted at rest. Consuming services read them from the secrets API and need the `secret:read` permission to do so.
- Publishes change events on NATS so dependent services refresh in seconds.

## Key concepts

- **Definition vs value** — definitions are schema (key, data type, default, validation); values are the concrete assignments.
- **Secrets are stored apart from values** — a secret-backed setting is rejected by the ordinary values API. Its value lives in encrypted storage keyed by definition and scope, is only returned to callers holding `secret:read`, and every decrypt is recorded in the audit trail.
- **Setting scope** — seven levels, lowest precedence first: `Machine`, `Application`, `Service`, `User`, `Team`, `Workspace`, `RuntimeOverride`. The highest-precedence scope with a value wins; if none has one, the definition's default is used. All scopes except `Machine` take a scope id.

## Where it fits

Settings is a hard dependency for every other Andy service — without it, services can't load their configuration. Conductor reads provider keys, GitHub PATs, and feature toggles through it.

## Configuration

Self-bootstrapped: Settings reads its own `registration.json` and seeds itself first. Connection strings come from environment variables baked into the Conductor service bundle. Schema migrations and definition seeding run on every startup, in every environment.

## Troubleshooting

- **A service can't find its config** — Settings is unreachable or hasn't finished seeding. Check `andy-settings.log` for `Definition catalog reconciled: N added, M updated, ...`, which is logged once startup reconciliation completes.
- **"Secret not found" errors** — no secret has been written for that definition at the scope being requested. Note that secrets are per-scope: a value set for one user is not visible to another. Set it via **Settings → Catalogs → Services → Andy Settings → Secrets** or through the appropriate provider's UI.
- **A secret reads as missing right after a restart** — if the Data Protection key ring changed, existing ciphertext can no longer be decrypted and is reported as absent rather than failing the caller. The log records `[SECRET-UNDECRYPTABLE]`. Re-set the secret to repair it.
- **Settings changes not reflected** — NATS isn't running or the consumer isn't subscribed. Restart the consuming service; values are refreshed on the next read.
