using System.Security.Claims;
using System.Text.Encodings.Web;
using Andy.Settings.Api.Data;
using Andy.Settings.Application.Messaging;
using Andy.Settings.Infrastructure.Data;
using Andy.Settings.Infrastructure.Messaging;
using Andy.Settings.Infrastructure.Messaging.Nats;
using Microsoft.Extensions.Hosting;
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
    // Per-instance shared in-memory SQLite database. `Mode=Memory&Cache=Shared`
    // lets every DbContext open its own physical connection (so EF Core's
    // UDF registration/cleanup is per-context, not interleaved across
    // sibling contexts on the same connection — the root cause of the
    // "unable to delete/modify user-function due to active statements"
    // flake that the global DateTimeOffsetToBinaryConverter exposed once
    // OutboxDispatcher's 50ms poll started racing the HTTP request
    // pipeline against the same `:memory:` connection). The keep-alive
    // connection pins the in-memory DB so it isn't garbage-collected when
    // no other connection is open. Each factory instance gets a unique
    // database name so xUnit's parallel class execution can't collide.
    // Mirrors andy-agents' SqliteTestWebAppFactory (issue #154).
    // Keyword form (`Mode=Memory;Cache=Shared`), NOT the URI form
    // (`file:name?Mode=Memory&Cache=Shared`).
    //
    // SQLite parses URI query parameters case-sensitively and expects
    // lowercase `mode=memory`. `Mode=Memory` inside a file: URI is therefore
    // silently IGNORED, and SQLite opens a real file named after the database.
    // This harness was never in-memory: it had left 530 stray SQLite files in
    // bin/ before anyone noticed, and each run paid real disk I/O.
    //
    // It surfaced as a CI failure once the SqliteConcurrencyInterceptor started
    // enabling WAL on file-backed databases — correctly, since these really
    // were files — and WAL + shared cache changed cross-connection visibility
    // enough to break an outbox dispatch assertion on Linux.
    //
    // Microsoft.Data.Sqlite translates the keyword form into a proper in-memory
    // URI itself, so this genuinely is memory-backed.
    private readonly string _connectionString =
        $"Data Source=integration-tests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private SqliteConnection? _keepAlive;

    /// <summary>
    /// Points the suite at a real PostgreSQL server. Unset (the default) runs
    /// against in-memory SQLite.
    /// </summary>
    /// <remarks>
    /// Several findings in the 2026-07 audit were provider-divergent and a
    /// SQLite-only suite could not see them — most sharply #134, where a
    /// negative SQL OFFSET is clamped by SQLite (silently serving page 1) and
    /// rejected by PostgreSQL (500), so the same request behaved differently in
    /// the Conductor-embedded and shared deployments. Running the suite against
    /// both providers is what closes that blind spot
    /// (rivoli-ai/andy-settings#148).
    /// </remarks>
    public const string PostgresConnectionEnvVar = "ANDY_SETTINGS_TEST_POSTGRES";

    private static string? PostgresConnectionString =>
        Environment.GetEnvironmentVariable(PostgresConnectionEnvVar) is { Length: > 0 } value ? value : null;

    // Each factory instance needs its own schema so xUnit's parallel class
    // execution cannot collide on a shared Postgres server.
    private readonly string _postgresSchema = $"it_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development bypasses the AK1 fail-loud guard (in-memory bus is
        // only valid in Development). Program.cs reads Messaging:Provider
        // before the ConfigureAppConfiguration override below takes
        // effect, so the env flag is what determines whether the guard
        // fires.
        builder.UseEnvironment("Development");

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
                ["Database:Provider"] = PostgresConnectionString is null ? "Sqlite" : "PostgreSql",
                ["OpenTelemetry:OtlpEndpoint"] = "",
                ["ConnectionStrings:DefaultConnection"] = "",
                ["Registrations:ManifestPaths:0"] = fixtureDir,
                // AK1 guard requires Nats in non-Development envs; "Testing"
                // would otherwise trip it. The in-memory bus is the right
                // bus for hermetic API tests — outbox→publish→consume
                // round-trips don't need a real broker.
                ["Messaging:Provider"] = "InMemory",
                // Drop the outbox poll cadence so messaging tests don't
                // wait a full second for a drain.
                ["Messaging:Outbox:PollInterval"] = "00:00:00.050",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Force the in-memory bus. The minimal-hosting model reads
            // Messaging:Provider in Program.cs before the
            // ConfigureAppConfiguration override above takes effect, so
            // appsettings.json's "Nats" default may have already wired
            // NatsMessageBus + NatsStreamProvisioner. Strip them and
            // register the in-memory bus instead so hermetic tests don't
            // require a running JetStream.
            var natsDescriptors = services
                .Where(d => d.ServiceType == typeof(IMessageBus)
                         || d.ServiceType == typeof(NatsMessageBus)
                         || d.ImplementationType == typeof(NatsMessageBus)
                         || d.ImplementationType == typeof(NatsStreamProvisioner))
                .ToList();
            foreach (var d in natsDescriptors) services.Remove(d);
            services.AddSingleton<IMessageBus, InMemoryMessageBus>();

            // Replace DbContext with SQLite in-memory
            var descriptorsToRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<SettingsDbContext>)
                         || d.ServiceType == typeof(DbContextOptions))
                .ToList();
            foreach (var d in descriptorsToRemove) services.Remove(d);

            var postgres = PostgresConnectionString;
            if (postgres is null)
            {
                _keepAlive = new SqliteConnection(_connectionString);
                _keepAlive.Open();

                services.AddDbContext<SettingsDbContext>(options =>
                    options.UseSqlite(_connectionString));
            }
            else
            {
                // Isolated schema per factory instance, so parallel test
                // classes on one Postgres server cannot see each other's rows.
                var builder = new Npgsql.NpgsqlConnectionStringBuilder(postgres)
                {
                    SearchPath = _postgresSchema,
                };

                using (var connection = new Npgsql.NpgsqlConnection(postgres))
                {
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = $"CREATE SCHEMA IF NOT EXISTS \"{_postgresSchema}\";";
                    command.ExecuteNonQuery();
                }

                services.AddDbContext<SettingsDbContext>(options =>
                    options.UseNpgsql(builder.ConnectionString, npgsql =>
                        // SearchPath alone is not enough: EF creates the
                        // migrations-history table in the model's default
                        // schema, which Npgsql qualifies as "public". Every
                        // parallel factory then races to create the same
                        // public.__EFMigrationsHistory and all but the first
                        // fail with 42P07.
                        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", _postgresSchema)));
            }

            // Bypass authentication: auto-succeed with a test user
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", null);

            // Bypass authorization: allow all policies
            services.AddSingleton<IAuthorizationPolicyProvider>(sp =>
            {
                var opts = sp.GetRequiredService<IOptions<AuthorizationOptions>>();
                return new AllowAllTestPolicyProvider(opts);
            });

            // Seed database. Migrate (not EnsureCreated) so the
            // __EFMigrationsHistory table is populated — Program.cs's
            // own Migrate() call on Development startup is then a no-op
            // instead of trying to re-create the same tables.
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SettingsDbContext>();
            db.Database.Migrate();

            var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
            seeder.SeedAsync().GetAwaiter().GetResult();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;

        _keepAlive?.Dispose();

        // Drop the per-instance schema so a long-lived CI Postgres does not
        // accumulate one per test class per run.
        if (PostgresConnectionString is { } postgres)
        {
            try
            {
                using var connection = new Npgsql.NpgsqlConnection(postgres);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = $"DROP SCHEMA IF EXISTS \"{_postgresSchema}\" CASCADE;";
                command.ExecuteNonQuery();
            }
            catch (Npgsql.NpgsqlException)
            {
                // Best-effort cleanup — a teardown failure must not fail a
                // green test run.
            }
        }
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
