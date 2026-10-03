namespace PortalApi.Domain.Entities;

/// <summary>
/// Link de acesso do portal do cliente (substitui o magic link do Supabase Auth). Só o hash SHA-256 do token é
/// guardado: quem lê o banco não consegue entrar. Uso único (<see cref="UsedAt"/>) e curta duração.
/// </summary>
public class ClienteLoginToken
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string TokenHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
