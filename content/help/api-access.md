---
title: "API Access"
order: 5
tags: [api, integrations]
---

# API Access

Andy Settings exposes a REST API for managing definitions, values, and effective-value resolution. It is designed to be consumed by backend services, CLIs, and the Conductor desktop app.

## Authentication

All endpoints (except `/api/help`) require a valid bearer token. The API accepts tokens from the platform identity provider and validates them against the required scopes.

## Key Endpoints

| Endpoint | Description |
|----------|-------------|
| `GET /api/definitions` | List all setting definitions (paginated). |
| `POST /api/definitions` | Create a new definition. |
| `GET /api/values` | List values, optionally filtered by definition or scope. |
| `POST /api/values` | Set a value for a definition within a scope. |
| `GET /api/effective?key=...&scopeType=...&scopeId=...` | Resolve the effective value for a given context. |
| `GET /api/help/topics` | List all help topics. |
| `GET /api/help/topics/{slug}` | Retrieve a single help topic. |

## Integrations

- **Conductor**: The desktop app syncs settings in real time and exposes them to local MCP servers.
- **CLI**: The `andy settings` command group lets you read and write values from shell scripts and CI/CD pipelines.
- **MCP**: Model Context Protocol servers can query effective values to adapt behavior dynamically.

## Rate Limits

Read endpoints are limited to 1,000 requests per minute per client. Write endpoints are limited to 100 requests per minute to protect the database.
