# Andy Settings API Examples

Code samples showing how to consume the Andy Settings REST API from various languages.

## Prerequisites

- A running Andy Settings API (default: `https://localhost:5300`)
- A JWT Bearer token (set `ANDY_SETTINGS_TOKEN` env var)

## Quick Start

```bash
# Start the API (SQLite mode, no auth)
cd ../src/Andy.Settings.Api
AndyAuth__Authority="" Database__Provider=Sqlite dotnet run

# Set the token (not needed in dev mode without auth)
export ANDY_SETTINGS_TOKEN=""
export ANDY_SETTINGS_URL="http://localhost:5300"
```

## Examples

| Language | Directory | Run |
|----------|-----------|-----|
| C# | `csharp/` | `dotnet run` |
| Python | `python/` | `python example.py` |
| JavaScript | `javascript/` | `node example.mjs` |
| Go | `go/` | `go run main.go` |
| Rust | `rust/` | `cargo run` |
| PowerShell | `powershell/` | `pwsh example.ps1` |

Each example demonstrates 5 operations:
1. List setting definitions
2. Resolve effective value
3. Set a scoped value
4. Explain resolution chain
5. Export settings
