---
title: Andy Settings Overview
slug: andy-settings-overview
order: 1
tags: [settings, configuration, secrets]
---

# Andy Settings Overview

Andy Settings is the centralized settings registry for the entire Andy ecosystem. It owns setting *definitions* (read from every service's `registration.json` at startup), setting *values* (per-installation), and the secret store that backs every service's credentials.

## What it does

- Seeds setting definitions from each sibling service's `registration.json` `settings.definitions` block on startup.
- Stores per-installation setting values and serves them via the `ISetting` API every Andy service uses.
- Acts as the central secret store — PATs, API keys, and other shared credentials live here exactly once. Consuming services hold references (e.g., `${secret:github.token}`), not the raw value.
- Publishes change events on NATS so dependent services refresh in seconds.

## Key concepts

- **Definition vs value** — definitions are schema (name, data type, default); values are user/installation-specific.
- **Secret reference** — a `${secret:<key>}` placeholder a service resolves through `ISecretStore`. The actual secret never leaves Settings.
- **Setting scope** — global, per-organization, or per-user. Most settings are global; tokens are per-user.

## Where it fits

Settings is a hard dependency for every other Andy service — without it, services can't load their configuration. Conductor reads provider keys, GitHub PATs, and feature toggles through it.

## Configuration

Self-bootstrapped: Settings reads its own `registration.json` and seeds itself first. Connection strings come from environment variables baked into the Conductor service bundle.

## Troubleshooting

- **A service can't find its config** — Settings is unreachable or hasn't finished seeding. Check `andy-settings.log` for `Seeded N definitions from M services`.
- **"Secret not found" errors** — the secret ref points at a key that was never written. Set it via **Settings → Catalogs → Services → Andy Settings → Secrets** or through the appropriate provider's UI.
- **Settings changes not reflected** — NATS isn't running or the consumer isn't subscribed. Restart the consuming service; values are eagerly refreshed on next request.
