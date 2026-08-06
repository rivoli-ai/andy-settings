using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using Andy.Settings.Api.Data;
using Andy.Settings.Api.Services;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Infrastructure.Data;
using Andy.Settings.Infrastructure.Messaging;
using Andy.Settings.Infrastructure.Repositories;
using Andy.Settings.Infrastructure.Services;
using Andy.Settings.Infrastructure.Telemetry;
using Andy.Rbac.Client;
using Andy.Telemetry;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// ── Server URLs for MCP metadata ────────────────────────────────────────────
var configuredUrl = builder.Configuration["Urls"]?.Split(';').FirstOrDefault();
var serverUrl = configuredUrl != null && !configuredUrl.Contains("://+:") && !configuredUrl.Contains("://0.0.0.0:")
    ? configuredUrl
    : "https://localhost:5300";
var protectedResourceUrl = $"{serverUrl}/mcp";
var andyAuthAuthority = builder.Configuration["AndyAuth:Authority"] ?? "";

// ── Auth bypass guard ───────────────────────────────────────────────────────
// Two independent switches turn off all access control: an empty
// AndyAuth:Authority disables authentication entirely, and Development
// registers AllowAllDevPolicyProvider, which satisfies EVERY [RequirePermission]
// on every controller and MCP tool. Both are keyed on configuration that a
// deployment can drift into silently — docker-compose.yml ships with both
// active — and a settings-and-secrets API that comes up unauthenticated logs
// nothing unusual while doing it.
//
// Messaging already fails loud for exactly this class of mistake (see the AK1
// guard below). This is the symmetric guard for auth
// (rivoli-ai/andy-settings#144). ANDY_ALLOW_INSECURE_AUTH exists so that
// running without auth is a deliberate act rather than an environment-name
// coincidence.
var allowInsecureAuth = string.Equals(
    Environment.GetEnvironmentVariable("ANDY_ALLOW_INSECURE_AUTH"), "true", StringComparison.OrdinalIgnoreCase);

if (string.IsNullOrEmpty(andyAuthAuthority) && !builder.Environment.IsDevelopment() && !allowInsecureAuth)
{
    throw new InvalidOperationException(
        $"AndyAuth:Authority must be set in {builder.Environment.EnvironmentName}. " +
        "It is empty, which disables authentication for the entire API — including " +
        "the secrets endpoints. Set AndyAuth__Authority on the host, or set " +
        "ANDY_ALLOW_INSECURE_AUTH=true to run without authentication deliberately.");
}

// ── JSON options ────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// ── EF Core ─────────────────────────────────────────────────────────────────
builder.Services.AddSettingsDbContext(builder.Configuration);

// ── Messaging (ADR 0001 — Epic AL) ──────────────────────────────────────────
// InMemory is the default for `dotnet run` and tests. NATS is required
// in every other environment per AK1; the guard below trips at boot if
// production config silently fell back to InMemory.
var messagingProvider = builder.Configuration["Messaging:Provider"] ?? "InMemory";
if (!builder.Environment.IsDevelopment()
    && !string.Equals(messagingProvider, "Nats", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        $"Messaging:Provider must be 'Nats' in {builder.Environment.EnvironmentName}. " +
        $"Got '{messagingProvider}'. In-memory bus is only valid in Development. " +
        "Set Messaging__Provider=Nats and Messaging__Nats__Url on the host.");
}
builder.Services.AddSettingsMessaging(builder.Configuration, builder.Environment);

// ── Data Protection (secret encryption) ─────────────────────────────────────
// Persist the key ring to a STABLE on-disk location. Bare
// AddDataProtection() falls back to EPHEMERAL keys when it can't find a
// writable user-profile key store — and an ephemeral key ring is
// regenerated on every restart, so secrets encrypted in a prior run
// become permanently undecryptable ("payload was invalid" → 500). That
// took down andy-tasks at startup. A fixed path also means the embedded
// app and the conductord daemon (running as the same user) share one key
// ring, so a secret survives the move between hosting modes.
// Override via ANDY_DATAPROTECTION_KEYS_DIR (e.g. a mounted volume in
// the Dockerized conductord deployment).
var dataProtectionKeysDir = Environment.GetEnvironmentVariable("ANDY_DATAPROTECTION_KEYS_DIR")
    ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".andy", "dataprotection-keys");
Directory.CreateDirectory(dataProtectionKeysDir);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysDir))
    // Pin the application discriminator (rivoli-ai/conductor#2118). Without
    // this, DataProtection derives the discriminator from the CONTENT ROOT
    // PATH — so a payload encrypted while the service ran from one services
    // dir (app bundle, conductord worktree snapshot, canonical repo dir)
    // throws "The payload was invalid" after ANY relocation, even with the
    // identical key ring on disk. That made every hosting-mode or deploy-path
    // change silently invalidate ALL stored secrets, forcing users to
    // re-enter keys/PATs over and over. A fixed name makes payload
    // portability follow the key ring, not the install path.
    .SetApplicationName("andy-settings");

// ── Application services ────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IDefinitionService, DefinitionRepository>();
builder.Services.AddScoped<IAuditService, AuditRepository>();
builder.Services.AddScoped<IAssignmentService, AssignmentRepository>();
builder.Services.AddScoped<IResolutionService, ResolutionService>();
builder.Services.AddScoped<ISecretService, SecretService>();
builder.Services.AddScoped<IValidationService, ValidationService>();
builder.Services.AddScoped<IExportImportService, ExportImportService>();
builder.Services.AddScoped<DataSeeder>();

// ── MCP Server ─────────────────────────────────────────────────────────────
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .AddAuthorizationFilters()
    .WithToolsFromAssembly();

// ── Authentication (Andy Auth) ──────────────────────────────────────────────
if (!string.IsNullOrEmpty(andyAuthAuthority))
{
    var audience = builder.Configuration["AndyAuth:Audience"] ?? "urn:andy-settings-api";
    builder.Services.AddAuthentication("Bearer")
        .AddJwtBearer("Bearer", options =>
        {
            options.Authority = andyAuthAuthority;
            options.Audience = audience;
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            if (builder.Environment.IsDevelopment())
            {
                options.BackchannelHttpHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
                options.TokenValidationParameters.ValidIssuers = new[]
                {
                    andyAuthAuthority, andyAuthAuthority.TrimEnd('/') + "/",
                    "https://localhost:5001", "https://localhost:5001/"
                };
            }
        });
    builder.Services.AddAuthorization();

    // MCP OAuth Protected Resource Metadata (RFC 8707)
    builder.Services.AddAuthentication()
        .AddMcp(mcpOptions =>
        {
            mcpOptions.ResourceMetadataUri = new Uri($"{serverUrl}/mcp/.well-known/oauth-protected-resource");
            mcpOptions.ResourceMetadata = new()
            {
                Resource = new Uri(protectedResourceUrl),
                ResourceDocumentation = new Uri("https://github.com/rivoli-ai/andy-settings"),
                AuthorizationServers = { new Uri(andyAuthAuthority) },
                ScopesSupported = ["openid", "profile", "email"],
            };
        });
}
else
{
    // Dev fallback: no auth enforcement for local development
    builder.Services.AddAuthentication();
    builder.Services.AddAuthorization(options =>
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();
    });
}

// ── RBAC (Andy.Rbac.Client) ─────────────────────────────────────────────────
var rbacBaseUrl = builder.Configuration["Rbac:ApiBaseUrl"];
if (!string.IsNullOrEmpty(rbacBaseUrl))
{
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.ConfigureHttpClientDefaults(b =>
            b.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            }));
    }

    if (builder.Environment.IsDevelopment())
    {
        // Development policies are explicitly permissive below; keep the
        // client registration independent of M2M secrets for local/test hosts.
        builder.Services.AddRbacClient(options =>
        {
            options.ApiBaseUrl = rbacBaseUrl;
            options.ApplicationCode = "settings";
        });
    }
    else
    {
        // RBAC's API is protected too. Use the client-credentials handler so a
        // valid end-user token is not accidentally forwarded as service identity.
        builder.Services.AddRbacClientWithM2M(builder.Configuration);
    }

    if (builder.Environment.IsDevelopment())
    {
        // Satisfies every policy, so all [RequirePermission] checks pass. Only
        // reachable in Development — the guard at the top of this file stops a
        // non-Development host from booting into an unauthenticated state.
        builder.Services.AddSingleton<IAuthorizationPolicyProvider>(sp =>
        {
            var opts = sp.GetRequiredService<IOptions<AuthorizationOptions>>();
            return new AllowAllDevPolicyProvider(opts);
        });
    }
}

// ── Swagger / OpenAPI ───────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Andy Settings API",
        Version = "v1",
        Description = "Centralized configuration and settings management for the Andy ecosystem"
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    // Microsoft.OpenApi v2 (via Swashbuckle 10) replaced the
    // OpenApiSecurityScheme.Reference / OpenApiReference pair with a dedicated
    // reference type, and AddSecurityRequirement now takes a factory over the
    // document rather than the requirement itself.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        // The reference must be bound to the host document. Without it this
        // compiles but emits "security": [{}], silently dropping bearer auth
        // from the generated OpenAPI document.
        { new OpenApiSecuritySchemeReference("Bearer", document), new List<string>() }
    });
});

// ── CORS ────────────────────────────────────────────────────────────────────
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
    options.AddPolicy("AllowMcpClients", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// ── OpenTelemetry (via Andy.Telemetry) ─────────────────────────────────────
// OT5 (rivoli-ai/conductor#1263). Replaces the per-service OpenTelemetry
// hand-roll with the shared library so every Andy service shares the same
// attribute set, propagator stack, and OTLP export config. UnifiedProxy
// already emits server-side request spans, so AspNetCore instrumentation
// stays off here to avoid double-counting.
builder.Services.AddAndyTelemetry(builder.Configuration, o =>
{
    if (string.IsNullOrWhiteSpace(o.ServiceName))
        o.ServiceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "andy-settings";
    if (string.IsNullOrWhiteSpace(o.OtlpEndpoint))
        o.OtlpEndpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
    if (string.IsNullOrWhiteSpace(o.Protocol) || o.Protocol == "grpc")
    {
        var envProtocol = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL");
        if (!string.IsNullOrWhiteSpace(envProtocol))
            o.Protocol = envProtocol;
    }
    o.ActivitySources.Add(SettingsTelemetry.ServiceName);
    o.Meters.Add(SettingsTelemetry.ServiceName);
    o.EnableAspNetCoreInstrumentation = false;
    o.EnableHttpClientInstrumentation = true;
});
// EF Core tracing is service-specific (not bundled in Andy.Telemetry).
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddEntityFrameworkCoreInstrumentation());

// ═════════════════════════════════════════════════════════════════════════════
var app = builder.Build();

// HC.8.1 of rivoli-ai/conductor#1245: expose the OpenAPI document in
// every environment so Conductor's in-app Help Center can ingest
// /openapi.json from the bundled service. The Swagger UI itself
// stays development-only.
app.UseSwagger();
// Stable alias so every andy-* service exposes the same path.
app.MapGet("/openapi.json", () => Results.Redirect("/swagger/v1/swagger.json"))
    .ExcludeFromDescription();

// ── Development-only UI ─────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwaggerUI();
}

// An active auth bypass must be obvious in the log, not inferred from config
// (rivoli-ai/andy-settings#144).
if (string.IsNullOrEmpty(andyAuthAuthority) || app.Environment.IsDevelopment())
{
    var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Andy.Settings.Startup");
    if (string.IsNullOrEmpty(andyAuthAuthority))
        startupLogger.LogWarning(
            "[INSECURE] AndyAuth:Authority is empty — authentication is DISABLED for the entire API, "
            + "including the secrets endpoints. Never run like this outside local development.");
    if (app.Environment.IsDevelopment() && !string.IsNullOrEmpty(rbacBaseUrl))
        startupLogger.LogWarning(
            "[INSECURE] Development environment — RBAC permission checks are BYPASSED; "
            + "every [RequirePermission] is satisfied automatically.");
}

// ── Schema migration + definition seeding ───────────────────────────────────
// Runs in EVERY environment (rivoli-ai/andy-settings#128). These used to sit
// inside the IsDevelopment() block, so the schema was never created in the
// Embedded, Docker, or Production modes this service explicitly supports (see
// HostEnvironmentExtensions). Under the Conductor-embedded SQLite provider
// that failure is especially bad: SQLite creates an empty database file
// happily, so a missing schema reads as data loss rather than a config error.
//
// Seeding is what makes REGISTRATIONS__MANIFEST_PATHS work; gating it on
// Development made the documented production mechanism a no-op.
//
// Set Database:MigrateOnStartup=false when migrations are applied out of band
// (e.g. a deploy job in a multi-instance Postgres deployment, where concurrent
// startup migrations would race).
if (builder.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Andy.Settings.Startup");

    try
    {
        var db = scope.ServiceProvider.GetRequiredService<SettingsDbContext>();
        db.Database.Migrate();
        logger.LogInformation("Database schema is up to date.");

        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        // Fail fast. A service that starts without its schema serves 500s on
        // every request and looks healthy to an orchestrator.
        logger.LogCritical(ex, "Database migration or seeding failed; aborting startup.");
        throw;
    }
}

// ── Pipeline ────────────────────────────────────────────────────────────────
app.UseHttpsRedirection();
app.UseCors();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapMcp("/mcp")
    .RequireAuthorization()
    .RequireCors("AllowMcpClients");

// ── MCP OAuth well-known endpoints ──────────────────────────────────────────
if (!string.IsNullOrEmpty(andyAuthAuthority))
{
    var oauthMetadataJsonOptions = new System.Text.Json.JsonSerializerOptions
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    app.MapGet("/.well-known/oauth-protected-resource", (IServiceProvider sp) =>
    {
        var optionsMonitor = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<
            ModelContextProtocol.AspNetCore.Authentication.McpAuthenticationOptions>>();
        var options = optionsMonitor.Get(
            ModelContextProtocol.AspNetCore.Authentication.McpAuthenticationDefaults.AuthenticationScheme);
        return Results.Json(options.ResourceMetadata, oauthMetadataJsonOptions);
    }).AllowAnonymous().RequireCors("AllowMcpClients");

    app.MapGet("/mcp/.well-known/oauth-protected-resource", (IServiceProvider sp) =>
    {
        var optionsMonitor = sp.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<
            ModelContextProtocol.AspNetCore.Authentication.McpAuthenticationOptions>>();
        var options = optionsMonitor.Get(
            ModelContextProtocol.AspNetCore.Authentication.McpAuthenticationDefaults.AuthenticationScheme);
        return Results.Json(options.ResourceMetadata, oauthMetadataJsonOptions);
    }).AllowAnonymous().RequireCors("AllowMcpClients");

    app.MapGet("/.well-known/openid-configuration", () =>
        Results.Redirect($"{andyAuthAuthority}/.well-known/openid-configuration", permanent: false))
        .AllowAnonymous().RequireCors("AllowMcpClients");

    app.MapGet("/.well-known/oauth-authorization-server", () =>
        Results.Redirect($"{andyAuthAuthority}/.well-known/openid-configuration", permanent: false))
        .AllowAnonymous().RequireCors("AllowMcpClients");

    app.MapGet("/authorize", (HttpContext ctx) =>
    {
        var qs = ctx.Request.QueryString.HasValue ? ctx.Request.QueryString.Value : string.Empty;
        return Results.Redirect($"{andyAuthAuthority}/connect/authorize{qs}", permanent: false);
    }).AllowAnonymous().RequireCors("AllowMcpClients");

    app.MapPost("/token", (HttpContext ctx) =>
    {
        var qs = ctx.Request.QueryString.HasValue ? ctx.Request.QueryString.Value : string.Empty;
        ctx.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
        ctx.Response.Headers.Location = $"{andyAuthAuthority}/connect/token{qs}";
        return Task.CompletedTask;
    }).AllowAnonymous().RequireCors("AllowMcpClients");
}

// ── Health endpoint ─────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }))
    .AllowAnonymous().ExcludeFromDescription();

// ── Prometheus metrics scraping (via Andy.Telemetry) ────────────────────────
// OT5 (rivoli-ai/conductor#1263). Exposes /metrics for the Conductor
// scraper; OTLP push is independent.
app.MapAndyTelemetry();

// ── SPA fallback ────────────────────────────────────────────────────────────
app.MapFallbackToFile("index.html");

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }

/// <summary>
/// Bypasses RBAC permission checks in development.
/// </summary>
internal class AllowAllDevPolicyProvider : DefaultAuthorizationPolicyProvider
{
    private static readonly AuthorizationPolicy AllowAll = new AuthorizationPolicyBuilder()
        .RequireAssertion(_ => true)
        .Build();

    public AllowAllDevPolicyProvider(IOptions<AuthorizationOptions> options)
        : base(options) { }

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        => Task.FromResult<AuthorizationPolicy?>(AllowAll);

    public new Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        => Task.FromResult(AllowAll);
}
