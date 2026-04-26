using System.Security.Claims;
using System.Text.Encodings.Web;
using Andy.Settings.Api.Data;
using Andy.Settings.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Andy.Settings.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Bundled fixture manifests under tests/.../Fixtures/registrations/
            // are copied to bin/ via the test csproj. Pointing the seeder at
            // them keeps integration tests deterministic across Mac/Linux CI —
            // production reads from /etc/andy/registrations or whatever path
            // ops configures, but tests must not depend on the dev workstation.
            var fixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "registrations");

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AndyAuth:Authority"] = "",
                ["Rbac:ApiBaseUrl"] = "",
                ["Database:Provider"] = "Sqlite",
                ["OpenTelemetry:OtlpEndpoint"] = "",
                ["ConnectionStrings:DefaultConnection"] = "",
                ["Registrations:ManifestPaths:0"] = fixtureDir,
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace DbContext with SQLite in-memory
            var descriptorsToRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<SettingsDbContext>)
                         || d.ServiceType == typeof(DbContextOptions))
                .ToList();
            foreach (var d in descriptorsToRemove) services.Remove(d);

            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();

            services.AddDbContext<SettingsDbContext>(options =>
                options.UseSqlite(_connection));

            // Bypass authentication: auto-succeed with a test user
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", null);

            // Bypass authorization: allow all policies
            services.AddSingleton<IAuthorizationPolicyProvider>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<AuthorizationOptions>>();
                return new AllowAllTestPolicyProvider(opts);
            });

            // Seed database
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SettingsDbContext>();
            db.Database.EnsureCreated();

            var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
            seeder.SeedAsync().GetAwaiter().GetResult();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection?.Dispose();
    }
}

/// <summary>
/// Authentication handler that always succeeds with a test user identity.
/// </summary>
internal class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "test-user"),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Email, "test@andy.local"),
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

internal class AllowAllTestPolicyProvider : DefaultAuthorizationPolicyProvider
{
    private static readonly AuthorizationPolicy AllowAll = new AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes("Test")
        .RequireAssertion(_ => true)
        .Build();

    public AllowAllTestPolicyProvider(IOptions<AuthorizationOptions> options) : base(options) { }
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) => Task.FromResult<AuthorizationPolicy?>(AllowAll);
    public new Task<AuthorizationPolicy> GetDefaultPolicyAsync() => Task.FromResult(AllowAll);
}
