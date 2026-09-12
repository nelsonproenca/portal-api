using PortalApi.Api.Filters;
using PortalApi.Application.Etapas;

namespace PortalApi.Api.Endpoints;

public static class EtapaEndpoints
{
    public static void MapEtapaEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/etapas");

        group.MapGet("/", async (Guid projetoId, EtapaService service, CancellationToken ct) =>
            Results.Ok(await service.ListByProjetoAsync(projetoId, ct)))
        .RequireAuthorization();

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
