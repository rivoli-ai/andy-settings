---
title: "Getting Started"
order: 1
tags: [onboarding, quickstart]
---

# Getting Started with Andy Settings

Andy Settings is the centralized settings registry for the Andy platform. It stores **definitions** (what a setting is) and **values** (what a setting is set to) across different **scopes**.

## Core Concepts

- **Definition**: Describes a setting—its key, data type, default value, validation rules, and whether it is secret.
- **Value**: The concrete assignment of a setting to a specific scope (e.g., a workspace or environment).
- **Scope**: The context in which a value applies. Scopes are hierarchical, so a value set at a broader scope can be overridden by a narrower one.

## Quick Start

1. Create a **definition** for each setting your application needs.
2. Assign **values** to the definition within the appropriate scope.
3. Query the **effective value** API to resolve the final value for a given context.

## Why Centralized?

By keeping all settings in one place, you avoid configuration drift, gain auditability, and can manage secrets safely using encrypted storage and secret references.
