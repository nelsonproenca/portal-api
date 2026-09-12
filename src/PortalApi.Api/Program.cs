using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PortalApi.Api;
using PortalApi.Api.Endpoints;
using PortalApi.Infrastructure.Data;
using PortalApi.Infrastructure.DependencyInjection;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ─── Auth (cookie httpOnly — sessão do admin, ver ticket #14) ──────────────────

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "portal_admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax; // same-origin (path-routed), não precisa de None
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Secure em prod (https), permissivo em dev (http)
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false; // expira e pede login de novo — sem refresh token

        // API, não app MVC: 401/403 em vez de redirect pra uma página de login inexistente.
        options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
        options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
    });

builder.Services.AddAuthorization();

// ─── Rate limiting (login do admin — ver ticket #14) ───────────────────────────

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
        }));
});

// ─── JSON options ─────────────────────────────────────────────────────────────

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

// ─── OpenAPI ──────────────────────────────────────────────────────────────────

builder.Services.AddOpenApi();

// ─── Infrastructure ───────────────────────────────────────────────────────────

builder.Services.AddInfrastructure(builder.Configuration);

// ─── Build ────────────────────────────────────────────────────────────────────

var app = builder.Build();

// ─── Migrations automáticas no startup ─────────────────────────────────────────

// Deploy de instância única (sem blue-green) — aplicar aqui evita depender do SDK
// do .NET estar instalado na VPS só pra rodar "dotnet ef database update".
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<PortalDbContext>().Database.MigrateAsync();
}

// ─── Comando CLI de bootstrap (sem subir o Kestrel) ────────────────────────────

if (args.Length > 0 && args[0] == "seed-admin")
    return await SeedAdminCommand.RunAsync(args, app.Services);

if (args.Length > 0 && args[0] == "import-clientes")
    return await ImportClientesCommand.RunAsync(args, app.Services);

if (args.Length > 0 && args[0] == "import-projetos")
    return await ImportProjetosCommand.RunAsync(args, app.Services);

// ─── Middleware pipeline ──────────────────────────────────────────────────────

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

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

// Rotas expostas sem prefixo — o Caddy é responsável por remover o prefixo
// /api/portal antes de repassar pra este container (handle_path, não handle).
app.MapGet("/health", async (PortalDbContext db, CancellationToken ct) =>
{
    var canConnect = await db.Database.CanConnectAsync(ct);
    return canConnect
        ? Results.Ok(new { status = "healthy", database = "connected", timestamp = DateTimeOffset.UtcNow })
        : Results.Problem("Não foi possível conectar ao banco de dados.", statusCode: 503);
})
.WithName("HealthCheck");

app.MapAuthEndpoints();
app.MapClienteEndpoints();
app.MapProjetoEndpoints();

app.Run();
return 0;
