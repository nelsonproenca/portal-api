using Microsoft.EntityFrameworkCore;
using PortalApi.Application.Crm;
using PortalApi.Domain.Entities;

namespace PortalApi.Infrastructure.Data;

public class CrmRepository(PortalDbContext db) : ICrmRepository
{
    public async Task<IReadOnlyList<Colaborador>> ListColaboradoresAsync(CancellationToken ct) =>
        await db.Colaboradores.AsNoTracking().OrderByDescending(c => c.CreatedAt).ToListAsync(ct);

    public Task<bool> EmailColaboradorExisteAsync(string email, Guid? exceto, CancellationToken ct) =>
        db.Colaboradores.AnyAsync(c => c.Email == email && (exceto == null || c.Id != exceto), ct);

    public async Task<IReadOnlyList<ContatoCliente>> ListContatosAsync(Guid? clienteId, CancellationToken ct) =>
        await db.ContatosClientes.AsNoTracking()
            .Where(c => clienteId == null || c.ClienteId == clienteId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    public Task<bool> ClienteExisteAsync(Guid id, CancellationToken ct) =>
        db.Clientes.AnyAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<LeadIa>> ListLeadsAsync(CancellationToken ct) =>
        await db.LeadsIa.AsNoTracking().OrderByDescending(l => l.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<Agendamento>> ListAgendamentosAsync(CancellationToken ct) =>
        await db.Agendamentos.AsNoTracking().OrderByDescending(a => a.DataReuniao).ToListAsync(ct);

    public async Task<T?> FindAsync<T>(Guid id, CancellationToken ct) where T : class =>
        await db.Set<T>().FindAsync([id], ct);

    public async Task AddAsync<T>(T entity, CancellationToken ct) where T : class
    {
        db.Set<T>().Add(entity);
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync<T>(T entity, CancellationToken ct) where T : class
    {
        db.Set<T>().Remove(entity);
        await db.SaveChangesAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
