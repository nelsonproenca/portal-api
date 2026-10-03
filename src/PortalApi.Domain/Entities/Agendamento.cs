namespace PortalApi.Domain.Entities;

/// <summary>Reunião agendada (hoje criada pelo atendimento do Instagram via n8n).</summary>
public class Agendamento
{
    public Guid Id { get; set; }
    public string? ClienteNome { get; set; }
    public string? ClienteEmail { get; set; }
    public string? ClienteWhatsapp { get; set; }
    public DateTimeOffset DataReuniao { get; set; }
    public string? Status { get; set; }
    public string? InstagramUserId { get; set; }
    public string? ExpertResponsavel { get; set; }

    /// <summary>Texto livre: nome de quem indicou ou o ref do referral. Sem relação com colaboradores.</summary>
    public string? IndicadoPor { get; set; }

    public string? Origem { get; set; }
    public decimal? ValorProjeto { get; set; }
    public bool ComissaoPaga { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
