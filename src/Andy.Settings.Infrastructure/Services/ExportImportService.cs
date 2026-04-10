using System.Text.Json;
using Andy.Settings.Application.DTOs.ImportExport;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Andy.Settings.Infrastructure.Services;

public class ExportImportService : IExportImportService
{
    private readonly SettingsDbContext _db;

    public ExportImportService(SettingsDbContext db) => _db = db;

    public async Task<ExportResult> ExportAsync(ExportOptions options, CancellationToken ct = default)
    {
        var definitionsQuery = _db.SettingDefinitions.AsQueryable();
        if (!string.IsNullOrEmpty(options.ApplicationCode))
            definitionsQuery = definitionsQuery.Where(d => d.ApplicationCode == options.ApplicationCode);

        var definitions = await definitionsQuery.ToListAsync(ct);
        var definitionIds = definitions.Select(d => d.Id).ToHashSet();

        var assignments = await _db.SettingAssignments
            .Where(a => definitionIds.Contains(a.DefinitionId))
            .ToListAsync(ct);

        var exportData = new
        {
            definitions = definitions.Select(d => new
            {
                d.Key, d.ApplicationCode, d.DisplayName, d.Description,
                d.Category, DataType = d.DataType.ToString(),
                d.DefaultValueJson, d.ValidationJson, d.IsSecret,
                d.AllowedScopesJson, d.TagsJson, d.IsDeprecated
            }),
            assignments = assignments.Select(a => new
            {
                DefinitionKey = definitions.First(d => d.Id == a.DefinitionId).Key,
                ScopeType = a.ScopeType.ToString(), a.ScopeId,
                a.ValueJson
            })
        };

        return new ExportResult
        {
            Format = options.Format ?? "json",
            ExportedAt = DateTimeOffset.UtcNow,
            DefinitionCount = definitions.Count,
            AssignmentCount = assignments.Count,
            Data = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true })
        };
    }

    public Task<ImportPreview> PreviewImportAsync(Stream data, CancellationToken ct = default)
    {
        // Placeholder — full implementation in a follow-up
        return Task.FromResult(new ImportPreview
        {
            ValidationErrors = ["Import preview not yet implemented"]
        });
    }

    public Task<ImportResult> ImportAsync(Stream data, ImportOptions options, string? actorId, CancellationToken ct = default)
    {
        // Placeholder — full implementation in a follow-up
        return Task.FromResult(new ImportResult());
    }
}
