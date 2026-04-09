var builder = WebApplication.CreateBuilder(args);

// TODO: Configure services (see stories for implementation order)
// - Andy Auth (JWT Bearer)
// - Andy RBAC
// - EF Core (PostgreSQL / SQLite)
// - Swagger / OpenAPI
// - MCP Server
// - OpenTelemetry
// - Serilog
// - CORS
// - Application services

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
