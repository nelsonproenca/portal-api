using PortalApi.Domain.Entities;
using PortalApi.Domain.Enums;

namespace PortalApi.Application.Etapas;

public class EtapaService(IEtapaRepository repository)
{
    private static EtapaDto ToDto(Etapa e) => new(e.Id, e.ProjetoId, e.Nome, e.Ordem, e.Status.ToString(), e.CreatedAt);

    public async Task<IReadOnlyList<EtapaDto>> ListByProjetoAsync(Guid projetoId, CancellationToken ct) =>
        (await repository.GetByProjetoIdAsync(projetoId, ct)).Select(ToDto).ToList();

    public async Task<EtapaDto> CreateAsync(CreateEtapaRequest request, CancellationToken ct)
    {
        var etapa = new Etapa
        {
            Id = Guid.NewGuid(),
            ProjetoId = request.ProjetoId,
            Nome = request.Nome,
            Ordem = request.Ordem,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await repository.AddAsync(etapa, ct);
        return ToDto(etapa);
    }

    public async Task<EtapaDto?> UpdateAsync(Guid id, UpdateEtapaRequest request, CancellationToken ct)
    {
        var etapa = await repository.GetByIdAsync(id, ct);
        if (etapa is null) return null;

        if (request.Nome is not null) etapa.Nome = request.Nome;
        if (request.Ordem is not null) etapa.Ordem = request.Ordem.Value;
        if (request.Status is not null) etapa.Status = Enum.Parse<EtapaStatus>(request.Status);

        await repository.SaveChangesAsync(ct);
        return ToDto(etapa);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var etapa = await repository.GetByIdAsync(id, ct);
        if (etapa is null) return false;
        await repository.DeleteAsync(etapa, ct);
        return true;
    }
}
