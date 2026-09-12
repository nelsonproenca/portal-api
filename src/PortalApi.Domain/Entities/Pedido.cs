using PortalApi.Domain.Enums;

namespace PortalApi.Domain.Entities;

public class Pedido
{
    public Guid Id { get; set; }
    public Guid ProjetoId { get; set; }
    public Guid? EtapaId { get; set; }
    public PedidoTipo Tipo { get; set; }
    public required string Titulo { get; set; }
    public PedidoStatus Status { get; set; } = PedidoStatus.pendente;
    public DateTimeOffset CreatedAt { get; set; }
}
