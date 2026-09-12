namespace PortalApi.Domain.Entities;

public class PedidoResposta
{
    public Guid Id { get; set; }
    public Guid PedidoId { get; set; }
    public required string Texto { get; set; }

    /// <summary>Path relativo no disco de artefatos (mesmo storage do ticket #18), ou null.</summary>
    public string? ArquivoUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
