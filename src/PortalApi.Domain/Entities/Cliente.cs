namespace PortalApi.Domain.Entities;

public class Cliente
{
    public Guid Id { get; set; }
    public required string Nome { get; set; }
    public required string Email { get; set; }
    public string? Empresa { get; set; }
    public string? Segmento { get; set; }
    public string? SiteUrl { get; set; }
    public string? LogoUrl { get; set; }

    /// <summary>Livre (herdado do Supabase — hoje só "ativo" é usado, pela vitrine pública).</summary>
    public string? Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
