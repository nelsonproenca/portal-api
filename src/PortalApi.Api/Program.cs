using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PortalApi.Api;
using PortalApi.Api.Endpoints;
using PortalApi.Infrastructure.Data;
using PortalApi.Infrastructure.DependencyInjection;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ─── Auth ─────────────────────────────────────────────────────────────────────

// Admin: cookie httpOnly (ver ticket #14) — continua o esquema default (usado
// por login/logout/me e por todo endpoint de escrita do admin).
var authBuilder = builder.Services
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

// Cliente (portal do cliente): cookie próprio, emitido depois que o cliente abre o link enviado por e-mail
// (ver ClienteAuthEndpoints). Substitui o JWT do Supabase Auth.
authBuilder.AddClienteCookie();

builder.Services.AddAuthorization(options =>
{
    // Endpoints de leitura que tanto o admin quanto um cliente autenticado podem
    // chamar — a distinção de "o que cada um pode ver" é feita dentro do handler
    // (ClientAccessService), não aqui.
    options.AddPolicy("AdminOrClient", policy => policy
        .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme, ClienteAuthEndpoints.Scheme)
        .RequireAuthenticatedUser());
});

builder.Services.AddClienteAuthPolicies();

// Política "Admin" e rate limits dos formulários públicos do CRM (ver CrmEndpoints).
builder.Services.AddCrmPolicies();

// ─── IP real atrás do Caddy ───────────────────────────────────────────────────

// O container só é alcançável pelas redes Docker (o Caddy é o único ponto público), então confiar no
// X-Forwarded-For é seguro aqui. Sem isso, o rate limit por IP veria só o IP do Caddy e todos os visitantes
// dividiriam o mesmo limite.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

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

if (args.Length > 0 && args[0] == "import-crm")
    return await ImportCrmCommand.RunAsync(args, app.Services);

if (args.Length > 0 && args[0] == "import-uploads")
    return await ImportUploadsCommand.RunAsync(args, app.Services);

// ─── Middleware pipeline ──────────────────────────────────────────────────────

app.UseForwardedHeaders();
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
app.MapEtapaEndpoints();
app.MapArtefatoEndpoints();
app.MapPedidoEndpoints();
app.MapCrmEndpoints();
app.MapClienteAuthEndpoints();
app.MapUploadEndpoints();

app.Run();
return 0;
