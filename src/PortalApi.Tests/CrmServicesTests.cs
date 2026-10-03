using Microsoft.EntityFrameworkCore;
using PortalApi.Application.Crm;
using PortalApi.Domain.Entities;
using PortalApi.Infrastructure.Data;

namespace PortalApi.Tests;

public class LeadServiceTests
{
    private static (LeadService Service, PortalDbContext Db, FakeNotificador N8n) Novo()
    {
        var db = TestDb.New();
        var n8n = new FakeNotificador();
        return (new LeadService(new CrmRepository(db), n8n), db, n8n);
    }

    [Fact]
    public async Task Criar_grava_o_lead_e_avisa_o_n8n_com_o_contrato()
    {
        var (service, db, n8n) = Novo();

        var r = await service.CriarAsync(new("  Ana  ", "Acme", "ana@acme.com", "email", "Preciso de um CRM"), default);

        Assert.True(r.Ok);
        var lead = await db.LeadsIa.SingleAsync();
        Assert.Equal(r.Valor!.Id, lead.Id);
        Assert.Equal("Ana", lead.Nome);
        Assert.Equal(LeadService.OrigemSite, lead.Origem);
        Assert.False(lead.VistoPeloNelson);

        var aviso = Assert.Single(n8n.Leads);
        Assert.Equal(new N8nLeadPayload(lead.Id, "Ana", "Acme", "ana@acme.com", "email", "Preciso de um CRM"), aviso);
    }

    [Theory]
    [InlineData(null, "c@c.com", "desafio")]
    [InlineData("Ana", "   ", "desafio")]
    [InlineData("Ana", "c@c.com", "")]
    public async Task Criar_recusa_campo_obrigatorio_vazio_sem_gravar_nem_avisar(string? nome, string? contato, string? desafio)
    {
        var (service, db, n8n) = Novo();

        var r = await service.CriarAsync(new(nome, null, contato, null, desafio), default);

        Assert.Equal(CrmErro.Invalido, r.Erro);
        Assert.Empty(db.LeadsIa);
        Assert.Empty(n8n.Leads);
    }

    [Fact]
    public async Task Criar_recusa_texto_maior_que_o_limite_da_coluna()
    {
        var (service, db, _) = Novo();

        var r = await service.CriarAsync(new("Ana", null, "c@c.com", null, new string('x', 2001)), default);

        Assert.Equal(CrmErro.Invalido, r.Erro);
        Assert.Empty(db.LeadsIa);
    }

    [Fact]
    public async Task RegistrarAnalise_grava_o_resultado_do_n8n()
    {
        var (service, db, _) = Novo();
        var id = (await service.CriarAsync(new("Ana", null, "c@c.com", null, "x"), default)).Valor!.Id;

        var r = await service.RegistrarAnaliseAsync(id, "Análise pronta", default);

        Assert.True(r.Ok);
        Assert.Equal("Análise pronta", (await db.LeadsIa.SingleAsync()).AnaliseIa);
    }

    [Fact]
    public async Task RegistrarAnalise_de_lead_inexistente_ou_vazia_e_recusada()
    {
        var (service, _, _) = Novo();
        var id = (await service.CriarAsync(new("Ana", null, "c@c.com", null, "x"), default)).Valor!.Id;

        Assert.Equal(CrmErro.NaoEncontrado, (await service.RegistrarAnaliseAsync(Guid.NewGuid(), "x", default)).Erro);
        Assert.Equal(CrmErro.Invalido, (await service.RegistrarAnaliseAsync(id, "  ", default)).Erro);
    }

    [Fact]
    public async Task MarcarVisto_alterna_e_devolve_nao_encontrado_para_id_desconhecido()
    {
        var (service, db, _) = Novo();
        var id = (await service.CriarAsync(new("Ana", null, "c@c.com", null, "x"), default)).Valor!.Id;

        Assert.True((await service.MarcarVistoAsync(id, true, default)).Ok);
        Assert.True((await db.LeadsIa.SingleAsync()).VistoPeloNelson);
        Assert.Equal(CrmErro.NaoEncontrado, (await service.MarcarVistoAsync(Guid.NewGuid(), true, default)).Erro);
    }
}

public class LeadExclusaoTests
{
    [Fact]
    public async Task Excluir_remove_so_o_lead_pedido_e_devolve_false_para_id_desconhecido()
    {
        var db = TestDb.New();
        var service = new LeadService(new CrmRepository(db), new FakeNotificador());
        var a = (await service.CriarAsync(new("A", null, "a@a.com", null, "x"), default)).Valor!.Id;
        var b = (await service.CriarAsync(new("B", null, "b@b.com", null, "x"), default)).Valor!.Id;

        Assert.True(await service.ExcluirAsync(a, default));

        Assert.Equal(b, (await db.LeadsIa.SingleAsync()).Id);
        Assert.False(await service.ExcluirAsync(a, default));
        Assert.False(await service.ExcluirAsync(Guid.NewGuid(), default));
    }
}

public class AnaliseServiceTests
{
    private static (AnaliseService Service, PortalDbContext Db, FakeNotificador N8n) Novo()
    {
        var db = TestDb.New();
        var n8n = new FakeNotificador();
        return (new AnaliseService(new CrmRepository(db), n8n), db, n8n);
    }

    [Fact]
    public async Task Enrich_ciclo_completo_cria_avisa_o_n8n_e_so_fica_pronto_depois_do_callback()
    {
        var (service, db, n8n) = Novo();

        var id = (await service.CriarEnrichAsync(new("Acme", "Varejo"), default)).Valor!.Id;
        Assert.Equal(new N8nEnrichPayload(id, "Acme", "Varejo"), Assert.Single(n8n.Enriches));
        Assert.False((await service.ObterEnrichAsync(id, default))!.Pronto);

        await service.RegistrarEnrichAsync(id, "Resumo da empresa", default);

        var pronto = await service.ObterEnrichAsync(id, default);
        Assert.True(pronto!.Pronto);
        Assert.Equal("Resumo da empresa", pronto.OutputAi);
        Assert.Equal("Resumo da empresa", (await db.EnrichCompanies.SingleAsync()).OutputAi);
    }

    [Fact]
    public async Task Playground_ciclo_completo_muda_o_status_de_pendente_para_concluido()
    {
        var (service, _, n8n) = Novo();

        var id = (await service.CriarPlaygroundAsync(new("arquitetura", "Quero migrar para microsserviços"), default)).Valor!.Id;
        Assert.Equal(new N8nPlaygroundPayload(id, "arquitetura", "Quero migrar para microsserviços"), Assert.Single(n8n.Playgrounds));
        var antes = await service.ObterPlaygroundAsync(id, default);
        Assert.False(antes!.Pronto);
        Assert.Equal(AnaliseService.StatusPendente, antes.Status);

        await service.RegistrarPlaygroundAsync(id, "Recomendação", default);

        var depois = await service.ObterPlaygroundAsync(id, default);
        Assert.True(depois!.Pronto);
        Assert.Equal(AnaliseService.StatusConcluido, depois.Status);
    }

    [Fact]
    public async Task Resultado_de_id_inexistente_e_get_de_id_inexistente()
    {
        var (service, _, _) = Novo();

        Assert.Equal(CrmErro.NaoEncontrado, (await service.RegistrarEnrichAsync(Guid.NewGuid(), "x", default)).Erro);
        Assert.Equal(CrmErro.NaoEncontrado, (await service.RegistrarPlaygroundAsync(Guid.NewGuid(), "x", default)).Erro);
        Assert.Null(await service.ObterEnrichAsync(Guid.NewGuid(), default));
        Assert.Null(await service.ObterPlaygroundAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Entrada_invalida_nao_grava_nem_avisa()
    {
        var (service, db, n8n) = Novo();

        Assert.Equal(CrmErro.Invalido, (await service.CriarEnrichAsync(new("  ", "x"), default)).Erro);
        Assert.Equal(CrmErro.Invalido, (await service.CriarPlaygroundAsync(new("t", new string('x', 5001)), default)).Erro);

        Assert.Empty(db.EnrichCompanies);
        Assert.Empty(db.PlaygroundAnalises);
        Assert.Empty(n8n.Enriches);
        Assert.Empty(n8n.Playgrounds);
    }
}

public class AgendamentoServiceTests
{
    private static (AgendamentoService Service, PortalDbContext Db) Novo()
    {
        var db = TestDb.New();
        return (new AgendamentoService(new CrmRepository(db)), db);
    }

    [Fact]
    public async Task Criar_aplica_os_padroes_do_schema_original()
    {
        var (service, db) = Novo();
        var data = new DateTimeOffset(2026, 10, 20, 14, 0, 0, TimeSpan.Zero);

        var r = await service.CriarAsync(new("Bia", null, "5511999990000", data, null, "ig-1", null, null, null, null), default);

        Assert.True(r.Ok);
        var a = await db.Agendamentos.SingleAsync();
        Assert.Equal("pendente", a.Status);
        Assert.Equal("Nelson Proença", a.ExpertResponsavel);
        Assert.Equal("Nelson Proença", a.IndicadoPor);
        Assert.Equal("Site", a.Origem);
        Assert.False(a.ComissaoPaga);
        Assert.Equal(data, a.DataReuniao);
    }

    [Fact]
    public async Task Criar_exige_data_e_nao_aceita_valor_negativo()
    {
        var (service, db) = Novo();

        Assert.Equal(CrmErro.Invalido, (await service.CriarAsync(new("Bia", null, null, null, null, null, null, null, null, null), default)).Erro);
        Assert.Equal(CrmErro.Invalido, (await service.CriarAsync(new("Bia", null, null, DateTimeOffset.UtcNow, null, null, null, null, null, -1m), default)).Erro);
        Assert.Empty(db.Agendamentos);
    }

    [Fact]
    public async Task Atualizar_so_muda_o_que_veio_preenchido()
    {
        var (service, db) = Novo();
        var id = (await service.CriarAsync(new("Bia", null, null, DateTimeOffset.UtcNow, "confirmado", null, null, null, null, 1000m), default)).Valor!.Id;

        await service.AtualizarAsync(id, new(null, true, null), default);
        var a = await db.Agendamentos.SingleAsync();
        Assert.True(a.ComissaoPaga);
        Assert.Equal("confirmado", a.Status);
        Assert.Equal(1000m, a.ValorProjeto);

        await service.AtualizarAsync(id, new("realizado", null, 2500m), default);
        a = await db.Agendamentos.SingleAsync();
        Assert.Equal("realizado", a.Status);
        Assert.Equal(2500m, a.ValorProjeto);
        Assert.True(a.ComissaoPaga);
    }

    [Fact]
    public async Task Atualizar_id_inexistente_e_nao_encontrado()
    {
        var (service, _) = Novo();
        Assert.Equal(CrmErro.NaoEncontrado, (await service.AtualizarAsync(Guid.NewGuid(), new("x", null, null), default)).Erro);
    }
}

public class CadastroCrmServiceTests
{
    private static (CadastroCrmService Service, PortalDbContext Db) Novo()
    {
        var db = TestDb.New();
        return (new CadastroCrmService(new CrmRepository(db)), db);
    }

    [Fact]
    public async Task Colaborador_com_email_repetido_e_conflito_na_criacao_e_na_edicao()
    {
        var (service, db) = Novo();
        var a = (await service.CriarColaboradorAsync(new("Ana", "Dev", null, "ana@x.com", null), default)).Valor!;
        var b = (await service.CriarColaboradorAsync(new("Beto", null, null, "beto@x.com", null), default)).Valor!;

        Assert.Equal(CrmErro.Conflito, (await service.CriarColaboradorAsync(new("Outra", null, null, "ana@x.com", null), default)).Erro);
        Assert.Equal(CrmErro.Conflito, (await service.AtualizarColaboradorAsync(b.Id, new("Beto", null, null, "ana@x.com", null), default)).Erro);

        // Salvar o próprio registro sem trocar o e-mail não é conflito.
        Assert.True((await service.AtualizarColaboradorAsync(a.Id, new("Ana Maria", "Dev", null, "ana@x.com", null), default)).Ok);
        Assert.Equal("Ana Maria", (await db.Colaboradores.SingleAsync(c => c.Id == a.Id)).Nome);
    }

    [Fact]
    public async Task Colaborador_exige_nome_e_email_e_exclusao_de_inexistente_devolve_false()
    {
        var (service, _) = Novo();

        Assert.Equal(CrmErro.Invalido, (await service.CriarColaboradorAsync(new("", null, null, "a@x.com", null), default)).Erro);
        Assert.Equal(CrmErro.Invalido, (await service.CriarColaboradorAsync(new("Ana", null, null, null, null), default)).Erro);
        Assert.False(await service.ExcluirColaboradorAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Contato_so_e_criado_para_cliente_existente_e_nao_troca_de_cliente()
    {
        var (service, db) = Novo();
        var cliente = new Cliente { Id = Guid.NewGuid(), Nome = "Acme", Email = "a@acme.com", CreatedAt = DateTimeOffset.UtcNow };
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();

        Assert.Equal(CrmErro.Invalido, (await service.CriarContatoAsync(new(Guid.NewGuid(), "Zé", null, null), default)).Erro);

        var contato = (await service.CriarContatoAsync(new(cliente.Id, "Zé", "11 9999", "ze@acme.com"), default)).Valor!;
        var editado = (await service.AtualizarContatoAsync(contato.Id, new(Guid.NewGuid(), "José", null, null), default)).Valor!;

        Assert.Equal(cliente.Id, editado.ClienteId);
        Assert.Equal("José", editado.Nome);
        Assert.Single(await service.ListarContatosAsync(cliente.Id, default));
        Assert.Empty(await service.ListarContatosAsync(Guid.NewGuid(), default));
        Assert.True(await service.ExcluirContatoAsync(contato.Id, default));
        Assert.False(await service.ExcluirContatoAsync(contato.Id, default));
    }
}
