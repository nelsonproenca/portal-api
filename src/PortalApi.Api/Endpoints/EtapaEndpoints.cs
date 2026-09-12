using PortalApi.Api.Extensions;
using PortalApi.Api.Filters;
using PortalApi.Application.Etapas;
using PortalApi.Application.Portal;

namespace PortalApi.Api.Endpoints;

public static class EtapaEndpoints
{
    public static void MapEtapaEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/etapas");

        // Admin OU cliente dono do projeto (ticket #19) — mesma regra de
        // `scope_etapas_artefatos_select_by_client_identity.sql`.
        group.MapGet("/", async (Guid projetoId, HttpContext http, EtapaService service, ClientAccessService access, CancellationToken ct) =>
        {
            if (!http.User.IsAdmin())
            {
                var email = http.User.GetEmail();
                if (email is null || !await access.OwnsProjetoAsync(email, projetoId, ct))
                    return Results.Ok(Array.Empty<object>()); // mesmo efeito da RLS: lista vazia, não erro
            }

            return Results.Ok(await service.ListByProjetoAsync(projetoId, ct));
        })
        .RequireAuthorization("AdminOrClient");

        group.MapPost("/", async (CreateEtapaRequest request, EtapaService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/etapas/{created.Id}", created);
        })
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization();

        group.MapPut("/{id:guid}", async (Guid id, UpdateEtapaRequest request, EtapaService service, CancellationToken ct) =>
        {
            var updated = await service.UpdateAsync(id, request, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization();

        group.MapDelete("/{id:guid}", async (Guid id, EtapaService service, CancellationToken ct) =>
            await service.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound())
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization();
    }
}
