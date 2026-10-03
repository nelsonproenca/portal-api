namespace PortalApi.Domain.Entities;

public class Colaborador
{
    public Guid Id { get; set; }
    public required string Nome { get; set; }
    public string? Cargo { get; set; }
    public string? Departamento { get; set; }
    public required string Email { get; set; }
    public string? FotoUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
