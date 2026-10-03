namespace PortalApi.Domain.Entities;

/// <summary>Análise técnica do playground; o n8n devolve <see cref="OutputIa"/> por callback.</summary>
public class PlaygroundAnalise
{
    public Guid Id { get; set; }
    public string? InputTecnico { get; set; }
    public string? TipoAnalise { get; set; }
    public string? OutputIa { get; set; }
    public string? Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
