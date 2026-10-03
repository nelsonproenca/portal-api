using PortalApi.Domain.Entities;

namespace PortalApi.Application.Crm;

/// <summary>Enriquecimento de empresa e playground técnico: o mesmo ciclo (grava, avisa o n8n, o front faz
/// polling no GET e o n8n responde por callback), cada um na sua tabela.</summary>
public class AnaliseService(ICrmRepository repository, INotificadorN8n n8n)
{
    public const string StatusPendente = "pendente";
    public const string StatusConcluido = "concluido";
    private const int MaxResultado = 100_000;

    // ─── Enriquecer empresa ───────────────────────────────────────────────────

    public async Task<CrmResult<CriadoDto>> CriarEnrichAsync(CriarEnrichRequest request, CancellationToken ct)
    {
        var c = new Campos();
        var enrich = new EnrichCompany
        {
            Id = Guid.NewGuid(),
            CompanyName = c.Obrigatorio(request.NomeEmpresa, "Nome da empresa", 200),
            Segment = c.Opcional(request.Segmento, "Segmento", 200),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        if (c.Erro is not null) return CrmResult<CriadoDto>.Invalido(c.Erro);

        await repository.AddAsync(enrich, ct);
        await n8n.NotificarEnrichAsync(new N8nEnrichPayload(enrich.Id, enrich.CompanyName, enrich.Segment), ct);
        return CrmResult<CriadoDto>.Sucesso(new CriadoDto(enrich.Id));
    }

    public async Task<ResultadoEnrichDto?> ObterEnrichAsync(Guid id, CancellationToken ct)
    {
        var enrich = await repository.FindAsync<EnrichCompany>(id, ct);
        return enrich is null ? null : new ResultadoEnrichDto(enrich.Id, enrich.OutputAi, enrich.OutputAi is not null);
    }

    public async Task<CrmResult<ResultadoEnrichDto>> RegistrarEnrichAsync(Guid id, string? outputAi, CancellationToken ct)
    {
        var c = new Campos();
        var texto = c.Obrigatorio(outputAi, "outputAi", MaxResultado);
        if (c.Erro is not null) return CrmResult<ResultadoEnrichDto>.Invalido(c.Erro);

        var enrich = await repository.FindAsync<EnrichCompany>(id, ct);
        if (enrich is null) return CrmResult<ResultadoEnrichDto>.NaoEncontrado();

        enrich.OutputAi = texto;
        await repository.SaveAsync(ct);
        return CrmResult<ResultadoEnrichDto>.Sucesso(new ResultadoEnrichDto(enrich.Id, enrich.OutputAi, true));
    }

    // ─── Playground ───────────────────────────────────────────────────────────

    public async Task<CrmResult<CriadoDto>> CriarPlaygroundAsync(CriarPlaygroundRequest request, CancellationToken ct)
    {
        var c = new Campos();
        var analise = new PlaygroundAnalise
        {
            Id = Guid.NewGuid(),
            InputTecnico = c.Obrigatorio(request.InputTecnico, "Input técnico", 5000),
            TipoAnalise = c.Opcional(request.TipoAnalise, "Tipo de análise", 100),
            Status = StatusPendente,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        if (c.Erro is not null) return CrmResult<CriadoDto>.Invalido(c.Erro);

        await repository.AddAsync(analise, ct);
        await n8n.NotificarPlaygroundAsync(new N8nPlaygroundPayload(analise.Id, analise.TipoAnalise, analise.InputTecnico), ct);
        return CrmResult<CriadoDto>.Sucesso(new CriadoDto(analise.Id));
    }

    public async Task<ResultadoPlaygroundDto?> ObterPlaygroundAsync(Guid id, CancellationToken ct)
    {
        var analise = await repository.FindAsync<PlaygroundAnalise>(id, ct);
        return analise is null ? null : new ResultadoPlaygroundDto(analise.Id, analise.OutputIa, analise.Status, analise.OutputIa is not null);
    }

    public async Task<CrmResult<ResultadoPlaygroundDto>> RegistrarPlaygroundAsync(Guid id, string? outputIa, CancellationToken ct)
    {
        var c = new Campos();
        var texto = c.Obrigatorio(outputIa, "outputIa", MaxResultado);
        if (c.Erro is not null) return CrmResult<ResultadoPlaygroundDto>.Invalido(c.Erro);

        var analise = await repository.FindAsync<PlaygroundAnalise>(id, ct);
        if (analise is null) return CrmResult<ResultadoPlaygroundDto>.NaoEncontrado();

        analise.OutputIa = texto;
        analise.Status = StatusConcluido;
        await repository.SaveAsync(ct);
        return CrmResult<ResultadoPlaygroundDto>.Sucesso(
            new ResultadoPlaygroundDto(analise.Id, analise.OutputIa, analise.Status, true));
    }
}
