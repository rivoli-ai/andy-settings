using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Andy.Settings.Infrastructure.Data;

/// <summary>
/// Design-time factory for EF Core migration tooling.
/// Usage: dotnet ef migrations add ... --project src/Andy.Settings.Infrastructure --startup-project src/Andy.Settings.Api
/// </summary>
public class SettingsDbContextFactory : IDesignTimeDbContextFactory<SettingsDbContext>
{
    public SettingsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "Andy.Settings.Api"))
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<SettingsDbContext>();

        var provider = configuration.GetValue<string>("Database:Provider") ?? "PostgreSql";

        if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.UseSqlite("Data Source=andy-settings-migrations.sqlite");
        }
        else
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? "Host=localhost;Port=5438;Database=andy_settings;Username=andy_settings;Password=andy_settings_dev_password";
            optionsBuilder.UseNpgsql(connectionString);
        }

        return new SettingsDbContext(optionsBuilder.Options);
    }
}
