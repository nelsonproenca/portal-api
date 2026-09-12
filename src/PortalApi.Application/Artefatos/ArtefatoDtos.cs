using System.Text.Json.Serialization;

namespace PortalApi.Application.Artefatos;

public record ArtefatoDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("projeto_id")] Guid ProjetoId,
    [property: JsonPropertyName("etapa_id")] Guid? EtapaId,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("tipo")] string Tipo,
    [property: JsonPropertyName("uploaded_by")] string UploadedBy,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

public record CreateArtefatoLinkRequest(
    [property: JsonPropertyName("projetoId")] Guid ProjetoId,
    [property: JsonPropertyName("etapaId")] Guid? EtapaId,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("linkUrl")] string LinkUrl);
