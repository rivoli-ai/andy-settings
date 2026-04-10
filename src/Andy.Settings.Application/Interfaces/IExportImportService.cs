using Andy.Settings.Application.DTOs.ImportExport;

namespace Andy.Settings.Application.Interfaces;

public interface IExportImportService
{
    Task<ExportResult> ExportAsync(ExportOptions options, CancellationToken ct = default);
    Task<ImportPreview> PreviewImportAsync(Stream data, CancellationToken ct = default);
    Task<ImportResult> ImportAsync(Stream data, ImportOptions options, string? actorId, CancellationToken ct = default);
}
