# Examples

The `examples/` directory contains sample code demonstrating how to consume the Andy Settings API and MCP tools from various languages.

## Languages

| Language | Directory | Description |
|----------|-----------|-------------|
| C# (.NET) | `examples/csharp/` | HttpClient-based REST client |
| Python | `examples/python/` | requests-based REST client |
| JavaScript | `examples/javascript/` | fetch-based REST client + MCP |
| Go | `examples/go/` | net/http REST client |
| Rust | `examples/rust/` | reqwest REST client |
| PowerShell | `examples/powershell/` | Invoke-RestMethod scripts |

## Common Operations

Each example demonstrates:

1. **List definitions** -- browse available setting definitions
2. **Resolve effective value** -- get the effective value for a key in context
3. **Set a value** -- create or update a scoped setting value
4. **Explain resolution** -- understand why a value is active
5. **Export settings** -- bulk export as JSON

## Authentication

All examples assume a JWT Bearer token. Obtain one via:

```bash
# Using the CLI
andy-settings auth login

# Or via Andy Auth directly
curl -X POST https://localhost:5001/connect/token \
  -d "grant_type=client_credentials&client_id=..." \
  -H "Content-Type: application/x-www-form-urlencoded"
```

## API Base URL

Default: `https://localhost:5300`

In Conductor: `http://localhost:9100/settings`
