using System.Text.Json.Serialization;
using Andy.Settings.Api;
// HostEnvironmentExtensions.IsEmbedded / IsLocalOrEmbedded
using Andy.Settings.Api.Data;
using Andy.Settings.Api.Services;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Infrastructure.Data;
using Andy.Settings.Infrastructure.Messaging;
using Andy.Settings.Infrastructure.Repositories;
using Andy.Settings.Infrastructure.Services;
using Andy.Settings.Infrastructure.Telemetry;
using Andy.Rbac.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// ── Server URLs for MCP metadata ────────────────────────────────────────────
var configuredUrl = builder.Configuration["Urls"]?.Split(';').FirstOrDefault();
var serverUrl = configuredUrl != null && !configuredUrl.Contains("://+:") && !configuredUrl.Contains("://0.0.0.0:")
    ? configuredUrl
    : "https://localhost:5300";
var protectedResourceUrl = $"{serverUrl}/mcp";
var andyAuthAuthority = builder.Configuration["AndyAuth:Authority"] ?? "";

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
builder.Services.AddDataProtection();

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
            // HTTPS metadata off in every non-production mode. Conductor's
            // embedded mode talks to andy-auth over HTTP via the unified
            // proxy on port 9100 — without IsLocalOrEmbedded() the JWT
            // bearer middleware would refuse the discovery document.
            options.RequireHttpsMetadata = !builder.Environment.IsLocalOrEmbedded();
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

    builder.Services.AddRbacClient(options =>
    {
        options.ApiBaseUrl = rbacBaseUrl;
        options.ApplicationCode = "settings";
    });

    if (builder.Environment.IsDevelopment())
    {
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
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
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

// ── OpenTelemetry ───────────────────────────────────────────────────────────
var otelServiceName = builder.Configuration.GetValue<string>("OpenTelemetry:ServiceName") ?? SettingsTelemetry.ServiceName;
var otlpEndpoint = builder.Configuration.GetValue<string>("OpenTelemetry:OtlpEndpoint");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(otelServiceName))
    .WithTracing(tracing =>
    {
        tracing
            .AddSource(SettingsTelemetry.ServiceName)
            .AddAspNetCoreInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddHttpClientInstrumentation();

        if (!string.IsNullOrEmpty(otlpEndpoint))
            tracing.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        else
            tracing.AddConsoleExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(SettingsTelemetry.ServiceName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();

        if (!string.IsNullOrEmpty(otlpEndpoint))
            metrics.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        else
            metrics.AddConsoleExporter();
    });

// ═════════════════════════════════════════════════════════════════════════════
var app = builder.Build();

// ── Development-only chrome (Swagger UI) ────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ── Schema bootstrap + seed (every non-production mode) ─────────────────────
// Embedded needs schema-on-first-boot too — without this the bundled
// SQLite DB starts empty and every settings read 500s. The
// `IsRelational()` guard keeps integration tests (InMemory provider)
// from tripping on `Migrate()`.
if (app.Environment.IsLocalOrEmbedded())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SettingsDbContext>();
    if (db.Database.IsRelational())
    {
        db.Database.Migrate();
    }

    var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
    await seeder.SeedAsync();
}

// ── Pipeline ────────────────────────────────────────────────────────────────
// Conductor's embedded mode terminates HTTPS at the unified proxy on
// port 9100; the in-process Kestrel only ever binds to HTTP. Forcing
// a redirect would trap every request in a redirect loop.
if (!app.Environment.IsEmbedded())
{
    app.UseHttpsRedirection();
}
app.UseCors();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapMcp("/mcp").RequireCors("AllowMcpClients");

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
