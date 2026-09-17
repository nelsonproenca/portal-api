using PortalApi.Domain.Entities;
using PortalApi.Domain.Enums;

namespace PortalApi.Application.Projetos;

public class ProjetoService(IProjetoRepository repository)
{
    private static ProjetoDto ToDto(Projeto p) => new(
        p.Id, p.ClienteId, p.Nome, p.Categoria,
        p.StatusPublico.ToString(), p.Visibilidade.ToString(),
        p.ImagemCapaUrl, p.LinkUrl, p.CreatedAt);

    public async Task<IReadOnlyList<ProjetoDto>> ListAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<ProjetoDto>> ListPublicosAsync(string? statusPublico, Guid? clienteId, CancellationToken ct) =>
        (await repository.GetPublicosAsync(statusPublico, clienteId, ct)).Select(ToDto).ToList();

    public async Task<ProjetoDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var projeto = await repository.GetByIdAsync(id, ct);
        return projeto is null ? null : ToDto(projeto);
    }

    /// <summary>Só retorna o projeto se for público — usado pela rota pública de detalhe.</summary>
    public async Task<ProjetoDto?> GetPublicoAsync(Guid id, CancellationToken ct)
    {
        var projeto = await repository.GetByIdAsync(id, ct);
        return projeto is null || projeto.Visibilidade != Visibilidade.publico ? null : ToDto(projeto);
    }

    public async Task<ProjetoDto> CreateAsync(UpsertProjetoRequest request, CancellationToken ct)
    {
        var projeto = new Projeto
        {
            Id = Guid.NewGuid(),
            ClienteId = request.ClienteId,
            Nome = request.Nome,
            Categoria = request.Categoria,
            StatusPublico = Enum.Parse<StatusPublico>(request.StatusPublico),
            Visibilidade = Enum.Parse<Visibilidade>(request.Visibilidade),
            ImagemCapaUrl = request.ImagemCapaUrl,
            LinkUrl = request.LinkUrl,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await repository.AddAsync(projeto, ct);
        return ToDto(projeto);
    }
}
