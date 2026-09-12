using PortalApi.Domain.Entities;

namespace PortalApi.Application.Pedidos;

public interface IPedidoRepository
{
    Task<IReadOnlyList<Pedido>> GetByProjetoIdAsync(Guid projetoId, CancellationToken ct);
    Task<Pedido?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Pedido pedido, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
