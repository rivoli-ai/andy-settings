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

You can have many values for a single definition, each tied to a different scope. When resolving the effective value for a context, the narrowest matching scope wins.

## Secret References

Values of type `Secret` do not store plaintext. Instead, they store a **secret reference**—a pointer to an encrypted secret in the secret vault. At runtime, the reference is resolved and the actual secret is injected into the consuming service.

This means:
- Secrets are never exposed in APIs unless explicitly decrypted.
- Rotation is centralized: update the secret once, and all references automatically resolve to the new value.

## Setting Values

Use `POST /api/values` to set a value. Include the definition key, scope type, scope ID, and the value itself. The service validates the value against the definition’s `DataType` and `ValidationRegex` before persisting.

## Bulk Operations

You can update many values at once using `POST /api/values/bulk`. This is useful when onboarding a new workspace or applying a configuration template.
