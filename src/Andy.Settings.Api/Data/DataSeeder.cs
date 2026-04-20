using System.Text.Json;
using Andy.Settings.Domain.Entities;
using Andy.Settings.Domain.Enums;
using Andy.Settings.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Andy.Settings.Api.Data;

public class DataSeeder
{
    private readonly SettingsDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(SettingsDbContext db, IConfiguration configuration, ILogger<DataSeeder> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var definitions = BuildSeedDefinitions();
        definitions.AddRange(BuildFromManifests());

        var existingKeys = await _db.SettingDefinitions
            .Select(d => d.Key)
            .ToListAsync(ct);
        var toAdd = definitions
            .Where(d => !existingKeys.Contains(d.Key))
            // Deduplicate within this batch in case a manifest key collides with
            // the legacy hardcoded catalog — manifest wins for future additions
            // but we never insert the same Key twice.
            .GroupBy(d => d.Key)
            .Select(g => g.First())
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

    /// <summary>
    /// Reads every available registration.json and projects its settings.definitions
    /// array into SettingDefinition rows. Manifest-provided defaults are JSON-encoded
    /// literally; secrets omit defaultValue.
    /// </summary>
    private List<SettingDefinition> BuildFromManifests()
    {
        var manifests = RegistrationManifestLoader.LoadAll(_configuration, _logger);
        var now = DateTimeOffset.UtcNow;
        var rows = new List<SettingDefinition>();

        foreach (var manifest in manifests)
        {
            var defs = manifest.Settings?.Definitions;
            if (defs is null || defs.Length == 0) continue;

            foreach (var def in defs)
            {
                if (!Enum.TryParse<SettingDataType>(def.DataType, ignoreCase: true, out var dataType))
                {
                    _logger.LogWarning("Manifest {Service}: unknown dataType '{DataType}' for key '{Key}'; skipping.",
                        manifest.Service.Name, def.DataType, def.Key);
                    continue;
                }

                rows.Add(new SettingDefinition
                {
                    Id = Guid.NewGuid(),
                    Key = def.Key,
                    ApplicationCode = manifest.Service.Name,
                    DisplayName = def.DisplayName ?? def.Key,
                    Description = def.Description,
                    Category = def.Category,
                    DataType = dataType,
                    DefaultValueJson = SerializeDefaultValue(def.DefaultValue),
                    IsSecret = def.IsSecret ?? false,
                    AllowedScopesJson = SerializeAllowedScopes(def.AllowedScopes),
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        return rows;
    }

    private static string? SerializeDefaultValue(object? value)
    {
        if (value is null) return null;
        return JsonSerializer.Serialize(value);
    }

    private static string SerializeAllowedScopes(string[]? scopes)
    {
        if (scopes is null || scopes.Length == 0)
            return "[\"Machine\",\"Application\",\"User\"]";
        return JsonSerializer.Serialize(scopes);
    }

    /// <summary>
    /// Previously shipped a hardcoded catalog of setting definitions for every
    /// service. Now empty: every definition lives in the owning service's
    /// <c>config/registration.json</c> and is loaded via
    /// <see cref="BuildFromManifests"/>. Kept as a vestigial hook for any
    /// definitions that are truly meta-settings of andy-settings itself —
    /// currently none.
    /// </summary>
    private static List<SettingDefinition> BuildSeedDefinitions() => new();
}
