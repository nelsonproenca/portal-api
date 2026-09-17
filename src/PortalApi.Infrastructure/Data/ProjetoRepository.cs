using Microsoft.EntityFrameworkCore;
using PortalApi.Application.Projetos;
using PortalApi.Domain.Entities;
using PortalApi.Domain.Enums;

namespace PortalApi.Infrastructure.Data;

public class ProjetoRepository(PortalDbContext db) : IProjetoRepository
{
    public async Task<IReadOnlyList<Projeto>> GetAllAsync(CancellationToken ct) =>
        await db.Projetos.AsNoTracking().OrderByDescending(p => p.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<Projeto>> GetPublicosAsync(string? statusPublico, Guid? clienteId, CancellationToken ct)
    {
        var query = db.Projetos.AsNoTracking().Where(p => p.Visibilidade == Visibilidade.publico);

        if (!string.IsNullOrWhiteSpace(statusPublico) && Enum.TryParse<StatusPublico>(statusPublico, out var status))
            query = query.Where(p => p.StatusPublico == status);

        if (clienteId.HasValue)
            query = query.Where(p => p.ClienteId == clienteId.Value);

        return await query.OrderByDescending(p => p.CreatedAt).ToListAsync(ct);
    }

    public Task<Projeto?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Projetos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(Projeto projeto, CancellationToken ct)
    {
        db.Projetos.Add(projeto);
        await db.SaveChangesAsync(ct);
    }
}
