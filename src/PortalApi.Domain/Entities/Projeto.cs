using PortalApi.Domain.Enums;

namespace PortalApi.Domain.Entities;

public class Projeto
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public required string Nome { get; set; }
    public string? Categoria { get; set; }
    public StatusPublico StatusPublico { get; set; } = StatusPublico.em_andamento;
    public Visibilidade Visibilidade { get; set; } = Visibilidade.privado;
    public string? ImagemCapaUrl { get; set; }
    public string? LinkUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
