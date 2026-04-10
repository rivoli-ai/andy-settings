namespace Andy.Settings.Application.Options;

public class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Database provider: "PostgreSql" or "Sqlite".
    /// </summary>
    public string Provider { get; set; } = "PostgreSql";

    public bool IsPostgreSql => Provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase);
    public bool IsSqlite => Provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase);
}
