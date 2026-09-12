using PortalApi.Domain.Enums;

namespace PortalApi.Domain.Entities;

public class Etapa
{
    public Guid Id { get; set; }
    public Guid ProjetoId { get; set; }
    public required string Nome { get; set; }
    public int Ordem { get; set; }
    public EtapaStatus Status { get; set; } = EtapaStatus.pendente;
    public DateTimeOffset CreatedAt { get; set; }
}
