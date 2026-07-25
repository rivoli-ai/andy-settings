---
title: "API Access"
order: 5
tags: [api, integrations]
---

# API Access

Andy Settings exposes a REST API for managing definitions, values, secrets, and effective-value resolution. It is designed to be consumed by backend services, CLIs, and the Conductor desktop app.

## Authentication

All endpoints except `/api/help`, `/health` and the OpenAPI document require a valid bearer token. Individual endpoints additionally require an RBAC permission — `definition:read`, `value:write`, `secret:read` and so on.

## Key Endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /api/definitions` | List setting definitions (paginated). |
| `GET /api/definitions/{key}` | Get one definition. |
| `POST /api/definitions` | Create a definition. |
| `PUT /api/definitions/{key}` | Update a definition. |
| `DELETE /api/definitions/{key}` | Delete a definition **and every value and secret stored under it**. |
| `GET /api/values` | List values, optionally filtered by definition or scope (paginated). |
| `POST /api/values` | Set a value for a definition within a scope. |
| `POST /api/values/bulk` | Set many values in one transaction. |
| `DELETE /api/values/{id}` | Delete one scoped assignment. |
| `POST /api/effective/resolve` | Resolve the effective value for a context. |
| `POST /api/effective/resolve-batch` | Resolve several keys at once. |
| `POST /api/effective/explain` | Resolve, plus the full source chain showing which scope won. |
| `POST /api/secrets/{key}` | Set an encrypted secret at a scope. |
| `GET /api/secrets/{key}?scopeType=&scopeId=` | Read a decrypted secret (requires `secret:read`). |
| `POST /api/secrets/{key}/rotate` | Replace a secret's value. |
| `DELETE /api/secrets/{key}?scopeType=&scopeId=` | Delete one scope's secret; `?allScopes=true` deletes them all. |
| `GET /api/audit` | Query the audit trail. |
| `GET /api/export`, `POST /api/import` | Export and import definitions and values. |
| `GET /api/help/topics`, `GET /api/help/topics/{slug}` | This help content. |

The effective-value endpoints are **POST**, not GET: the resolution context (user, team, workspace, application, service) is sent as a JSON body rather than query parameters.

## Pagination

List endpoints take `page` (1-based) and `pageSize`. `pageSize` is capped at **500** and defaults to 25; `page` below 1 is treated as 1. `totalCount` in the response always reports the true total, so a client can tell when more pages remain.

## Integrations

- **Conductor**: The desktop app embeds this service and exposes it to local MCP servers.
- **CLI**: The `andy-settings` command reads and writes values from shell scripts and CI/CD pipelines. It authenticates with a bearer token from the `ANDY_SETTINGS_TOKEN` environment variable, and returns a non-zero exit code on failure.
- **MCP**: Model Context Protocol tools are served at `/mcp` for agents that need to read or change settings.

## Rate Limits

The service does **not** currently enforce rate limits. Callers should apply their own backoff, and operators who need throttling should place it in front of the service — for example at the proxy or gateway layer.
