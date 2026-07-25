---
title: "Values"
order: 3
tags: [settings, configuration]
---

# Values

A **value** is the concrete assignment of a setting within a specific scope. It answers the question: *What is this setting set to right now?*

## Value vs. Definition

- **Definition** = the blueprint.
- **Value** = the instance.

You can have many values for a single definition, each tied to a different scope. When resolving the effective value for a context, the **highest-precedence** matching scope wins — see [Scopes](scopes.md) for the ordering.

## Secrets Are Stored Separately

Secret-backed settings do **not** flow through the values API at all. A definition marked `isSecret` (or typed `Secret`) rejects any write to `POST /api/values`; its values live in separate encrypted storage, addressed by definition key plus scope.

This means:

- Secret values are encrypted at rest and are only returned to callers holding the `secret:read` permission.
- Every decrypt is recorded in the audit trail, with the actor and scope but never the value.
- Reading, writing, rotating and deleting secrets is done through `/api/secrets/{definitionKey}`.
- Resolving a secret-backed setting through the ordinary effective-value endpoints returns no value — it reports that the setting is secret instead.

Rotation replaces the stored encrypted value for one scope, so consumers pick up the new value the next time they read it.

## Setting Values

Use `POST /api/values` to set a value. Include the definition key, scope type, scope ID, and the value as **JSON** (`"\"podman\""` for a string, `42` for a number).

The service validates the value against the definition's `dataType` and its `validationJson` schema before persisting, and rejects scopes the definition does not allow.

Supply the current `etag` to get optimistic concurrency: if someone else changed the value in the meantime, the write is rejected with `409 Conflict` rather than overwriting them.

## Deleting Values

`DELETE /api/values/{id}` removes a single scoped assignment. Deleting a value does not affect other scopes, and the setting falls back to the next-highest scope that has one — or to the definition default.

## Bulk Operations

You can update many values at once using `POST /api/values/bulk`. This is useful when onboarding a new workspace or applying a configuration template. The batch is applied in a single transaction: if one value fails validation, none of them are written.
