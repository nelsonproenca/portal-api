using System.Text.Json.Serialization;

namespace PortalApi.Application.Projetos;

// Nomes de propriedade em snake_case: casam com o contrato que o frontend já
// usa hoje (features/portfolio/api.ts, tipo gerado a partir do Supabase) —
// evita reescrever GestaoProjetos.tsx/PortfolioPage.tsx/ProjetoDetalhePage.tsx
// além da troca de fetch em si (ver spec, "Frontend changes").
public record ProjetoDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("cliente_id")] Guid ClienteId,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("categoria")] string? Categoria,
    [property: JsonPropertyName("status_publico")] string StatusPublico,
    [property: JsonPropertyName("visibilidade")] string Visibilidade,
    [property: JsonPropertyName("imagem_capa_url")] string? ImagemCapaUrl,
    [property: JsonPropertyName("link_url")] string? LinkUrl,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

public record UpsertProjetoRequest(
    [property: JsonPropertyName("cliente_id")] Guid ClienteId,
    [property: JsonPropertyName("nome")] string Nome,
    [property: JsonPropertyName("categoria")] string? Categoria,
    [property: JsonPropertyName("status_publico")] string StatusPublico,
    [property: JsonPropertyName("visibilidade")] string Visibilidade,
    [property: JsonPropertyName("imagem_capa_url")] string? ImagemCapaUrl,
    [property: JsonPropertyName("link_url")] string? LinkUrl);
