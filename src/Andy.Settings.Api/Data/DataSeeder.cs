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

    /// <summary>
    /// Reconciles the definition catalog against every registration manifest
    /// that loaded successfully this boot.
    /// </summary>
    /// <remarks>
    /// This used to be insert-if-absent, which made manifests WRITE-ONCE: after
    /// a key's first insertion, changing its default, validation, allowed
    /// scopes or description in the owning service's registration.json had no
    /// effect, ever (rivoli-ai/andy-settings#136). Since manifests are the
    /// distribution mechanism for definitions, every definition in the
    /// ecosystem was frozen at whatever shape it had on first boot.
    ///
    /// Ownership rule: the MANIFEST WINS. A definition whose ApplicationCode
    /// matches a loaded manifest is owned by that manifest, and its schema is
    /// reset to match on every boot — an operator's API edit to those fields is
    /// overwritten. Operator-set VALUES (assignments and secrets) are never
    /// touched here; only the schema is owned.
    ///
    /// Definitions that vanish from their manifest are marked deprecated rather
    /// than deleted. Deletion cascades to stored assignments and encrypted
    /// secrets, so a manifest typo would be unrecoverable.
    ///
    /// Reconciliation is scoped to application codes whose manifest actually
    /// LOADED. RegistrationManifestLoader skips manifests that are missing or
    /// fail to parse, and treating a failed load as "everything disappeared"
    /// would deprecate a whole service's catalog on a transient file error.
    /// </remarks>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var definitions = BuildSeedDefinitions();
        var (fromManifests, loadedApplicationCodes) = BuildFromManifests();
        definitions.AddRange(fromManifests);

        // Manifest wins over the legacy hardcoded catalog on a key collision,
        // and we never insert the same Key twice.
        var desired = definitions
            .GroupBy(d => d.Key, StringComparer.Ordinal)
            .Select(g => g.Last())
            .ToList();
        var desiredKeys = desired.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);

        var existing = await _db.SettingDefinitions
            .Include(d => d.Assignments)
            .Include(d => d.Secrets)
            .ToListAsync(ct);
        var existingByKey = existing.ToDictionary(d => d.Key, StringComparer.Ordinal);

        var added = 0;
        var updated = 0;
        var deprecated = 0;
        var revived = 0;

        foreach (var incoming in desired)
        {
            if (!existingByKey.TryGetValue(incoming.Key, out var current))
            {
                _db.SettingDefinitions.Add(incoming);
                added++;
                continue;
            }

            // Un-deprecate a key that came back into its manifest. Counted so
            // the no-op guard below doesn't skip the save when reviving is the
            // only change.
            if (current.IsDeprecated)
            {
                current.IsDeprecated = false;
                current.UpdatedAt = DateTimeOffset.UtcNow;
                revived++;
            }

            if (TryReconcile(current, incoming))
                updated++;
        }

        // Only for application codes whose manifest loaded this boot.
        foreach (var orphan in existing.Where(d =>
                     !d.IsDeprecated &&
                     loadedApplicationCodes.Contains(d.ApplicationCode) &&
                     !desiredKeys.Contains(d.Key)))
        {
            _logger.LogInformation(
                "Definition {Key} is no longer declared by manifest {App}; marking deprecated. "
                + "Stored values are retained.",
                orphan.Key, orphan.ApplicationCode);
            orphan.IsDeprecated = true;
            orphan.UpdatedAt = DateTimeOffset.UtcNow;
            deprecated++;
        }

        if (added == 0 && updated == 0 && deprecated == 0 && revived == 0)
        {
            _logger.LogDebug("Setting definitions already match their manifests");
            return;
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Definition catalog reconciled: {Added} added, {Updated} updated, "
            + "{Deprecated} deprecated, {Revived} un-deprecated",
            added, updated, deprecated, revived);
    }

    /// <summary>
    /// Copies manifest-owned schema onto an existing definition. Returns true
    /// when anything actually changed.
    /// </summary>
    private bool TryReconcile(SettingDefinition current, SettingDefinition incoming)
    {
        // Switching storage mode with values already stored would strand or
        // expose them. ExportImportService enforces the same rule on import.
        var wasSecret = current.IsSecret || current.DataType == SettingDataType.Secret;
        var willBeSecret = incoming.IsSecret || incoming.DataType == SettingDataType.Secret;
        if (wasSecret != willBeSecret && (current.Assignments.Count > 0 || current.Secrets.Count > 0))
        {
            _logger.LogWarning(
                "Manifest for {Key} switches secret storage mode but the definition has stored values; "
                + "leaving it unchanged. Migrate the values, then re-run.",
                current.Key);
            return false;
        }

        var changed =
            current.ApplicationCode != incoming.ApplicationCode ||
            current.DisplayName != incoming.DisplayName ||
            current.Description != incoming.Description ||
            current.Category != incoming.Category ||
            current.DataType != incoming.DataType ||
            current.DefaultValueJson != incoming.DefaultValueJson ||
            current.ValidationJson != incoming.ValidationJson ||
            current.UiSchemaJson != incoming.UiSchemaJson ||
            current.IsSecret != incoming.IsSecret ||
            current.AllowedScopesJson != incoming.AllowedScopesJson ||
            current.TagsJson != incoming.TagsJson;

        if (!changed)
            return false;

        current.ApplicationCode = incoming.ApplicationCode;
        current.DisplayName = incoming.DisplayName;
        current.Description = incoming.Description;
        current.Category = incoming.Category;
        current.DataType = incoming.DataType;
        current.DefaultValueJson = incoming.DefaultValueJson;
        current.ValidationJson = incoming.ValidationJson;
        current.UiSchemaJson = incoming.UiSchemaJson;
        current.IsSecret = incoming.IsSecret;
        current.AllowedScopesJson = incoming.AllowedScopesJson;
        current.TagsJson = incoming.TagsJson;
        current.UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    /// <summary>
    /// Reads every available registration.json and projects its settings.definitions
    /// array into SettingDefinition rows. Manifest-provided defaults are JSON-encoded
    /// literally; secrets omit defaultValue.
    /// </summary>
    private (List<SettingDefinition> Rows, HashSet<string> LoadedApplicationCodes) BuildFromManifests()
    {
        var manifests = RegistrationManifestLoader.LoadAll(_configuration, _logger);
        var now = DateTimeOffset.UtcNow;
        var rows = new List<SettingDefinition>();

        // Application codes whose manifest loaded successfully. Only these are
        // eligible for the "disappeared from its manifest" sweep — a manifest
        // that failed to load must never be read as an empty one.
        var loadedApplicationCodes = manifests
            .Select(m => m.Service.Name)
            .ToHashSet(StringComparer.Ordinal);

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
                    IsSecret = (def.IsSecret ?? false) || dataType == SettingDataType.Secret,
                    AllowedScopesJson = SerializeAllowedScopes(def.AllowedScopes),
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        return (rows, loadedApplicationCodes);
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
