using PortalApi.Domain.Entities;

namespace PortalApi.Application.Clientes;

public interface IClienteRepository
{
    Task<IReadOnlyList<Cliente>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<Cliente>> GetByStatusAsync(string status, CancellationToken ct);
    Task<Cliente?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Cliente cliente, CancellationToken ct);
    Task UpdateAsync(Cliente cliente, CancellationToken ct);

    /// <summary>Upsert por Id — usado só pelo backfill (ticket #15), pra preservar os
    /// IDs dos clientes que já existiam no Supabase.</summary>
    Task UpsertPreservingIdAsync(Cliente cliente, CancellationToken ct);
}
