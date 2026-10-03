using PortalApi.Domain.Entities;

namespace PortalApi.Application.Crm;

/// <summary>Acesso às 6 tabelas do CRM. As listas voltam sem tracking; <see cref="FindAsync{T}"/> volta
/// rastreada, e a alteração só vai ao banco em <see cref="SaveAsync"/>.</summary>
public interface ICrmRepository
{
    Task<IReadOnlyList<Colaborador>> ListColaboradoresAsync(CancellationToken ct);
    Task<bool> EmailColaboradorExisteAsync(string email, Guid? exceto, CancellationToken ct);

    Task<IReadOnlyList<ContatoCliente>> ListContatosAsync(Guid? clienteId, CancellationToken ct);
    Task<bool> ClienteExisteAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<LeadIa>> ListLeadsAsync(CancellationToken ct);
    Task<IReadOnlyList<Agendamento>> ListAgendamentosAsync(CancellationToken ct);

    Task<T?> FindAsync<T>(Guid id, CancellationToken ct) where T : class;
    Task AddAsync<T>(T entity, CancellationToken ct) where T : class;
    Task RemoveAsync<T>(T entity, CancellationToken ct) where T : class;
    Task SaveAsync(CancellationToken ct);
}
