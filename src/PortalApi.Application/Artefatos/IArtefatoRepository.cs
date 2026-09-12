using PortalApi.Domain.Entities;

namespace PortalApi.Application.Artefatos;

public interface IArtefatoRepository
{
    Task<IReadOnlyList<Artefato>> GetByProjetoIdAsync(Guid projetoId, CancellationToken ct);
    Task<Artefato?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Artefato artefato, CancellationToken ct);
    Task DeleteAsync(Artefato artefato, CancellationToken ct);
}
