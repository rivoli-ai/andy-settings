---
title: "Scopes"
order: 4
tags: [settings, hierarchy]
---

# Scopes

Scopes provide the context in which a setting value applies. They form a hierarchy, allowing broad defaults to be overridden by more specific assignments.

## Scope Hierarchy

From broadest to narrowest:

1. **Global** — applies to the entire platform.
2. **Organization** — applies to a single organization.
3. **Workspace** — applies to a single workspace.
4. **Environment** — applies to a specific deployment environment (e.g., `staging`, `production`).

## Effective Value Resolution

When a client requests the value of a setting, Andy Settings walks the scope hierarchy from narrowest to broadest and returns the first match. If no value is found, the definition’s `DefaultValue` is returned.

## Example

| Scope Type | Scope ID | Value |
|------------|----------|-------|
| Global | — | `debug: false` |
| Workspace | `ws-123` | `debug: true` |

For workspace `ws-123`, the effective value of `debug` is `true`. For any other workspace, it is `false`.

## Best Practices

- Set sensible defaults at the **Global** scope.
- Override only when necessary at narrower scopes.
- Use **Environment** scopes for deployment-specific configuration like feature flags or endpoint URLs.
