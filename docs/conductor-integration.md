# Conductor Integration Guide

Andy Settings runs as an embedded .NET service inside the Conductor macOS app.

## Service Configuration

### SettingsServiceConfig

Add to `Conductor/Core/ServiceHost/Services/`:

```swift
struct SettingsServiceConfig: EmbeddedServiceConfig {
    let name = "andy-settings"
    let executableName = "Andy.Settings.Api"
    let port = 9111
    let proxyPrefix = "/settings"
    let healthPath = "/health"
    let dependsOn: [String] = ["andy-auth", "andy-rbac"]

    func environment() -> [String: String] {
        [
            "ASPNETCORE_ENVIRONMENT": "Development",
            "ASPNETCORE_URLS": "http://+:\(port)",
            "Database__Provider": "Sqlite",
            "ConnectionStrings__DefaultConnection": "Data Source=\(EmbeddedDatabase.dbPath)/andy-settings.sqlite",
            "AndyAuth__Authority": "http://localhost:9101",
            "AndyAuth__Audience": "urn:andy-settings-api",
            "Rbac__ApiBaseUrl": "http://localhost:9102",
            "Rbac__ApplicationCode": "settings",
            "CONDUCTOR_EMBEDDED": "true"
        ]
    }
}
```

### ServiceOrchestrator

Register in `ServiceOrchestrator.services`:

```swift
// In ServiceOrchestrator launch order (after auth + rbac):
let settingsConfig = SettingsServiceConfig()
try await launch(settingsConfig)
```

### UnifiedProxy

Add route to `UnifiedProxy.routeTable`:

```swift
"/settings": 9111
```

## Swift Client Integration

### SettingsServiceProtocol

```swift
protocol SettingsServiceProtocol: Sendable {
    func getDefinitions(applicationCode: String?) async throws -> PagedResult<SettingDefinition>
    func resolve(key: String, context: ResolutionContext) async throws -> ResolvedSetting
    func setValue(key: String, scopeType: String, scopeId: String?, value: String) async throws
    func explain(key: String, context: ResolutionContext) async throws -> ResolvedSetting
}
```

### Live Implementation

```swift
@Observable
final class SettingsService: SettingsServiceProtocol {
    private let client: APIClient

    init(client: APIClient) {
        self.client = client
    }

    func getDefinitions(applicationCode: String? = nil) async throws -> PagedResult<SettingDefinition> {
        var endpoint = APIEndpoint.get("/settings/api/definitions")
        if let app = applicationCode {
            endpoint = endpoint.queryItems(["applicationCode": app])
        }
        return try await client.send(endpoint)
    }

    func resolve(key: String, context: ResolutionContext) async throws -> ResolvedSetting {
        let body = ResolveRequest(key: key, context: context)
        return try await client.send(.post("/settings/api/effective/resolve", body: body))
    }

    func setValue(key: String, scopeType: String, scopeId: String?, value: String) async throws {
        let body = SetValueRequest(definitionKey: key, scopeType: scopeType, scopeId: scopeId, valueJson: value)
        try await client.send(.post("/settings/api/values", body: body))
    }

    func explain(key: String, context: ResolutionContext) async throws -> ResolvedSetting {
        let body = ResolveRequest(key: key, context: context)
        return try await client.send(.post("/settings/api/effective/explain", body: body))
    }
}
```

### Mock for Previews/Tests

```swift
final class MockSettingsService: SettingsServiceProtocol {
    func getDefinitions(applicationCode: String?) async throws -> PagedResult<SettingDefinition> {
        PagedResult(items: [.preview], totalCount: 1, page: 1, pageSize: 25)
    }
    // ... other methods return preview data
}
```

## ActionBus Integration

### UpdateSettingsAction

```swift
struct UpdateSettingsAction: Action {
    static let id = "settings.update"
    static let displayName = "Update Setting"
    static let description = "Update a configuration setting value"
    static let category: ActionCategory = .mutate
    static let requiredPermissions = ["settings:value:write"]

    struct Params: Codable, Sendable {
        let key: String
        let scopeType: String
        let scopeId: String?
        let value: String
    }

    struct Result: Codable, Sendable {
        let success: Bool
    }

    func execute(params: Params, context: ActionContext) async throws -> Result {
        let service = context.resolve(SettingsServiceProtocol.self)
        try await service.setValue(key: params.key, scopeType: params.scopeType, scopeId: params.scopeId, value: params.value)
        return Result(success: true)
    }
}
```

Register in `ActionRegistry`:

```swift
registry.register(UpdateSettingsAction.self)
```

## Database

SQLite database at:
```
~/Library/Application Support/ai.rivoli.conductor/db/andy-settings.sqlite
```

Auto-migrated on first startup. Seeded with 25 definitions for all Andy services.

## Ports

| Service | Port | Proxy Path |
|---------|------|------------|
| andy-auth | 9101 | /auth |
| andy-rbac | 9102 | /rbac |
| andy-containers | 9103 | /containers |
| andy-docs | 9105 | /docs |
| andy-code-index | 9107 | /code-index |
| andy-issues | 9108 | /issues |
| andy-agents | 9109 | /agents |
| andy-tasks | 9110 | /tasks |
| **andy-settings** | **9111** | **/settings** |
| andy-models | 9112 | /models |
| andy-policies | 9113 | /policies |
| andy-mcp-proxy | 9114 | /mcp-proxy |
