using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using PortalApi.Application.Auth;

namespace PortalApi.Api.Endpoints;

/// <summary>
/// Login do portal do cliente por link de e-mail (substitui o magic link do Supabase Auth). A sessão é um
/// cookie próprio (<c>portal_cliente</c>, HttpOnly), separado do cookie do admin: os dois podem coexistir no
/// mesmo navegador sem um derrubar o outro.
/// </summary>
public static class ClienteAuthEndpoints
{
    public const string Scheme = "PortalCliente";
    public const string Policy = "Cliente";
    public const string SolicitarRateLimit = "cliente-solicitar";
    public const string VerificarRateLimit = "cliente-verificar";

    public static AuthenticationBuilder AddClienteCookie(this AuthenticationBuilder auth) =>
        auth.AddCookie(Scheme, options =>
        {
            options.Cookie.Name = "portal_cliente";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromHours(12);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = ctx => { ctx.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
        });

    public static IServiceCollection AddClienteAuthPolicies(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder().AddPolicy(Policy, p => p
            .AddAuthenticationSchemes(Scheme)
            .RequireRole("cliente"));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Pedir o link dispara um e-mail: limite curto por IP (e há outro por e-mail no serviço).
            options.AddPolicy(SolicitarRateLimit, ctx => PorIp(ctx, permitLimit: 5, minutos: 15));
            // Trocar o token por sessão: token de 256 bits não se adivinha, mas o limite barra abuso.
            options.AddPolicy(VerificarRateLimit, ctx => PorIp(ctx, permitLimit: 30, minutos: 15));
        });

        return services;
    }

    private static RateLimitPartition<string> PorIp(HttpContext ctx, int permitLimit, int minutos) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(minutos),
                QueueLimit = 0,
            });

    public static void MapClienteAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth/cliente");

        // Resposta idêntica exista o e-mail ou não: não dá para descobrir quem é cliente.
        group.MapPost("/solicitar", async (SolicitarRequest request, ClienteLoginService service, CancellationToken ct) =>
        {
            await service.SolicitarAsync(request.Email, ct);
            return Results.Ok(new { ok = true });
        })
        .AllowAnonymous()
        .RequireRateLimiting(SolicitarRateLimit)
        .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(4 * 1024));

        // POST (e não GET) para que scanners de link de e-mail não consumam o token só por abrir a URL: quem
        // chama é a página /portal/entrar do front, depois que o cliente a abre.
        group.MapPost("/verificar", async (VerificarRequest request, ClienteLoginService service, HttpContext http, CancellationToken ct) =>
        {
            var email = await service.VerificarAsync(request.Token, ct);
            if (email is null) return Results.Json(new { error = "Link inválido ou expirado. Peça um novo acesso." }, statusCode: StatusCodes.Status401Unauthorized);

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Email, email), new Claim(ClaimTypes.Role, "cliente")], Scheme);
            await http.SignInAsync(Scheme, new ClaimsPrincipal(identity), new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12),
            });
            return Results.Ok(new { email });
        })
        .AllowAnonymous()
        .RequireRateLimiting(VerificarRateLimit)
        .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(4 * 1024));

        group.MapPost("/logout", async (HttpContext http) =>
        {
            await http.SignOutAsync(Scheme);
            return Results.Ok();
        })
        .AllowAnonymous();

        group.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new { email = user.FindFirstValue(ClaimTypes.Email) }))
        .RequireAuthorization(Policy);
    }

    public record SolicitarRequest(string? Email);
    public record VerificarRequest(string? Token);
}
