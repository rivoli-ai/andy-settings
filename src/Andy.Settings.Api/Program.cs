using System.Text.Json.Serialization;
using Andy.Settings.Api.Data;
using Andy.Settings.Application.Interfaces;
using Andy.Settings.Infrastructure.Data;
using Andy.Settings.Infrastructure.Repositories;
using Andy.Settings.Infrastructure.Services;
using Andy.Settings.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// ── JSON options ────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// ── EF Core ─────────────────────────────────────────────────────────────────
builder.Services.AddSettingsDbContext(builder.Configuration);

// ── Data Protection (secret encryption) ─────────────────────────────────────
builder.Services.AddDataProtection();

// ── Application services ────────────────────────────────────────────────────
builder.Services.AddScoped<IDefinitionService, DefinitionRepository>();
builder.Services.AddScoped<IAuditService, AuditRepository>();
builder.Services.AddScoped<IAssignmentService, AssignmentRepository>();
builder.Services.AddScoped<IResolutionService, ResolutionService>();
builder.Services.AddScoped<ISecretService, SecretService>();
builder.Services.AddScoped<IValidationService, ValidationService>();
builder.Services.AddScoped<DataSeeder>();

// ── Authentication ──────────────────────────────────────────────────────────
// Story #18 will add Andy Auth JWT Bearer here.
// For now, development mode allows all requests.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddAuthentication();
    builder.Services.AddAuthorization(options =>
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();
    });
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
var serviceName = builder.Configuration.GetValue<string>("OpenTelemetry:ServiceName") ?? SettingsTelemetry.ServiceName;
var otlpEndpoint = builder.Configuration.GetValue<string>("OpenTelemetry:OtlpEndpoint");

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName))
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

// ── Serilog ─────────────────────────────────────────────────────────────────
// Story #11 calls for Serilog; using built-in logging for now.
// builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// ═════════════════════════════════════════════════════════════════════════════
var app = builder.Build();

// ── Development-only middleware ──────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Auto-migrate
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<SettingsDbContext>();
    db.Database.Migrate();

    // Seed definitions
    var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
    await seeder.SeedAsync();
}

// ── Pipeline ────────────────────────────────────────────────────────────────
app.UseHttpsRedirection();
app.UseCors();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// ── Health endpoint ─────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTimeOffset.UtcNow }))
    .ExcludeFromDescription();

// ── SPA fallback ────────────────────────────────────────────────────────────
app.MapFallbackToFile("index.html");

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
