namespace Andy.Settings.Application.DTOs.ImportExport;

public record ExportOptions
{
    public string? ApplicationCode { get; init; }
    public string? Format { get; init; } = "json";
    public bool IncludeSecrets { get; init; }
}

public record ExportResult
{
    public string Format { get; init; } = "json";
    public DateTimeOffset ExportedAt { get; init; }
    public int DefinitionCount { get; init; }
    public int AssignmentCount { get; init; }
    public string Data { get; init; } = string.Empty;
}

public record ImportPreview
{
    public IReadOnlyList<ImportChange> Additions { get; init; } = [];
    public IReadOnlyList<ImportChange> Modifications { get; init; } = [];
    public IReadOnlyList<ImportChange> Deletions { get; init; } = [];
    public IReadOnlyList<string> ValidationErrors { get; init; } = [];
    public bool IsValid => ValidationErrors.Count == 0;
}

public record ImportChange(
    string DefinitionKey,
    string ChangeType,
    string? OldValue,
    string? NewValue
);

public record ImportOptions
{
    public bool DryRun { get; init; }
}

public record ImportResult
{
    public int DefinitionsCreated { get; init; }
    public int DefinitionsUpdated { get; init; }
    public int AssignmentsCreated { get; init; }
    public int AssignmentsUpdated { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
