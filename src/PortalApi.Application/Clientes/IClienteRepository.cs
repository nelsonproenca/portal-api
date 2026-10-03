using PortalApi.Domain.Entities;

namespace PortalApi.Application.Clientes;

public interface IClienteRepository
{
    Task<IReadOnlyList<Cliente>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<Cliente>> GetByStatusAsync(string status, CancellationToken ct);
    Task<Cliente?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Cliente?> GetByEmailAsync(string email, CancellationToken ct);
    Task AddAsync(Cliente cliente, CancellationToken ct);
    Task UpdateAsync(Cliente cliente, CancellationToken ct);

}
