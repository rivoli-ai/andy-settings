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
| `Key` | Unique identifier for the setting (e.g., `notifications.enabled`). |
| `DataType` | One of `String`, `Number`, `Boolean`, `Json`, or `Secret`. |
| `DefaultValue` | The fallback value when no explicit assignment exists. |
| `ValidationRegex` | Optional regex to validate user-provided values. |
| `IsSecret` | When `true`, values are encrypted at rest and masked in logs. |
| `Description` | Human-readable explanation of the setting’s purpose. |

## Creating Definitions

Use the `POST /api/definitions` endpoint or the Andy CLI to register new definitions. Once created, a definition can be referenced by any scope in the platform.

## Immutability

Definition keys are immutable after creation. If you need to rename a setting, create a new definition and migrate values over time.

## Best Practices

- Use dot-notation namespaces (e.g., `feature.subsystem.key`) to avoid collisions.
- Always provide a `Description` so that consumers understand intent.
- Mark sensitive settings with `IsSecret: true` to ensure encryption and access control.
