using PortalApi.Application.Storage;
using PortalApi.Domain.Entities;
using PortalApi.Domain.Enums;

namespace PortalApi.Application.Artefatos;

public class ArtefatoService(
    IArtefatoRepository repository,
    IFileStorageService storage,
    IDownloadTokenService tokenService)
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10MB

    public static readonly HashSet<string> AllowedContentTypePrefixes = ["image/"];
    public static readonly HashSet<string> AllowedContentTypes =
    [
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    ];

    public static bool IsContentTypeAllowed(string contentType) =>
        AllowedContentTypes.Contains(contentType) ||
        AllowedContentTypePrefixes.Any(contentType.StartsWith);

    private static ArtefatoDto ToDto(Artefato a) =>
        new(a.Id, a.ProjetoId, a.EtapaId, a.Nome, a.Url, a.Tipo.ToString(), a.UploadedBy, a.CreatedAt);

    public async Task<IReadOnlyList<ArtefatoDto>> ListByProjetoAsync(Guid projetoId, CancellationToken ct) =>
        (await repository.GetByProjetoIdAsync(projetoId, ct)).Select(ToDto).ToList();

    public async Task<ArtefatoDto> CreateLinkAsync(CreateArtefatoLinkRequest request, CancellationToken ct)
    {
        var artefato = new Artefato
        {
            Id = Guid.NewGuid(),
            ProjetoId = request.ProjetoId,
            EtapaId = request.EtapaId,
            Nome = request.Nome,
            Url = request.LinkUrl,
            Tipo = ArtefatoTipo.link,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await repository.AddAsync(artefato, ct);
        return ToDto(artefato);
    }

    public async Task<ArtefatoDto> CreateArquivoAsync(
        Guid projetoId, Guid? etapaId, string nome, Stream fileContent, string fileName, CancellationToken ct)
    {
        var relativePath = $"{projetoId}/{Guid.NewGuid()}-{fileName}";
        var savedPath = await storage.SaveAsync(relativePath, fileContent, ct);

        var artefato = new Artefato
        {
            Id = Guid.NewGuid(),
            ProjetoId = projetoId,
            EtapaId = etapaId,
            Nome = nome,
            Url = savedPath,
            Tipo = ArtefatoTipo.arquivo,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await repository.AddAsync(artefato, ct);
        return ToDto(artefato);
    }

    /// <summary>Pra tipo=link retorna a própria URL; pra tipo=arquivo, gera um token de
    /// download de curta duração (mesmo princípio do MediaMtxJwtService).</summary>
    public async Task<string?> GetAccessUrlAsync(Guid id, CancellationToken ct)
    {
        var artefato = await repository.GetByIdAsync(id, ct);
        if (artefato is null) return null;
        if (artefato.Tipo == ArtefatoTipo.link) return artefato.Url;

        var (token, _) = tokenService.Generate(artefato.Id);
        return $"/artefatos/{artefato.Id}/download?token={Uri.EscapeDataString(token)}";
    }

    public async Task<(Stream Content, string FileName)?> DownloadAsync(Guid id, string token, CancellationToken ct)
    {
        var artefatoId = tokenService.Validate(token);
        if (artefatoId is null || artefatoId.Value != id) return null;

        var artefato = await repository.GetByIdAsync(id, ct);
        if (artefato is null || artefato.Tipo != ArtefatoTipo.arquivo) return null;

        var stream = await storage.OpenReadAsync(artefato.Url, ct);
        if (stream is null) return null;

        // Nome amigável pro download (o que o admin digitou), preservando a extensão
        // real do arquivo salvo (o path guarda "{uuid}-{nome-original-do-arquivo}").
        var extensao = Path.GetExtension(artefato.Url);
        var fileName = Path.HasExtension(artefato.Nome) ? artefato.Nome : artefato.Nome + extensao;
        return (stream, fileName);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var artefato = await repository.GetByIdAsync(id, ct);
        if (artefato is null) return false;

        if (artefato.Tipo == ArtefatoTipo.arquivo) storage.Delete(artefato.Url);
        await repository.DeleteAsync(artefato, ct);
        return true;
    }
}
