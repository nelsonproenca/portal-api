using PortalApi.Api.Extensions;
using PortalApi.Api.Filters;
using PortalApi.Application.Portal;
using PortalApi.Application.Projetos;

namespace PortalApi.Api.Endpoints;

public static class ProjetoEndpoints
{
    public static void MapProjetoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/projetos");

        // Admin — lista completa (qualquer visibilidade/status) e detalhe por id.
        group.MapGet("/", async (ProjetoService service, CancellationToken ct) =>
            Results.Ok(await service.ListAllAsync(ct)))
        .RequireAuthorization();

        // Admin OU cliente dono do projeto (ticket #19) — mesma regra de
        // `scope_projetos_select_by_client_identity.sql`: admin vê qualquer um,
        // cliente só o seu (senão 404 — não revela que o projeto existe).
        group.MapGet("/{id:guid}", async (Guid id, HttpContext http, ProjetoService service, ClientAccessService access, CancellationToken ct) =>
        {
            if (!http.User.IsAdmin())
            {
                var email = http.User.GetEmail();
                if (email is null || !await access.OwnsProjetoAsync(email, id, ct))
                    return Results.NotFound();
            }

            var projeto = await service.GetAsync(id, ct);
            return projeto is null ? Results.NotFound() : Results.Ok(projeto);
        })
        .RequireAuthorization("AdminOrClient");

        // Cliente (portal): só os projetos do próprio e-mail. O dono é resolvido no
        // servidor a partir da sessão, nunca de um id vindo do cliente.
        group.MapGet("/mine", async (HttpContext http, ClientAccessService access, CancellationToken ct) =>
        {
            var email = http.User.GetEmail();
            if (email is null) return Results.Unauthorized();

            return Results.Ok(await access.ListProjetosDoEmailAsync(email, ct));
        })
        .RequireAuthorization("AdminOrClient");

        group.MapPost("/", async (UpsertProjetoRequest request, ProjetoService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/projetos/{created.Id}", created);
        })
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization();

        // Público — só projetos com visibilidade=publico. clienteId opcional filtra
        // pra vitrine de projetos de um cliente específico (link a partir do card
        // dele em /clientes — ver Clientes.tsx).
        group.MapGet("/public", async (string? status, Guid? clienteId, ProjetoService service, CancellationToken ct) =>
            Results.Ok(await service.ListPublicosAsync(status, clienteId, ct)))
        .AllowAnonymous();

        group.MapGet("/public/{id:guid}", async (Guid id, ProjetoService service, CancellationToken ct) =>
        {
            var projeto = await service.GetPublicoAsync(id, ct);
            return projeto is null ? Results.NotFound() : Results.Ok(projeto);
        })
        .AllowAnonymous();
    }
}
