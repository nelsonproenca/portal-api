using PortalApi.Api.Extensions;
using PortalApi.Api.Filters;
using PortalApi.Application.Artefatos;
using PortalApi.Application.Pedidos;
using PortalApi.Application.Portal;

namespace PortalApi.Api.Endpoints;

public static class PedidoEndpoints
{
    public static void MapPedidoEndpoints(this WebApplication app)
    {
        var pedidosGroup = app.MapGroup("/pedidos");

        // Admin OU cliente dono do projeto (ticket #20) — mesma regra usada em
        // etapas/artefatos.
        pedidosGroup.MapGet("/", async (Guid projetoId, HttpContext http, PedidoService service, ClientAccessService access, CancellationToken ct) =>
        {
            if (!http.User.IsAdmin())
            {
                var email = http.User.GetEmail();
                if (email is null || !await access.OwnsProjetoAsync(email, projetoId, ct))
                    return Results.Ok(Array.Empty<object>());
            }

            return Results.Ok(await service.ListByProjetoAsync(projetoId, ct));
        })
        .RequireAuthorization("AdminOrClient");

        // Só admin cria pedidos.
        pedidosGroup.MapPost("/", async (CreatePedidoRequest request, PedidoService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/pedidos/{created.Id}", created);
        })
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization();

        // Cliente dono decide uma "validacao" (aprovar/pedir ajuste) — é o cliente
        // que valida a entrega, não o admin (ver GestaoPedidos.tsx / PortalProjetoDetalhePage.tsx
        // no frontend: o formulário de decisão vive na tela do cliente). Admin
        // também pode chamar (útil pra correção manual), mas a UI não expõe isso pra ele.
        pedidosGroup.MapPost("/{id:guid}/aprovar", async (Guid id, DecisaoPedidoRequest request, HttpContext http, PedidoService service, ClientAccessService access, CancellationToken ct) =>
        {
            if (!http.User.IsAdmin())
            {
                var email = http.User.GetEmail();
                if (email is null || !await access.OwnsPedidoAsync(email, id, ct)) return Results.NotFound();
            }

            var resposta = await service.AprovarAsync(id, request.Comentario, ct);
            return resposta is null ? Results.NotFound() : Results.Ok(resposta);
        })
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization("AdminOrClient");

        pedidosGroup.MapPost("/{id:guid}/ajustar", async (Guid id, DecisaoPedidoRequest request, HttpContext http, PedidoService service, ClientAccessService access, CancellationToken ct) =>
        {
            if (!http.User.IsAdmin())
            {
                var email = http.User.GetEmail();
                if (email is null || !await access.OwnsPedidoAsync(email, id, ct)) return Results.NotFound();
            }

            var (dto, error) = await service.AjustarAsync(id, request.Comentario ?? "", ct);
            return error is not null ? Results.BadRequest(error) : Results.Ok(dto);
        })
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization("AdminOrClient");

        var respostasGroup = app.MapGroup("/pedido-respostas");

        respostasGroup.MapGet("/", async (Guid pedidoId, HttpContext http, PedidoService service, ClientAccessService access, CancellationToken ct) =>
        {
            if (!http.User.IsAdmin())
            {
                var email = http.User.GetEmail();
                if (email is null || !await access.OwnsPedidoAsync(email, pedidoId, ct))
                    return Results.Ok(Array.Empty<object>());
            }

            return Results.Ok(await service.ListRespostasAsync(pedidoId, ct));
        })
        .RequireAuthorization("AdminOrClient");

        // Cliente dono responde uma "pergunta" (texto + arquivo opcional). Admin
        // também pode (mesma lógica de "AdminOrClient" já usada em todo o resto).
        respostasGroup.MapPost("/", async (HttpRequest httpRequest, HttpContext http, PedidoService service, ClientAccessService access, CancellationToken ct) =>
        {
            if (!httpRequest.HasFormContentType) return Results.BadRequest("Esperado multipart/form-data.");

            var form = await httpRequest.ReadFormAsync(ct);
            var pedidoIdRaw = form["pedidoId"].ToString();
            var projetoIdRaw = form["projetoId"].ToString();
            var texto = form["texto"].ToString();
            var file = form.Files.GetFile("file");

            if (!Guid.TryParse(pedidoIdRaw, out var pedidoId) || !Guid.TryParse(projetoIdRaw, out var projetoId) || string.IsNullOrWhiteSpace(texto))
                return Results.BadRequest("Campos obrigatórios: pedidoId, projetoId, texto.");

            if (!http.User.IsAdmin())
            {
                var email = http.User.GetEmail();
                if (email is null || !await access.OwnsPedidoAsync(email, pedidoId, ct)) return Results.NotFound();
            }

            if (file is not null && file.Length > ArtefatoService.MaxFileSizeBytes)
                return Results.BadRequest($"Arquivo excede o limite de {ArtefatoService.MaxFileSizeBytes / 1024 / 1024}MB.");

            await using var stream = file?.OpenReadStream();
            var (dto, error) = await service.SubmitRespostaAsync(pedidoId, projetoId, texto, stream, file?.FileName, file?.ContentType, ct);
            return error is not null ? Results.BadRequest(error) : Results.Created($"/pedido-respostas/{dto!.Id}", dto);
        })
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization("AdminOrClient");
    }
}
