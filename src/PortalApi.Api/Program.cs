using Microsoft.EntityFrameworkCore;
using PortalApi.Infrastructure.Data;
using PortalApi.Infrastructure.DependencyInjection;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ─── JSON options ─────────────────────────────────────────────────────────────

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// ─── OpenAPI ──────────────────────────────────────────────────────────────────

builder.Services.AddOpenApi();

// ─── Infrastructure ───────────────────────────────────────────────────────────

builder.Services.AddInfrastructure(builder.Configuration);

// ─── Build ────────────────────────────────────────────────────────────────────

var app = builder.Build();

// ─── OpenAPI / Scalar ────────────────────────────────────────────────────────

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "PortalApi";
        options.Theme = ScalarTheme.DeepSpace;
    });
}

// ─── Endpoints ────────────────────────────────────────────────────────────────

// Rota exposta sem prefixo — o Caddy é responsável por remover o prefixo
// /api/portal antes de repassar pra este container (handle_path, não handle).
app.MapGet("/health", async (PortalDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return canConnect
        ? Results.Ok(new { status = "healthy", database = "connected", timestamp = DateTimeOffset.UtcNow })
        : Results.Problem("Não foi possível conectar ao banco de dados.", statusCode: 503);
})
.WithName("HealthCheck");

app.Run();
