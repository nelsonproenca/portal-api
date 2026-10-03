using System.Net;
using System.Net.Http.Json;
using PortalApi.Application.Crm;

namespace PortalApi.Tests;

public class CrmEndpointsTests
{
    private static readonly object LeadValido = new { nome = "Ana", contato = "ana@acme.com", desafioTecnico = "Preciso de um CRM" };

    // ─── Público ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Lead_publico_responde_201_com_o_id_e_nao_exige_login()
    {
        await using var api = await CrmApi.StartAsync();

        var r = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/leads", LeadValido));

        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var id = (await r.Content.ReadFromJsonAsync<CriadoDto>())!.Id;
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(id, Assert.Single(api.N8n.Leads).Id); // e o n8n foi avisado do mesmo lead
    }

    [Fact]
    public async Task Lead_publico_invalido_responde_400_com_a_mensagem()
    {
        await using var api = await CrmApi.StartAsync();

        var r = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/leads", new { nome = "Ana" }));

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("obrigatório", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Envio_publico_e_limitado_a_10_por_minuto_por_ip()
    {
        await using var api = await CrmApi.StartAsync();

        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.Created, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/leads", LeadValido))).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/leads", LeadValido))).StatusCode);
    }

    [Fact]
    public async Task Corpo_publico_acima_de_32_KB_e_recusado()
    {
        await using var api = await CrmApi.StartAsync(kestrelReal: true);
        var grande = new { nome = "Ana", contato = "a@a.com", desafioTecnico = new string('x', 40 * 1024) };

        var r = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/leads", grande));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, r.StatusCode);
    }

    [Fact]
    public async Task Colaboradores_sao_publicos_para_leitura()
    {
        await using var api = await CrmApi.StartAsync();
        Assert.Equal(HttpStatusCode.OK, (await api.Http.GetAsync("/colaboradores")).StatusCode);
    }

    [Fact]
    public async Task Polling_de_id_desconhecido_responde_404_e_id_malformado_tambem()
    {
        await using var api = await CrmApi.StartAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await api.Http.GetAsync($"/enrich/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Http.GetAsync($"/playground/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Http.GetAsync("/enrich/nao-e-guid")).StatusCode);
    }

    // ─── Admin: só com cookie de admin ────────────────────────────────────────

    [Theory]
    [InlineData("GET", "/leads")]
    [InlineData("GET", "/agendamentos")]
    [InlineData("GET", "/contatos-clientes")]
    [InlineData("POST", "/colaboradores")]
    public async Task Admin_sem_cookie_responde_401(string metodo, string url)
    {
        await using var api = await CrmApi.StartAsync();

        var r = await api.Http.SendAsync(CrmApi.Req(new HttpMethod(metodo), url, metodo == "GET" ? null : new { }));

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/leads")]
    [InlineData("GET", "/agendamentos")]
    [InlineData("GET", "/contatos-clientes")]
    public async Task Admin_com_cookie_de_outro_papel_responde_403(string metodo, string url)
    {
        await using var api = await CrmApi.StartAsync();
        var cookie = await api.LoginAsync("cliente");

        var r = await api.Http.SendAsync(CrmApi.Req(new HttpMethod(metodo), url, cookie: cookie));

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Admin_le_leads_com_o_cookie_de_admin()
    {
        await using var api = await CrmApi.StartAsync();
        await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/leads", LeadValido));
        var cookie = await api.LoginAsync("admin");

        var r = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Get, "/leads", cookie: cookie));

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Single((await r.Content.ReadFromJsonAsync<List<LeadDto>>())!);
    }

    [Fact]
    public async Task Admin_escrita_exige_o_header_csrf()
    {
        await using var api = await CrmApi.StartAsync();
        var cookie = await api.LoginAsync("admin");
        var corpo = new { nome = "Ana", email = "ana@x.com" };

        var sem = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/colaboradores", corpo, cookie));
        var com = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/colaboradores", corpo, cookie, csrf: true));

        Assert.Equal(HttpStatusCode.Forbidden, sem.StatusCode);
        Assert.Equal(HttpStatusCode.Created, com.StatusCode);
    }

    [Fact]
    public async Task Admin_colaborador_com_email_repetido_responde_409()
    {
        await using var api = await CrmApi.StartAsync();
        var cookie = await api.LoginAsync("admin");
        var corpo = new { nome = "Ana", email = "ana@x.com" };

        await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/colaboradores", corpo, cookie, csrf: true));
        var r = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/colaboradores", corpo, cookie, csrf: true));

        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
    }

    [Fact]
    public async Task Admin_exclui_lead_com_cookie_e_csrf_e_so_ele()
    {
        await using var api = await CrmApi.StartAsync();
        var id = (await (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/leads", LeadValido))).Content.ReadFromJsonAsync<CriadoDto>())!.Id;
        var url = $"/leads/{id}";
        var admin = await api.LoginAsync("admin");
        await api.SeedClienteAsync("ana@acme.com");
        var cliente = await api.LoginClienteAsync("ana@acme.com");

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Delete, url))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Delete, url, cookie: cliente, csrf: true))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Delete, url, cookie: admin))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Delete, url, segredo: CrmApi.Segredo))).StatusCode); // segredo do n8n não exclui

        Assert.Equal(HttpStatusCode.NoContent, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Delete, url, cookie: admin, csrf: true))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Delete, url, cookie: admin, csrf: true))).StatusCode);
        var restantes = await (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Get, "/leads", cookie: admin))).Content.ReadFromJsonAsync<List<LeadDto>>();
        Assert.Empty(restantes!);
    }

    // ─── n8n: só com o segredo ────────────────────────────────────────────────

    [Fact]
    public async Task Callback_do_n8n_sem_segredo_ou_com_segredo_errado_responde_401_e_nao_grava()
    {
        await using var api = await CrmApi.StartAsync();
        var id = (await (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/leads", LeadValido))).Content.ReadFromJsonAsync<CriadoDto>())!.Id;
        var corpo = new { analiseIa = "invadido" };

        var sem = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Patch, $"/leads/{id}/analise", corpo));
        var errado = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Patch, $"/leads/{id}/analise", corpo, segredo: "outro"));
        var cookieAdmin = await api.LoginAsync("admin");
        var soAdmin = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Patch, $"/leads/{id}/analise", corpo, cookieAdmin, csrf: true));

        Assert.Equal(HttpStatusCode.Unauthorized, sem.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, errado.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, soAdmin.StatusCode); // cookie de admin não substitui o segredo
    }

    [Fact]
    public async Task Callback_do_n8n_com_o_segredo_grava_a_analise()
    {
        await using var api = await CrmApi.StartAsync();
        var id = (await (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/leads", LeadValido))).Content.ReadFromJsonAsync<CriadoDto>())!.Id;

        var r = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Patch, $"/leads/{id}/analise", new { analiseIa = "Lead quente" }, segredo: CrmApi.Segredo));

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("Lead quente", (await r.Content.ReadFromJsonAsync<LeadDto>())!.AnaliseIa);
    }

    [Fact]
    public async Task Ciclo_do_enrich_pelo_http_o_front_cria_faz_polling_e_o_n8n_responde()
    {
        await using var api = await CrmApi.StartAsync();

        var criado = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/enrich", new { nomeEmpresa = "Acme", segmento = "Varejo" }));
        var id = (await criado.Content.ReadFromJsonAsync<CriadoDto>())!.Id;
        Assert.False((await api.Http.GetFromJsonAsync<ResultadoEnrichDto>($"/enrich/{id}"))!.Pronto);

        var cb = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Patch, $"/enrich/{id}/resultado", new { outputAi = "Resumo" }, segredo: CrmApi.Segredo));
        Assert.Equal(HttpStatusCode.OK, cb.StatusCode);

        var depois = await api.Http.GetFromJsonAsync<ResultadoEnrichDto>($"/enrich/{id}");
        Assert.True(depois!.Pronto);
        Assert.Equal("Resumo", depois.OutputAi);
    }

    [Fact]
    public async Task Agendamento_do_instagram_so_com_o_segredo()
    {
        await using var api = await CrmApi.StartAsync();
        var corpo = new { clienteNome = "Bia", dataReuniao = DateTimeOffset.UtcNow.AddDays(2) };

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/agendamentos", corpo))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/agendamentos", corpo, segredo: CrmApi.Segredo))).StatusCode);
    }

    [Fact]
    public async Task Sem_segredo_configurado_todo_callback_e_recusado_mesmo_com_header_vazio()
    {
        await using var api = await CrmApi.StartAsync(segredo: null);

        var r = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/agendamentos",
            new { dataReuniao = DateTimeOffset.UtcNow }, segredo: ""));

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }
}
