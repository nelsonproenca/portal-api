using System.Text.Json.Serialization;

namespace PortalApi.Application.Etapas;

public record EtapaDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("projeto_id")] Guid ProjetoId,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("ordem")] int Ordem,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

public record CreateEtapaRequest(
    [property: JsonPropertyName("projeto_id")] Guid ProjetoId,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("ordem")] int Ordem);

/// <summary>Update parcial — só os campos presentes são aplicados (casa com
/// `TablesUpdate&lt;"etapas"&gt;` que o frontend já usa: às vezes só `ordem`,
/// às vezes só `status`).</summary>
public record UpdateEtapaRequest(
    [property: JsonPropertyName("nome")] string? Nome,
    [property: JsonPropertyName("ordem")] int? Ordem,
    [property: JsonPropertyName("status")] string? Status);
