using PortalApi.Api.Extensions;
using PortalApi.Api.Filters;
using PortalApi.Application.Clientes;

namespace PortalApi.Api.Endpoints;

public static class ClienteEndpoints
{
    public static void MapClienteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/clientes");

        // Admin — lista completa, sem filtro de status.
        group.MapGet("/", async (ClienteService service, CancellationToken ct) =>
            Results.Ok(await service.ListAllAsync(ct)))
        .RequireAuthorization();

        group.MapPost("/", async (UpsertClienteRequest request, ClienteService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/clientes/{created.Id}", created);
        })
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization();

        group.MapPut("/{id:guid}", async (Guid id, UpsertClienteRequest request, ClienteService service, CancellationToken ct) =>
        {
            var updated = await service.UpdateAsync(id, request, ct);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        })
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization();

        // Público — só os "ativo", pra vitrine de clientes/parceiros (sem auth).
        group.MapGet("/public", async (ClienteService service, CancellationToken ct) =>
            Results.Ok(await service.ListByStatusAsync("ativo", ct)))
        .AllowAnonymous();

        // Cliente (portal): resolve o próprio registro de cliente a partir do e-mail
        // da sessão (cookie do login por link), nunca de um e-mail enviado pelo front.
        group.MapGet("/me", async (HttpContext http, ClienteService service, CancellationToken ct) =>
        {
            var email = http.User.GetEmail();
            if (email is null) return Results.Unauthorized();

            var cliente = await service.GetByEmailAsync(email, ct);
            return cliente is null ? Results.NotFound() : Results.Ok(cliente);
        })
        .RequireAuthorization("AdminOrClient");
    }
}
