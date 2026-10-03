using PortalApi.Domain.Entities;

namespace PortalApi.Application.Crm;

public class LeadService(ICrmRepository repository, INotificadorN8n n8n)
{
    public const string OrigemSite = "Site_Institucional";
    private const int MaxAnalise = 100_000;

    private static LeadDto ToDto(LeadIa l) =>
        new(l.Id, l.Nome, l.Empresa, l.Contato, l.Canal, l.DesafioTecnico, l.Origem, l.AnaliseIa, l.VistoPeloNelson, l.CreatedAt);

    /// <summary>Formulário público do site: grava o lead e avisa o n8n (a análise volta por callback).</summary>
    public async Task<CrmResult<CriadoDto>> CriarAsync(CriarLeadRequest request, CancellationToken ct)
    {
        var c = new Campos();
        var lead = new LeadIa
        {
            Id = Guid.NewGuid(),
            Nome = c.Obrigatorio(request.Nome, "Nome", 100),
            Contato = c.Obrigatorio(request.Contato, "Contato", 255),
            DesafioTecnico = c.Obrigatorio(request.DesafioTecnico, "Desafio técnico", 2000),
            Empresa = c.Opcional(request.Empresa, "Empresa", 200),
            Canal = c.Opcional(request.Canal, "Canal", 50),
            Origem = OrigemSite,
            VistoPeloNelson = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        if (c.Erro is not null) return CrmResult<CriadoDto>.Invalido(c.Erro);

        await repository.AddAsync(lead, ct);
        await n8n.NotificarLeadAsync(
            new N8nLeadPayload(lead.Id, lead.Nome, lead.Empresa, lead.Contato, lead.Canal, lead.DesafioTecnico), ct);
        return CrmResult<CriadoDto>.Sucesso(new CriadoDto(lead.Id));
    }

    public async Task<IReadOnlyList<LeadDto>> ListarAsync(CancellationToken ct) =>
        (await repository.ListLeadsAsync(ct)).Select(ToDto).ToList();

    public async Task<CrmResult<LeadDto>> MarcarVistoAsync(Guid id, bool visto, CancellationToken ct)
    {
        var lead = await repository.FindAsync<LeadIa>(id, ct);
        if (lead is null) return CrmResult<LeadDto>.NaoEncontrado();

        lead.VistoPeloNelson = visto;
        await repository.SaveAsync(ct);
        return CrmResult<LeadDto>.Sucesso(ToDto(lead));
    }

    /// <summary>Callback do n8n com a análise da IA.</summary>
    public async Task<CrmResult<LeadDto>> RegistrarAnaliseAsync(Guid id, string? analiseIa, CancellationToken ct)
    {
        var c = new Campos();
        var texto = c.Obrigatorio(analiseIa, "analiseIa", MaxAnalise);
        if (c.Erro is not null) return CrmResult<LeadDto>.Invalido(c.Erro);

        var lead = await repository.FindAsync<LeadIa>(id, ct);
        if (lead is null) return CrmResult<LeadDto>.NaoEncontrado();

        lead.AnaliseIa = texto;
        await repository.SaveAsync(ct);
        return CrmResult<LeadDto>.Sucesso(ToDto(lead));
    }
}
