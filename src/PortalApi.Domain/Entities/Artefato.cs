using PortalApi.Domain.Enums;

namespace PortalApi.Domain.Entities;

public class Artefato
{
    public Guid Id { get; set; }
    public Guid ProjetoId { get; set; }
    public Guid? EtapaId { get; set; }
    public required string Nome { get; set; }

    /// <summary>Path relativo no disco (tipo=arquivo) ou URL completa (tipo=link).</summary>
    public required string Url { get; set; }

    public ArtefatoTipo Tipo { get; set; }
    public string UploadedBy { get; set; } = "admin";
    public DateTimeOffset CreatedAt { get; set; }
}
