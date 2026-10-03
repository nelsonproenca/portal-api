using PortalApi.Domain.Entities;

namespace PortalApi.Application.Crm;

public class AgendamentoService(ICrmRepository repository)
{
    public const string ExpertPadrao = "Nelson Proença";

    private static AgendamentoDto ToDto(Agendamento a) =>
        new(a.Id, a.ClienteNome, a.ClienteEmail, a.ClienteWhatsapp, a.DataReuniao, a.Status, a.InstagramUserId,
            a.ExpertResponsavel, a.IndicadoPor, a.Origem, a.ValorProjeto, a.ComissaoPaga, a.CreatedAt);

    public async Task<IReadOnlyList<AgendamentoDto>> ListarAsync(CancellationToken ct) =>
        (await repository.ListAgendamentosAsync(ct)).Select(ToDto).ToList();

    /// <summary>Criado pelo n8n (atendimento do Instagram). Os padrões repetem os do schema original.</summary>
    public async Task<CrmResult<AgendamentoDto>> CriarAsync(CriarAgendamentoRequest request, CancellationToken ct)
    {
        if (request.DataReuniao is null) return CrmResult<AgendamentoDto>.Invalido("dataReuniao é obrigatória.");
        if (request.ValorProjeto is < 0) return CrmResult<AgendamentoDto>.Invalido("valorProjeto não pode ser negativo.");

        var c = new Campos();
        var agendamento = new Agendamento
        {
            Id = Guid.NewGuid(),
            ClienteNome = c.Opcional(request.ClienteNome, "clienteNome", 200),
            ClienteEmail = c.Opcional(request.ClienteEmail, "clienteEmail", 320),
            ClienteWhatsapp = c.Opcional(request.ClienteWhatsapp, "clienteWhatsapp", 50),
            DataReuniao = request.DataReuniao.Value,
            Status = c.Opcional(request.Status, "status", 50) ?? "pendente",
            InstagramUserId = c.Opcional(request.InstagramUserId, "instagramUserId", 100),
            ExpertResponsavel = c.Opcional(request.ExpertResponsavel, "expertResponsavel", 200) ?? ExpertPadrao,
            IndicadoPor = c.Opcional(request.IndicadoPor, "indicadoPor", 200) ?? ExpertPadrao,
            Origem = c.Opcional(request.Origem, "origem", 100) ?? "Site",
            ValorProjeto = request.ValorProjeto,
            ComissaoPaga = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        if (c.Erro is not null) return CrmResult<AgendamentoDto>.Invalido(c.Erro);

        await repository.AddAsync(agendamento, ct);
        return CrmResult<AgendamentoDto>.Sucesso(ToDto(agendamento));
    }

    /// <summary>Painel do admin: troca o status, marca a comissão ou ajusta o valor. Só o que veio muda.</summary>
    public async Task<CrmResult<AgendamentoDto>> AtualizarAsync(Guid id, AtualizarAgendamentoRequest request, CancellationToken ct)
    {
        if (request.ValorProjeto is < 0) return CrmResult<AgendamentoDto>.Invalido("valorProjeto não pode ser negativo.");

        var c = new Campos();
        var status = c.Opcional(request.Status, "status", 50);
        if (c.Erro is not null) return CrmResult<AgendamentoDto>.Invalido(c.Erro);

        var agendamento = await repository.FindAsync<Agendamento>(id, ct);
        if (agendamento is null) return CrmResult<AgendamentoDto>.NaoEncontrado();

        if (status is not null) agendamento.Status = status;
        if (request.ComissaoPaga is { } paga) agendamento.ComissaoPaga = paga;
        if (request.ValorProjeto is { } valor) agendamento.ValorProjeto = valor;

        await repository.SaveAsync(ct);
        return CrmResult<AgendamentoDto>.Sucesso(ToDto(agendamento));
    }
}
