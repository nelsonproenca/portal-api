using PortalApi.Api.Extensions;
using PortalApi.Api.Filters;
using PortalApi.Application.Artefatos;
using PortalApi.Application.Portal;

namespace PortalApi.Api.Endpoints;

public static class ArtefatoEndpoints
{
    public static void MapArtefatoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/artefatos");

        // Admin OU cliente dono do projeto (ticket #19).
        group.MapGet("/", async (Guid projetoId, HttpContext http, ArtefatoService service, ClientAccessService access, CancellationToken ct) =>
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

        group.MapPost("/link", async (CreateArtefatoLinkRequest request, ArtefatoService service, CancellationToken ct) =>
        {
            var created = await service.CreateLinkAsync(request, ct);
            return Results.Created($"/artefatos/{created.Id}", created);
        })
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization();

        group.MapPost("/upload", async (HttpRequest httpRequest, ArtefatoService service, CancellationToken ct) =>
        {
            if (!httpRequest.HasFormContentType) return Results.BadRequest("Esperado multipart/form-data.");

            var form = await httpRequest.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            var projetoIdRaw = form["projetoId"].ToString();
            var etapaIdRaw = form["etapaId"].ToString();
            var nome = form["nome"].ToString();

            if (file is null || string.IsNullOrWhiteSpace(nome) || !Guid.TryParse(projetoIdRaw, out var projetoId))
                return Results.BadRequest("Campos obrigatórios: file, nome, projetoId.");

            Guid? etapaId = Guid.TryParse(etapaIdRaw, out var parsedEtapaId) ? parsedEtapaId : null;

            if (file.Length > ArtefatoService.MaxFileSizeBytes)
                return Results.BadRequest($"Arquivo excede o limite de {ArtefatoService.MaxFileSizeBytes / 1024 / 1024}MB.");

            if (!ArtefatoService.IsContentTypeAllowed(file.ContentType))
                return Results.BadRequest("Tipo de arquivo não permitido. Envie uma imagem, PDF ou documento (Word).");

            await using var stream = file.OpenReadStream();
            var created = await service.CreateArquivoAsync(projetoId, etapaId, nome, stream, file.FileName, ct);
            return Results.Created($"/artefatos/{created.Id}", created);
        })
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization();

        // Emite a URL de acesso (link direto, ou path com token assinado pra arquivo).
        // Admin OU cliente dono do projeto do artefato (ticket #19).
        group.MapGet("/{id:guid}/signed-url", async (Guid id, HttpContext http, ArtefatoService service, ClientAccessService access, CancellationToken ct) =>
        {
            if (!http.User.IsAdmin())
            {
                var email = http.User.GetEmail();
                if (email is null || !await access.OwnsArtefatoAsync(email, id, ct))
                    return Results.NotFound();
            }

            var url = await service.GetAccessUrlAsync(id, ct);
            return url is null ? Results.NotFound() : Results.Ok(new { url });
        })
        .RequireAuthorization("AdminOrClient");

        // Download em si — a autorização é o token assinado (curta duração), não a
        // sessão do admin, porque signed URLs precisam funcionar numa nova aba/download
        // direto do navegador, mesmo padrão do stream do MediaMTX no Watchtower.
        group.MapGet("/{id:guid}/download", async (Guid id, string token, ArtefatoService service, CancellationToken ct) =>
        {
            var result = await service.DownloadAsync(id, token, ct);
            if (result is null) return Results.NotFound();

            var (content, fileName) = result.Value;
            return Results.File(content, "application/octet-stream", fileName);
        })
        .AllowAnonymous();

        group.MapDelete("/{id:guid}", async (Guid id, ArtefatoService service, CancellationToken ct) =>
            await service.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound())
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization();
    }
}
