using PortalApi.Api.Filters;
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

        group.MapGet("/{id:guid}", async (Guid id, ProjetoService service, CancellationToken ct) =>
        {
            var projeto = await service.GetAsync(id, ct);
            return projeto is null ? Results.NotFound() : Results.Ok(projeto);
        })
        .RequireAuthorization();

        group.MapPost("/", async (UpsertProjetoRequest request, ProjetoService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/projetos/{created.Id}", created);
        })
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization();

        // Público — só projetos com visibilidade=publico.
        group.MapGet("/public", async (string? status, ProjetoService service, CancellationToken ct) =>
            Results.Ok(await service.ListPublicosAsync(status, ct)))
        .AllowAnonymous();

        group.MapGet("/public/{id:guid}", async (Guid id, ProjetoService service, CancellationToken ct) =>
        {
            var projeto = await service.GetPublicoAsync(id, ct);
            return projeto is null ? Results.NotFound() : Results.Ok(projeto);
        })
        .AllowAnonymous();
    }
}
