namespace PortalApi.Application.Crm;

// ─── Colaboradores ────────────────────────────────────────────────────────────

public record ColaboradorDto(
    Guid Id, string Nome, string? Cargo, string? Departamento, string Email, string? FotoUrl, DateTimeOffset CreatedAt);

public record UpsertColaboradorRequest(
    string? Nome, string? Cargo, string? Departamento, string? Email, string? FotoUrl);

// ─── Contatos de clientes ─────────────────────────────────────────────────────

public record ContatoClienteDto(
    Guid Id, Guid ClienteId, string Nome, string? Telefone, string? Email, DateTimeOffset CreatedAt);

public record UpsertContatoClienteRequest(
    Guid ClienteId, string? Nome, string? Telefone, string? Email);

// ─── Leads ────────────────────────────────────────────────────────────────────

public record CriarLeadRequest(
    string? Nome, string? Empresa, string? Contato, string? Canal, string? DesafioTecnico);

public record LeadDto(
    Guid Id, string? Nome, string? Empresa, string? Contato, string? Canal, string? DesafioTecnico,
    string? Origem, string? AnaliseIa, bool VistoPeloNelson, DateTimeOffset CreatedAt);

public record MarcarLeadVistoRequest(bool Visto);

public record ResultadoAnaliseLeadRequest(string? AnaliseIa);

// ─── Enrich / Playground ──────────────────────────────────────────────────────

public record CriarEnrichRequest(string? NomeEmpresa, string? Segmento);

public record CriarPlaygroundRequest(string? TipoAnalise, string? InputTecnico);

/// <summary>Resposta do POST (só o id, para o front fazer polling) e do GET (resultado, nulo até o n8n responder).</summary>
public record CriadoDto(Guid Id);

public record ResultadoEnrichDto(Guid Id, string? OutputAi, bool Pronto);

public record ResultadoPlaygroundDto(Guid Id, string? OutputIa, string? Status, bool Pronto);

public record ResultadoEnrichRequest(string? OutputAi);

public record ResultadoPlaygroundRequest(string? OutputIa);

// ─── Agendamentos ─────────────────────────────────────────────────────────────

public record AgendamentoDto(
    Guid Id, string? ClienteNome, string? ClienteEmail, string? ClienteWhatsapp, DateTimeOffset DataReuniao,
    string? Status, string? InstagramUserId, string? ExpertResponsavel, string? IndicadoPor, string? Origem,
    decimal? ValorProjeto, bool ComissaoPaga, DateTimeOffset CreatedAt);

public record CriarAgendamentoRequest(
    string? ClienteNome, string? ClienteEmail, string? ClienteWhatsapp, DateTimeOffset? DataReuniao,
    string? Status, string? InstagramUserId, string? ExpertResponsavel, string? IndicadoPor, string? Origem,
    decimal? ValorProjeto);

/// <summary>Atualização parcial pelo painel: só o que veio preenchido muda.</summary>
public record AtualizarAgendamentoRequest(string? Status, bool? ComissaoPaga, decimal? ValorProjeto);

// ─── Payloads enviados ao n8n (contrato do PLANO.md) ──────────────────────────

public record N8nLeadPayload(Guid Id, string? Nome, string? Empresa, string? Contato, string? Canal, string? DesafioTecnico);

public record N8nPlaygroundPayload(Guid Id, string? TipoAnalise, string? InputTecnico);

public record N8nEnrichPayload(Guid Id, string? NomeEmpresa, string? Segmento);
