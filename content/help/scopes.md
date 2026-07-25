---
title: "Scopes"
order: 4
tags: [settings, hierarchy]
---

# Scopes

Scopes provide the context in which a setting value applies. They form a hierarchy, allowing broad defaults to be overridden by more specific assignments.

## Scope Hierarchy

Seven levels, from lowest precedence to highest. A value set at a higher level wins over one set at a lower level.

| Scope | Scope ID | Applies to |
|-------|----------|------------|
| `Machine` | *(none)* | The whole installation. The broadest level, and the only one that takes no scope id. |
| `Application` | application code | One application, e.g. `andy-tasks`. |
| `Service` | service code | One service within an application. |
| `User` | user id | One user. |
| `Team` | team id | One team. |
| `Workspace` | workspace id | One workspace. |
| `RuntimeOverride` | *(context-dependent)* | Highest precedence. Reserved for temporary overrides that must beat everything else. |

Every scope except `Machine` requires a scope id. A definition can restrict which of these levels it accepts via its allowed-scopes metadata.

## Effective Value Resolution

When a client resolves a setting, Andy Settings evaluates every scope present in the request context in precedence order and takes the **highest-precedence match**. If no scope has an assignment, the definition's default value is used.

`POST /api/effective/explain` returns the full source chain — every scope that had a value, which one won, and whether the result came from the definition default. Exactly one entry in the chain is marked as the winner.

## Example

Given a request context with `userId = u-42` and `workspaceId = ws-123`:

| Scope | Scope ID | Value |
|-------|----------|-------|
| *(definition default)* | — | `debug: false` |
| `Machine` | — | `debug: false` |
| `User` | `u-42` | `debug: true` |

The effective value of `debug` for user `u-42` is `true`, because `User` outranks `Machine`. For any other user it is `false`.

Note that `Workspace` outranks `User`: had a workspace-level value existed for `ws-123`, it would have won instead.

## Best Practices

- Set sensible defaults on the definition itself, and use `Machine` for installation-wide policy.
- Override only when necessary, at the narrowest scope that expresses the intent.
- Reserve `RuntimeOverride` for temporary situations — because it beats every other scope, a forgotten override is hard to diagnose.
- Use `explain` when a value is not what you expect; it shows every scope considered and which one won.
