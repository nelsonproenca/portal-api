namespace PortalApi.Domain.Entities;

/// <summary>Pessoa de contato de um <see cref="Cliente"/>. Some junto com o cliente (cascade).</summary>
public class ContatoCliente
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public required string Nome { get; set; }
    public string? Telefone { get; set; }
    public string? Email { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
