using Microsoft.EntityFrameworkCore;
using PortalApi.Application.Pedidos;
using PortalApi.Domain.Entities;

namespace PortalApi.Infrastructure.Data;

public class PedidoRespostaRepository(PortalDbContext db) : IPedidoRespostaRepository
{
    public async Task<IReadOnlyList<PedidoResposta>> GetByPedidoIdAsync(Guid pedidoId, CancellationToken ct) =>
        await db.PedidoRespostas.AsNoTracking()
            .Where(r => r.PedidoId == pedidoId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(PedidoResposta resposta, CancellationToken ct)
    {
        db.PedidoRespostas.Add(resposta);
        await db.SaveChangesAsync(ct);
    }
}
