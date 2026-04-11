using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Andy.Settings.Api.Data;

public class DataSeeder
{
    private readonly SettingsDbContext _db;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(SettingsDbContext db, ILogger<DataSeeder> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var definitions = BuildSeedDefinitions();
        var existingKeys = await _db.SettingDefinitions
            .Select(d => d.Key)
            .ToListAsync(ct);
        var toAdd = definitions
            .Where(d => !existingKeys.Contains(d.Key))
            .ToList();

        if (toAdd.Count == 0)
        {
            _logger.LogDebug("All setting definitions already seeded");
            return;
        }

        _logger.LogInformation("Seeding {Count} new setting definitions...", toAdd.Count);
        _db.SettingDefinitions.AddRange(toAdd);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Seeded {Count} new setting definitions", toAdd.Count);
    }

    private static List<SettingDefinition> BuildSeedDefinitions()
    {
        var now = DateTimeOffset.UtcNow;
        var defs = new List<SettingDefinition>();

        void Add(string key, string app, string name, string? desc, string? category,
            SettingDataType type, string? defaultValue = null, bool isSecret = false,
            string? allowedScopes = null)
        {
            defs.Add(new SettingDefinition
            {
                Id = Guid.NewGuid(),
                Key = key,
                ApplicationCode = app,
                DisplayName = name,
                Description = desc,
                Category = category,
                DataType = type,
                DefaultValueJson = defaultValue,
                IsSecret = isSecret,
                AllowedScopesJson = allowedScopes ?? "[\"Machine\",\"Application\",\"User\"]",
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        // andy.auth
        Add("andy.auth.authority", "auth", "Auth Authority URL", "OIDC authority URL", "General",
            SettingDataType.Uri, "\"https://localhost:5001\"");
        Add("andy.auth.audience", "auth", "Auth Audience", "Expected JWT audience", "General",
            SettingDataType.String, "\"urn:andy-settings-api\"");
        Add("andy.auth.requireHttpsMetadata", "auth", "Require HTTPS Metadata", "Require HTTPS for OIDC metadata", "Security",
            SettingDataType.Boolean, "false");
        Add("andy.auth.tokenLifetimeMinutes", "auth", "Token Lifetime", "Access token lifetime in minutes", "Security",
            SettingDataType.Integer, "60");

        // andy.rbac
        Add("andy.rbac.apiBaseUrl", "rbac", "RBAC API URL", "Andy RBAC API base URL", "General",
            SettingDataType.Uri, "\"https://localhost:5003\"");
        Add("andy.rbac.applicationCode", "rbac", "Application Code", "RBAC application identifier", "General",
            SettingDataType.String, "\"settings\"");
        Add("andy.rbac.cacheTtlSeconds", "rbac", "Cache TTL", "Permission cache time-to-live in seconds", "Performance",
            SettingDataType.Integer, "300");

        // andy.containers
        Add("andy.containers.defaultProvider", "containers", "Default Provider", "Default infrastructure provider", "General",
            SettingDataType.String, "\"docker\"",
            allowedScopes: "[\"Machine\",\"Application\",\"User\",\"Team\"]");
        Add("andy.containers.maxCpuCores", "containers", "Max CPU Cores", "Maximum CPU cores per container", "Resources",
            SettingDataType.Integer, "4");
        Add("andy.containers.maxMemoryMb", "containers", "Max Memory (MB)", "Maximum memory per container in MB", "Resources",
            SettingDataType.Integer, "8192");
        Add("andy.containers.sshTimeoutSeconds", "containers", "SSH Timeout", "SSH connection timeout in seconds", "Connectivity",
            SettingDataType.Integer, "30");

        // andy.codeindex
        Add("andy.codeindex.embedding.provider", "codeindex", "Embedding Provider", "Embedding API provider", "Embedding",
            SettingDataType.String, "\"openai\"",
            allowedScopes: "[\"Machine\",\"Application\",\"User\",\"Team\"]");
        Add("andy.codeindex.embedding.model", "codeindex", "Embedding Model", "Model name for embeddings", "Embedding",
            SettingDataType.String, "\"text-embedding-3-small\"");
        Add("andy.codeindex.embedding.dimensions", "codeindex", "Embedding Dimensions", "Vector dimensions", "Embedding",
            SettingDataType.Integer, "1536");
        Add("andy.codeindex.embedding.apiKey", "codeindex", "Embedding API Key", "API key for embedding provider", "Embedding",
            SettingDataType.Secret, isSecret: true,
            allowedScopes: "[\"Machine\",\"Application\",\"User\"]");
        Add("andy.codeindex.chunkSizeTokens", "codeindex", "Chunk Size", "Maximum tokens per chunk", "Indexing",
            SettingDataType.Integer, "512");

        // andy.devpilot
        Add("andy.devpilot.llm.provider", "devpilot", "LLM Provider", "LLM API provider", "LLM",
            SettingDataType.String, "\"openai\"");
        Add("andy.devpilot.llm.model", "devpilot", "LLM Model", "Default LLM model", "LLM",
            SettingDataType.String, "\"gpt-4o-mini\"");
        Add("andy.devpilot.llm.apiKey", "devpilot", "LLM API Key", "API key for LLM provider", "LLM",
            SettingDataType.Secret, isSecret: true,
            allowedScopes: "[\"Machine\",\"Application\",\"User\"]");
        Add("andy.devpilot.llm.maxTokens", "devpilot", "Max Tokens", "Maximum output tokens", "LLM",
            SettingDataType.Integer, "4096");
        Add("andy.devpilot.agentTimeoutSeconds", "devpilot", "Agent Timeout", "Agent execution timeout in seconds", "Agents",
            SettingDataType.Integer, "120");

        // andy.docs
        Add("andy.docs.sourcePaths", "docs", "Source Paths", "Comma-separated list of documentation source paths", "General",
            SettingDataType.StringList, "[]");
        Add("andy.docs.cacheDurationMinutes", "docs", "Cache Duration", "Documentation cache duration in minutes", "Performance",
            SettingDataType.Integer, "30");

        // andy.settings (meta)
        Add("andy.settings.autoMigrate", "settings", "Auto Migrate", "Run database migrations on startup", "Database",
            SettingDataType.Boolean, "true");
        Add("andy.settings.seedOnStartup", "settings", "Seed on Startup", "Seed definitions on startup in development", "Database",
            SettingDataType.Boolean, "true");

        // andy.tasks
        Add("andy.tasks.defaultPriority", "andy-tasks", "Default Priority", "Default priority for new items", "General",
            SettingDataType.String, "\"medium\"",
            allowedScopes: "[\"Machine\",\"Application\",\"User\",\"Team\"]");
        Add("andy.tasks.maxItemsPerList", "andy-tasks", "Max Items per List", "Maximum number of items allowed in a single list", "Limits",
            SettingDataType.Integer, "1000");
        Add("andy.tasks.enableReminders", "andy-tasks", "Enable Reminders", "Send reminder notifications for due items", "Notifications",
            SettingDataType.Boolean, "true");

        return defs;
    }
}
