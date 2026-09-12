using Microsoft.EntityFrameworkCore;
using PortalApi.Application.Clientes;
using PortalApi.Domain.Entities;

namespace PortalApi.Infrastructure.Data;

public class ClienteRepository(PortalDbContext db) : IClienteRepository
{
    public async Task<IReadOnlyList<Cliente>> GetAllAsync(CancellationToken ct) =>
        await db.Clientes.AsNoTracking().OrderByDescending(c => c.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<Cliente>> GetByStatusAsync(string status, CancellationToken ct) =>
        await db.Clientes.AsNoTracking()
            .Where(c => c.Status == status)
            .OrderBy(c => c.Empresa)
            .ToListAsync(ct);

    public Task<Cliente?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Clientes.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task AddAsync(Cliente cliente, CancellationToken ct)
    {
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Cliente cliente, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
    }

    public async Task UpsertPreservingIdAsync(Cliente cliente, CancellationToken ct)
    {
        var existing = await db.Clientes.FirstOrDefaultAsync(c => c.Id == cliente.Id, ct);
        if (existing is null)
        {
            db.Clientes.Add(cliente);
        }
        else
        {
            existing.Nome = cliente.Nome;
            existing.Email = cliente.Email;
            existing.Empresa = cliente.Empresa;
            existing.Segmento = cliente.Segmento;
            existing.SiteUrl = cliente.SiteUrl;
            existing.LogoUrl = cliente.LogoUrl;
            existing.Status = cliente.Status;
        }
        await db.SaveChangesAsync(ct);
    }
}
