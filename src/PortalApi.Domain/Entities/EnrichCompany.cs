namespace PortalApi.Domain.Entities;

/// <summary>Pedido de enriquecimento de empresa; o n8n devolve <see cref="OutputAi"/> por callback.</summary>
public class EnrichCompany
{
    public Guid Id { get; set; }
    public string? CompanyName { get; set; }
    public string? Segment { get; set; }
    public string? OutputAi { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
