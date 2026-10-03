namespace PortalApi.Domain.Entities;

/// <summary>Lead capturado pelo formulário do site; o n8n devolve a análise por callback.</summary>
public class LeadIa
{
    public Guid Id { get; set; }
    public string? Nome { get; set; }
    public string? Empresa { get; set; }
    public string? Contato { get; set; }
    public string? Canal { get; set; }
    public string? DesafioTecnico { get; set; }
    public string? Origem { get; set; }
    public string? AnaliseIa { get; set; }
    public bool VistoPeloNelson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
