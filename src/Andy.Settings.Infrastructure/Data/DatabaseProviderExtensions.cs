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
                if (string.IsNullOrEmpty(connectionString))
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
        // Check if running inside Conductor
        var conductorPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library", "Application Support", "ai.rivoli.conductor", "db", "andy-settings.sqlite");

        if (Directory.Exists(Path.GetDirectoryName(conductorPath)!))
            return conductorPath;

        // Default standalone path
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".andy-settings", "andy-settings.sqlite");
    }
}
