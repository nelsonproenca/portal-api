using Microsoft.EntityFrameworkCore;
using PortalApi.Application.Artefatos;
using PortalApi.Domain.Entities;

namespace PortalApi.Infrastructure.Data;

public class ArtefatoRepository(PortalDbContext db) : IArtefatoRepository
{
    public async Task<IReadOnlyList<Artefato>> GetByProjetoIdAsync(Guid projetoId, CancellationToken ct) =>
        await db.Artefatos.AsNoTracking()
            .Where(a => a.ProjetoId == projetoId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    public Task<Artefato?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Artefatos.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task AddAsync(Artefato artefato, CancellationToken ct)
    {
        db.Artefatos.Add(artefato);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Artefato artefato, CancellationToken ct)
    {
        db.Artefatos.Remove(artefato);
        await db.SaveChangesAsync(ct);
    }
}
