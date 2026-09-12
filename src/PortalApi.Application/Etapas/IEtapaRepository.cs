using PortalApi.Domain.Entities;

namespace PortalApi.Application.Etapas;

public interface IEtapaRepository
{
    Task<IReadOnlyList<Etapa>> GetByProjetoIdAsync(Guid projetoId, CancellationToken ct);
    Task<Etapa?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Etapa etapa, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task DeleteAsync(Etapa etapa, CancellationToken ct);
}
