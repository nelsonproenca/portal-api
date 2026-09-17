using PortalApi.Domain.Entities;

namespace PortalApi.Application.Projetos;

public interface IProjetoRepository
{
    Task<IReadOnlyList<Projeto>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<Projeto>> GetPublicosAsync(string? statusPublico, Guid? clienteId, CancellationToken ct);
    Task<Projeto?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Projeto projeto, CancellationToken ct);
}
