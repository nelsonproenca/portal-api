using System.Text.Json.Serialization;

namespace PortalApi.Application.Pedidos;

public record PedidoDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("projeto_id")] Guid ProjetoId,
    [property: JsonPropertyName("etapa_id")] Guid? EtapaId,
    [property: JsonPropertyName("tipo")] string Tipo,
    [property: JsonPropertyName("titulo")] string Titulo,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

public record CreatePedidoRequest(
    [property: JsonPropertyName("projeto_id")] Guid ProjetoId,
    [property: JsonPropertyName("etapa_id")] Guid? EtapaId,
    [property: JsonPropertyName("tipo")] string Tipo,
    [property: JsonPropertyName("titulo")] string Titulo);

public record PedidoRespostaDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("pedido_id")] Guid PedidoId,
    [property: JsonPropertyName("texto")] string Texto,
    [property: JsonPropertyName("arquivo_url")] string? ArquivoUrl,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

public record DecisaoPedidoRequest(
    [property: JsonPropertyName("comentario")] string? Comentario);
