using PortalApi.Domain.Entities;

namespace PortalApi.Application.Pedidos;

public interface IPedidoRespostaRepository
{
    Task<IReadOnlyList<PedidoResposta>> GetByPedidoIdAsync(Guid pedidoId, CancellationToken ct);
    Task AddAsync(PedidoResposta resposta, CancellationToken ct);
}
