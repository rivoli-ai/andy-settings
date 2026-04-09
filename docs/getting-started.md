# Getting Started

## Prerequisites

- .NET 8 SDK
- Node.js 20+ (for Angular client)
- Docker and Docker Compose (for containerized development)

## Quick Start with Docker Compose

```bash
git clone https://github.com/rivoli-ai/andy-settings.git
cd andy-settings
docker compose up --build
```

Services will be available at:

- **API**: https://localhost:5300
- **Swagger**: https://localhost:5300/swagger
- **PostgreSQL**: localhost:5438

## Native Local Development

### Backend

```bash
# Build the solution
dotnet build

# Run the API (uses SQLite by default in development)
dotnet run --project src/Andy.Settings.Api
```

The API will start at https://localhost:5300.

### Frontend

```bash
cd client
npm install
npm start
```

The Angular dev server starts at https://localhost:4200 with proxy to the backend API.

### CLI

```bash
# Run directly
dotnet run --project tools/Andy.Settings.Cli -- --help

# Or install as global tool
dotnet pack tools/Andy.Settings.Cli
dotnet tool install --global --add-source tools/Andy.Settings.Cli/nupkg Andy.Settings.Cli
andy-settings --help
```

## Database Provider

By default, the API uses SQLite for local development. To use PostgreSQL:

```bash
# Via environment variable
Database__Provider=PostgreSql dotnet run --project src/Andy.Settings.Api

# Or edit appsettings.Development.json
```

## Running Tests

```bash
# All tests
dotnet test

# Unit tests only
dotnet test tests/Andy.Settings.Tests.Unit

# Integration tests only
dotnet test tests/Andy.Settings.Tests.Integration

# Frontend tests
cd client && npx ng test --watch=false --browsers=ChromeHeadless
```

## Project Structure

```
andy-settings/
├── src/
│   ├── Andy.Settings.Domain/           # Entities, enums
│   ├── Andy.Settings.Application/      # Interfaces, DTOs, options
│   ├── Andy.Settings.Infrastructure/   # EF Core, repositories, services
│   ├── Andy.Settings.Api/              # REST + MCP + Swagger
│   └── Andy.Settings.Shared/           # Shared types
├── tools/
│   └── Andy.Settings.Cli/             # CLI tool
├── tests/
│   ├── Andy.Settings.Tests.Unit/
│   └── Andy.Settings.Tests.Integration/
├── client/                            # Angular 18 SPA
├── examples/                          # Multi-language examples
├── docs/                              # MkDocs documentation
└── docker-compose.yml
```

## Next Steps

- Read the [Architecture](architecture.md) guide to understand the domain model
- Browse the [Features](features.md) catalog
- Review the [Stories](stories.md) for the implementation backlog
- Check the [Examples](examples.md) for API usage in your language
