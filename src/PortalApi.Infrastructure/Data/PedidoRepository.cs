using Microsoft.EntityFrameworkCore;
using PortalApi.Application.Pedidos;
using PortalApi.Domain.Entities;

namespace PortalApi.Infrastructure.Data;

public class PedidoRepository(PortalDbContext db) : IPedidoRepository
{
    public async Task<IReadOnlyList<Pedido>> GetByProjetoIdAsync(Guid projetoId, CancellationToken ct) =>
        await db.Pedidos.AsNoTracking()
            .Where(p => p.ProjetoId == projetoId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    public Task<Pedido?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Pedidos.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(Pedido pedido, CancellationToken ct)
    {
        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
