using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Andy.Settings.Infrastructure.Data;

public static class DatabaseProviderExtensions
{
    public static IServiceCollection AddSettingsDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration.GetValue<string>("Database:Provider") ?? "PostgreSql";

        services.AddDbContext<SettingsDbContext>(options =>
        {
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");

                // Ignore PostgreSQL-style connection strings when SQLite is selected
                if (string.IsNullOrEmpty(connectionString) ||
                    connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
                {
                    var dbPath = GetDefaultSqlitePath();
                    var dir = Path.GetDirectoryName(dbPath)!;
                    Directory.CreateDirectory(dir);
                    connectionString = $"Data Source={dbPath}";
                }

                options.UseSqlite(connectionString);
            }
            else
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required for PostgreSQL.");
                options.UseNpgsql(connectionString);
            }
        });

        return services;
    }

    private static string GetDefaultSqlitePath()
    {
        // Use Conductor path only when explicitly running inside Conductor (env var set by ServiceOrchestrator)
        if (Environment.GetEnvironmentVariable("CONDUCTOR_EMBEDDED") == "true")
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "ai.rivoli.conductor", "db", "andy-settings.sqlite");
        }

        // Default standalone path
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".andy-settings", "andy-settings.sqlite");
    }
}
