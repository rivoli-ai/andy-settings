---
title: "Definitions"
order: 2
tags: [settings, schema]
---

# Definitions

A **definition** is the schema for a setting. It answers the question: *What is this setting?*

## Fields

| Field | Description |
|-------|-------------|
| `key` | Unique identifier for the setting (e.g., `notifications.enabled`). |
| `applicationCode` | The application that owns this setting. |
| `displayName` | Short human-readable name. |
| `dataType` | One of the data types listed below. |
| `defaultValueJson` | The fallback value when no assignment exists, as **JSON** — so a string default is `"\"light\""`, not `light`. Must be null for secret definitions. |
| `validationJson` | Optional JSON validation schema. See below. |
| `uiSchemaJson` | Optional UI hints for rendering an editor. |
| `isSecret` | When `true`, values are encrypted at rest and only readable with `secret:read`. |
| `allowedScopesJson` | Optional JSON array restricting which scopes may hold a value, e.g. `["Machine","User"]`. |
| `tagsJson` | Optional JSON array of tags. |
| `isDeprecated` | Marks a setting as retired without deleting it or its values. |
| `description` | Human-readable explanation of the setting's purpose. |

## Data Types

| Type | Accepts |
|------|---------|
| `String` | A JSON string. |
| `Integer` | A whole number. |
| `Decimal` | A number, fractional allowed. |
| `Boolean` | `true` or `false`. |
| `Enum` | A JSON string, normally constrained by an `enum` list in `validationJson`. |
| `Duration` | A JSON string parseable as a timespan, e.g. `"00:05:00"`. |
| `Uri` | A JSON string containing an absolute URI. |
| `Json` | Any JSON value. |
| `StringList` | A JSON array whose items are all strings. |
| `Secret` | An encrypted value. Written and read through the secrets endpoints, never through the values API. |

## Validation

`validationJson` is a JSON object supporting these keywords, applied after the data-type check:

- `enum` — the value must be one of the listed values
- `minLength` / `maxLength` — for string values
- `pattern` — regular expression, for string values
- `minimum` / `maximum` — for numeric values
- `minItems` / `maxItems` — for array values

## Creating Definitions

Use `POST /api/definitions`, the Andy CLI, or — preferably — declare the setting in the owning service's `config/registration.json`.

Definitions declared in a registration manifest are **owned by that manifest**: their schema is reconciled on every startup, so editing those fields through the API is overwritten on the next boot. Change the manifest instead. Stored values are never affected by reconciliation.

## Immutability

Definition keys are immutable after creation. If you need to rename a setting, create a new definition and migrate values over time.

A definition that already has stored values cannot switch between secret and ordinary storage.

Deleting a definition **cascades**: every assignment and every encrypted secret stored under that key is removed with it.

## Best Practices

- Use dot-notation namespaces (e.g., `feature.subsystem.key`) to avoid collisions.
- Always provide a `description` so that consumers understand intent.
- Mark sensitive settings with `isSecret: true` to ensure encryption and access control.
- Prefer `isDeprecated` over deletion when retiring a setting, so stored values survive.
