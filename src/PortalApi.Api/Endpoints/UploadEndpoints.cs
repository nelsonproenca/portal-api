using Microsoft.Extensions.Options;
using PortalApi.Api.Filters;
using PortalApi.Application.Crm;
using PortalApi.Application.Uploads;
using PortalApi.Infrastructure.Storage;

namespace PortalApi.Api.Endpoints;

/// <summary>Imagens públicas do site (logos de clientes, fotos de colaboradores, convites). Envio e remoção só do
/// admin; leitura pública (as imagens aparecem no site para qualquer visitante).</summary>
public static class UploadEndpoints
{
    private const long MaxCorpo = UploadService.TamanhoMaximo + 512 * 1024; // imagem + cabeçalho do multipart

    public static void MapUploadEndpoints(this WebApplication app)
    {
        app.MapPost("/uploads/{pasta}", async (string pasta, HttpRequest http, UploadService service, IOptions<StorageOptions> storage, CancellationToken ct) =>
        {
            if (!http.HasFormContentType) return Results.BadRequest(new { error = "Esperado multipart/form-data." });

            var form = await http.ReadFormAsync(ct);
            var arquivo = form.Files.GetFile("file");
            if (arquivo is null) return Results.BadRequest(new { error = "Campo obrigatório: file." });

            await using var conteudo = arquivo.OpenReadStream();
            var r = await service.SalvarAsync(pasta, conteudo, ct);
            if (!r.Ok) return Results.BadRequest(new { error = r.Mensagem });

            var u = r.Valor!;
            return Results.Created($"/uploads/{u.Path}", new
            {
                pasta = u.Pasta,
                arquivo = u.Arquivo,
                path = u.Path,
                url = $"{storage.Value.UploadsPublicBasePath.TrimEnd('/')}/{u.Path}",
            });
        })
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization(CrmEndpoints.AdminPolicy)
        .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxCorpo));

        // Convite com nome fixo (convite-<pessoa>.png), regravado a cada novo QR Code.
        app.MapPut("/uploads/convites/{arquivo}", async (string arquivo, HttpRequest http, UploadService service, IOptions<StorageOptions> storage, CancellationToken ct) =>
        {
            if (!http.HasFormContentType) return Results.BadRequest(new { error = "Esperado multipart/form-data." });

            var form = await http.ReadFormAsync(ct);
            var enviado = form.Files.GetFile("file");
            if (enviado is null) return Results.BadRequest(new { error = "Campo obrigatório: file." });

            await using var conteudo = enviado.OpenReadStream();
            var r = await service.SalvarConviteAsync(arquivo, conteudo, ct);
            if (!r.Ok) return Results.BadRequest(new { error = r.Mensagem });

            var u = r.Valor!;
            return Results.Ok(new
            {
                pasta = u.Pasta,
                arquivo = u.Arquivo,
                path = u.Path,
                url = $"{storage.Value.UploadsPublicBasePath.TrimEnd('/')}/{u.Path}",
            });
        })
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization(CrmEndpoints.AdminPolicy)
        .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(MaxCorpo));

        app.MapGet("/uploads/{pasta}/{arquivo}", (string pasta, string arquivo, HttpContext http, UploadService service) =>
        {
            var aberto = service.Abrir(pasta, arquivo);
            if (aberto is null) return Results.NotFound();

            // Nome único por upload: a imagem não muda, então o navegador pode guardar. nosniff impede o
            // navegador de tratar o arquivo como outro tipo.
            http.Response.Headers.CacheControl = "public, max-age=86400";
            http.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(aberto.Conteudo, aberto.ContentType);
        })
        .AllowAnonymous();

        app.MapDelete("/uploads/{pasta}/{arquivo}", (string pasta, string arquivo, UploadService service) =>
            service.Excluir(pasta, arquivo) ? Results.NoContent() : Results.NotFound())
        .AddEndpointFilter<AdminCsrfEndpointFilter>()
        .RequireAuthorization(CrmEndpoints.AdminPolicy);

        // Landing: lista as imagens de convite para o gerador.
        app.MapGet("/convites", (UploadService service, IOptions<StorageOptions> storage) =>
        {
            var baseUrl = storage.Value.UploadsPublicBasePath.TrimEnd('/');
            return Results.Ok(service.Listar("convites")
                .Select(a => new { arquivo = a.Arquivo, url = $"{baseUrl}/convites/{a.Arquivo}", tamanho = a.Tamanho, atualizadoEm = a.AtualizadoEm }));
        })
        .AllowAnonymous()
        .RequireRateLimiting(CrmEndpoints.PublicReadPolicy);
    }
}
