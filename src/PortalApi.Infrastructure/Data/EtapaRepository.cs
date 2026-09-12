using Microsoft.EntityFrameworkCore;
using PortalApi.Application.Etapas;
using PortalApi.Domain.Entities;

namespace PortalApi.Infrastructure.Data;

public class EtapaRepository(PortalDbContext db) : IEtapaRepository
{
    public async Task<IReadOnlyList<Etapa>> GetByProjetoIdAsync(Guid projetoId, CancellationToken ct) =>
        await db.Etapas.AsNoTracking()
            .Where(e => e.ProjetoId == projetoId)
            .OrderBy(e => e.Ordem)
            .ToListAsync(ct);

    public Task<Etapa?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Etapas.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task AddAsync(Etapa etapa, CancellationToken ct)
    {
        db.Etapas.Add(etapa);
        await db.SaveChangesAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task DeleteAsync(Etapa etapa, CancellationToken ct)
    {
        db.Etapas.Remove(etapa);
        await db.SaveChangesAsync(ct);
    }
}
