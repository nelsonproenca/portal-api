using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using PortalApi.Api.Filters;
using PortalApi.Application.Crm;

namespace PortalApi.Api.Endpoints;

/// <summary>
/// CRM do site institucional (leads, enriquecimento, playground, colaboradores, contatos e agendamentos).
/// Três tipos de chamador: o visitante do site (público, com rate limit e corpo pequeno), o admin (cookie +
/// header CSRF nas escritas) e o n8n (header X-Webhook-Secret). Substitui o acesso direto do navegador ao
/// Supabase com a chave anon.
/// </summary>
public static class CrmEndpoints
{
    public const string PublicWritePolicy = "public-write";
    public const string PublicReadPolicy = "public-read";
    public const string AdminPolicy = "Admin";

    private const long MaxCorpoPublico = 32 * 1024;
    private const long MaxCorpoN8n = 256 * 1024;

    private static IResult ToResult<T>(CrmResult<T> r, Func<T, IResult>? ok = null) => r.Erro switch
    {
        CrmErro.Nenhum => ok is null ? Results.Ok(r.Valor) : ok(r.Valor!),
        CrmErro.Invalido => Results.BadRequest(new { error = r.Mensagem }),
        CrmErro.Conflito => Results.Conflict(new { error = r.Mensagem }),
        _ => Results.NotFound(),
    };

    /// <summary>Política "Admin" e os rate limits públicos. Fica aqui (e não no Program) para os testes de
    /// endpoint usarem exatamente as mesmas regras de produção.</summary>
    public static IServiceCollection AddCrmPolicies(this IServiceCollection services)
    {
        // Só a sessão do admin (cookie com papel admin). O papel é checado de verdade para que um cookie de
        // outro perfil (portal do cliente, fase 2) nunca abra esses endpoints.
        services.AddAuthorizationBuilder().AddPolicy(AdminPolicy, policy => policy
            .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme)
            .RequireRole("admin"));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Formulários públicos (lead, enriquecer, playground): cada envio dispara um fluxo de IA no n8n.
            options.AddPolicy(PublicWritePolicy, ctx => PorIp(ctx, permitLimit: 10));

            // Polling do resultado e vitrine: mais folgado, mas limitado.
            options.AddPolicy(PublicReadPolicy, ctx => PorIp(ctx, permitLimit: 120));
        });

        return services;
    }

    private static RateLimitPartition<string> PorIp(HttpContext ctx, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });

    public static void MapCrmEndpoints(this WebApplication app)
    {
        MapPublicos(app);
        MapAdmin(app);
        MapN8n(app);
    }

    // ─── Visitante do site ────────────────────────────────────────────────────

    private static void MapPublicos(WebApplication app)
    {
        app.MapPost("/leads", async (CriarLeadRequest request, LeadService service, CancellationToken ct) =>
            ToResult(await service.CriarAsync(request, ct), c => Results.Created($"/leads/{c.Id}", c)))
        .AllowAnonymous()
        .RequireRateLimiting(PublicWritePolicy)
        .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxCorpoPublico));

        app.MapPost("/enrich", async (CriarEnrichRequest request, AnaliseService service, CancellationToken ct) =>
            ToResult(await service.CriarEnrichAsync(request, ct), c => Results.Created($"/enrich/{c.Id}", c)))
        .AllowAnonymous()
        .RequireRateLimiting(PublicWritePolicy)
        .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxCorpoPublico));

        // Polling do resultado. O id é um Guid aleatório devolvido só a quem criou; a resposta não traz o input.
        app.MapGet("/enrich/{id:guid}", async (Guid id, AnaliseService service, CancellationToken ct) =>
        {
            var r = await service.ObterEnrichAsync(id, ct);
            return r is null ? Results.NotFound() : Results.Ok(r);
        })
        .AllowAnonymous()
        .RequireRateLimiting(PublicReadPolicy);

        app.MapPost("/playground", async (CriarPlaygroundRequest request, AnaliseService service, CancellationToken ct) =>
            ToResult(await service.CriarPlaygroundAsync(request, ct), c => Results.Created($"/playground/{c.Id}", c)))
        .AllowAnonymous()
        .RequireRateLimiting(PublicWritePolicy)
        .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxCorpoPublico));

        app.MapGet("/playground/{id:guid}", async (Guid id, AnaliseService service, CancellationToken ct) =>
        {
            var r = await service.ObterPlaygroundAsync(id, ct);
            return r is null ? Results.NotFound() : Results.Ok(r);
        })
        .AllowAnonymous()
        .RequireRateLimiting(PublicReadPolicy);

        // Landing e página de colaboradores (a vitrine já era pública no Supabase).
        app.MapGet("/colaboradores", async (CadastroCrmService service, CancellationToken ct) =>
            Results.Ok(await service.ListarColaboradoresAsync(ct)))
        .AllowAnonymous()
        .RequireRateLimiting(PublicReadPolicy);
    }

    // ─── Admin ────────────────────────────────────────────────────────────────

    private static void MapAdmin(WebApplication app)
    {
        // Colaboradores
        app.MapPost("/colaboradores", async (UpsertColaboradorRequest request, CadastroCrmService service, CancellationToken ct) =>
            ToResult(await service.CriarColaboradorAsync(request, ct), c => Results.Created($"/colaboradores/{c.Id}", c)))
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization(AdminPolicy);

        app.MapPut("/colaboradores/{id:guid}", async (Guid id, UpsertColaboradorRequest request, CadastroCrmService service, CancellationToken ct) =>
            ToResult(await service.AtualizarColaboradorAsync(id, request, ct)))
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization(AdminPolicy);

        app.MapDelete("/colaboradores/{id:guid}", async (Guid id, CadastroCrmService service, CancellationToken ct) =>
            await service.ExcluirColaboradorAsync(id, ct) ? Results.NoContent() : Results.NotFound())
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization(AdminPolicy);

        // Contatos de clientes
        var contatos = app.MapGroup("/contatos-clientes").RequireAuthorization(AdminPolicy);

        contatos.MapGet("/", async (Guid? clienteId, CadastroCrmService service, CancellationToken ct) =>
            Results.Ok(await service.ListarContatosAsync(clienteId, ct)));

        contatos.MapPost("/", async (UpsertContatoClienteRequest request, CadastroCrmService service, CancellationToken ct) =>
            ToResult(await service.CriarContatoAsync(request, ct), c => Results.Created($"/contatos-clientes/{c.Id}", c)))
        .AddEndpointFilter<AdminCsrfEndpointFilter>();

        contatos.MapPut("/{id:guid}", async (Guid id, UpsertContatoClienteRequest request, CadastroCrmService service, CancellationToken ct) =>
            ToResult(await service.AtualizarContatoAsync(id, request, ct)))
        .AddEndpointFilter<AdminCsrfEndpointFilter>();

        contatos.MapDelete("/{id:guid}", async (Guid id, CadastroCrmService service, CancellationToken ct) =>
            await service.ExcluirContatoAsync(id, ct) ? Results.NoContent() : Results.NotFound())
        .AddEndpointFilter<AdminCsrfEndpointFilter>();

        // Leads
        app.MapGet("/leads", async (LeadService service, CancellationToken ct) =>
            Results.Ok(await service.ListarAsync(ct)))
        .RequireAuthorization(AdminPolicy);

        app.MapPatch("/leads/{id:guid}/visto", async (Guid id, MarcarLeadVistoRequest request, LeadService service, CancellationToken ct) =>
            ToResult(await service.MarcarVistoAsync(id, request.Visto, ct)))
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization(AdminPolicy);

        app.MapDelete("/leads/{id:guid}", async (Guid id, LeadService service, CancellationToken ct) =>
            await service.ExcluirAsync(id, ct) ? Results.NoContent() : Results.NotFound())
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization(AdminPolicy);

        // Agendamentos
        app.MapGet("/agendamentos", async (AgendamentoService service, CancellationToken ct) =>
            Results.Ok(await service.ListarAsync(ct)))
        .RequireAuthorization(AdminPolicy);

        app.MapPatch("/agendamentos/{id:guid}", async (Guid id, AtualizarAgendamentoRequest request, AgendamentoService service, CancellationToken ct) =>
            ToResult(await service.AtualizarAsync(id, request, ct)))
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization(AdminPolicy);
    }

    // ─── n8n (callbacks e agendamento do Instagram) ───────────────────────────

    private static void MapN8n(WebApplication app)
    {
        var n8n = app.MapGroup("")
            .AllowAnonymous()
            .AddEndpointFilter<N8nSecretEndpointFilter>()
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxCorpoN8n));

        n8n.MapPatch("/leads/{id:guid}/analise", async (Guid id, ResultadoAnaliseLeadRequest request, LeadService service, CancellationToken ct) =>
            ToResult(await service.RegistrarAnaliseAsync(id, request.AnaliseIa, ct)));

        n8n.MapPatch("/playground/{id:guid}/resultado", async (Guid id, ResultadoPlaygroundRequest request, AnaliseService service, CancellationToken ct) =>
            ToResult(await service.RegistrarPlaygroundAsync(id, request.OutputIa, ct)));

        n8n.MapPatch("/enrich/{id:guid}/resultado", async (Guid id, ResultadoEnrichRequest request, AnaliseService service, CancellationToken ct) =>
            ToResult(await service.RegistrarEnrichAsync(id, request.OutputAi, ct)));

        n8n.MapPost("/agendamentos", async (CriarAgendamentoRequest request, AgendamentoService service, CancellationToken ct) =>
            ToResult(await service.CriarAsync(request, ct), a => Results.Created($"/agendamentos/{a.Id}", a)));
    }
}
