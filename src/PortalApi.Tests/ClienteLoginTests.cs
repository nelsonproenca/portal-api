using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PortalApi.Application.Auth;
using PortalApi.Domain.Entities;
using PortalApi.Infrastructure.Data;

namespace PortalApi.Tests;

public class ClienteLoginServiceTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private sealed record Ambiente(ClienteLoginService Service, PortalDbContext Db, FakeEmail Email, FakeClock Clock);

    private static async Task<Ambiente> NovoAsync(string? baseUrl = "https://portal.teste", string clienteEmail = "ana@acme.com", string nome = "Ana")
    {
        var db = TestDb.New();
        db.Clientes.Add(new Cliente { Id = Guid.NewGuid(), Nome = nome, Email = clienteEmail, CreatedAt = Inicio });
        await db.SaveChangesAsync();

        var email = new FakeEmail();
        var clock = new FakeClock(Inicio);
        var service = new ClienteLoginService(new ClienteRepository(db), new ClienteLoginTokenRepository(db), email,
            Options.Create(new ClienteLoginOptions { PortalBaseUrl = baseUrl }), clock, NullLogger<ClienteLoginService>.Instance);
        return new(service, db, email, clock);
    }

    private static string TokenDo(FakeEmail email)
    {
        var html = System.Net.WebUtility.HtmlDecode(email.Enviados[^1].Html);
        var inicio = html.IndexOf("?token=", StringComparison.Ordinal) + "?token=".Length;
        return Uri.UnescapeDataString(html[inicio..html.IndexOf('"', inicio)]);
    }

    [Fact]
    public async Task Cliente_cadastrado_recebe_o_link_e_so_o_hash_do_token_vai_para_o_banco()
    {
        var a = await NovoAsync();

        await a.Service.SolicitarAsync("ana@acme.com", default);

        var (para, _, html) = Assert.Single(a.Email.Enviados);
        Assert.Equal("ana@acme.com", para);
        Assert.Contains("https://portal.teste/portal/entrar?token=", System.Net.WebUtility.HtmlDecode(html));

        var token = TokenDo(a.Email);
        var guardado = await a.Db.ClienteLoginTokens.SingleAsync();
        Assert.NotEqual(token, guardado.TokenHash);                       // o token em si nunca é gravado
        Assert.Equal(ClienteLoginService.Hash(token), guardado.TokenHash);
        Assert.Equal(Inicio.AddMinutes(15), guardado.ExpiresAt);
        Assert.Null(guardado.UsedAt);
        Assert.True(token.Length >= 43);                                  // 256 bits em base64url
    }

    [Theory]
    [InlineData("naoexiste@x.com")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("sem-arroba")]
    public async Task Email_desconhecido_ou_invalido_nao_envia_nem_grava_e_nao_lanca(string? informado)
    {
        var a = await NovoAsync();

        await a.Service.SolicitarAsync(informado, default);

        Assert.Empty(a.Email.Enviados);
        Assert.Empty(a.Db.ClienteLoginTokens);
    }

    [Fact]
    public async Task Email_e_normalizado_maiusculas_e_espacos()
    {
        var a = await NovoAsync();

        await a.Service.SolicitarAsync("  ANA@Acme.COM ", default);

        Assert.Equal("ana@acme.com", Assert.Single(a.Email.Enviados).To);
    }

    [Fact]
    public async Task Limite_de_3_links_por_e_mail_na_janela_de_15_minutos_e_depois_libera()
    {
        var a = await NovoAsync();

        for (var i = 0; i < 5; i++) await a.Service.SolicitarAsync("ana@acme.com", default);
        Assert.Equal(3, a.Email.Enviados.Count);

        a.Clock.Avancar(TimeSpan.FromMinutes(16));
        await a.Service.SolicitarAsync("ana@acme.com", default);
        Assert.Equal(4, a.Email.Enviados.Count);
    }

    [Fact]
    public async Task Verificar_consome_o_token_uma_unica_vez()
    {
        var a = await NovoAsync();
        await a.Service.SolicitarAsync("ana@acme.com", default);
        var token = TokenDo(a.Email);

        Assert.Equal("ana@acme.com", await a.Service.VerificarAsync(token, default));
        Assert.Null(await a.Service.VerificarAsync(token, default));
        Assert.NotNull((await a.Db.ClienteLoginTokens.SingleAsync()).UsedAt);
    }

    [Fact]
    public async Task Token_expirado_e_recusado()
    {
        var a = await NovoAsync();
        await a.Service.SolicitarAsync("ana@acme.com", default);
        var token = TokenDo(a.Email);

        a.Clock.Avancar(TimeSpan.FromMinutes(15)); // exatamente no vencimento já não vale

        Assert.Null(await a.Service.VerificarAsync(token, default));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("token-que-nunca-existiu")]
    public async Task Token_invalido_e_recusado(string? token)
    {
        var a = await NovoAsync();
        await a.Service.SolicitarAsync("ana@acme.com", default);

        Assert.Null(await a.Service.VerificarAsync(token, default));
    }

    [Fact]
    public async Task Token_gigante_e_recusado_sem_ir_ao_banco()
    {
        var a = await NovoAsync();
        Assert.Null(await a.Service.VerificarAsync(new string('a', 5000), default));
    }

    [Fact]
    public async Task Falha_no_envio_do_email_nao_propaga()
    {
        var a = await NovoAsync();
        a.Email.Falha = true;

        await a.Service.SolicitarAsync("ana@acme.com", default);

        Assert.Empty(a.Email.Enviados);
    }

    [Fact]
    public async Task Sem_PortalBaseUrl_nao_envia_e_nao_grava()
    {
        var a = await NovoAsync(baseUrl: "");

        await a.Service.SolicitarAsync("ana@acme.com", default);

        Assert.Empty(a.Email.Enviados);
        Assert.Empty(a.Db.ClienteLoginTokens);
    }

    [Fact]
    public async Task Nome_do_cliente_e_escapado_no_html_do_email()
    {
        var a = await NovoAsync(nome: "<script>alert(1)</script>");

        await a.Service.SolicitarAsync("ana@acme.com", default);

        var html = a.Email.Enviados.Single().Html;
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public async Task Tokens_vencidos_ha_mais_de_um_dia_sao_removidos_na_proxima_solicitacao()
    {
        var a = await NovoAsync();
        await a.Service.SolicitarAsync("ana@acme.com", default);
        a.Clock.Avancar(TimeSpan.FromDays(2));

        await a.Service.SolicitarAsync("ana@acme.com", default);

        var restantes = await a.Db.ClienteLoginTokens.ToListAsync();
        Assert.Single(restantes);
        Assert.True(restantes[0].CreatedAt > Inicio);
    }
}

public class ClienteLoginEndpointsTests
{
    [Fact]
    public async Task Solicitar_responde_igual_para_email_cadastrado_e_desconhecido()
    {
        await using var api = await CrmApi.StartAsync();
        await api.SeedClienteAsync("ana@acme.com");

        var conhecido = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/auth/cliente/solicitar", new { email = "ana@acme.com" }));
        var desconhecido = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/auth/cliente/solicitar", new { email = "outro@x.com" }));

        Assert.Equal(HttpStatusCode.OK, conhecido.StatusCode);
        Assert.Equal(conhecido.StatusCode, desconhecido.StatusCode);
        Assert.Equal(await conhecido.Content.ReadAsStringAsync(), await desconhecido.Content.ReadAsStringAsync());
        Assert.Single(api.Email.Enviados); // mas só o cadastrado recebeu e-mail
    }

    [Fact]
    public async Task Fluxo_completo_link_sessao_e_acesso_ao_proprio_cadastro()
    {
        await using var api = await CrmApi.StartAsync();
        await api.SeedClienteAsync("ana@acme.com", "Ana da Acme");

        var cookie = await api.LoginClienteAsync("ana@acme.com");

        var eu = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Get, "/auth/cliente/me", cookie: cookie));
        Assert.Equal(HttpStatusCode.OK, eu.StatusCode);
        Assert.Contains("ana@acme.com", await eu.Content.ReadAsStringAsync());

        var cadastro = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Get, "/clientes/me", cookie: cookie));
        Assert.Equal(HttpStatusCode.OK, cadastro.StatusCode);
        Assert.Contains("Ana da Acme", await cadastro.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cookie_de_sessao_e_httponly_e_o_link_nao_vale_duas_vezes()
    {
        await using var api = await CrmApi.StartAsync();
        await api.SeedClienteAsync("ana@acme.com");
        await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/auth/cliente/solicitar", new { email = "ana@acme.com" }));
        var token = api.TokenDoUltimoEmail();

        var primeiro = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/auth/cliente/verificar", new { token }));
        var segundo = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/auth/cliente/verificar", new { token }));

        Assert.Equal(HttpStatusCode.OK, primeiro.StatusCode);
        Assert.Contains("httponly", primeiro.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant());
        Assert.Equal(HttpStatusCode.Unauthorized, segundo.StatusCode);
        Assert.False(segundo.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Token_inventado_responde_401_sem_cookie()
    {
        await using var api = await CrmApi.StartAsync();

        var r = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/auth/cliente/verificar", new { token = "inventado" }));

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.False(r.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Sessao_de_cliente_nao_abre_nenhum_endpoint_do_admin()
    {
        await using var api = await CrmApi.StartAsync();
        await api.SeedClienteAsync("ana@acme.com");
        var cookie = await api.LoginClienteAsync("ana@acme.com");

        foreach (var url in new[] { "/leads", "/agendamentos", "/contatos-clientes", "/clientes" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Get, url, cookie: cookie))).StatusCode);

        var upload = CrmApi.Req(HttpMethod.Post, "/colaboradores", new { nome = "X", email = "x@x.com" }, cookie, csrf: true);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Http.SendAsync(upload)).StatusCode);
    }

    [Fact]
    public async Task Sessao_de_admin_nao_vale_como_cliente()
    {
        await using var api = await CrmApi.StartAsync();
        var admin = await api.LoginAsync("admin");

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Get, "/auth/cliente/me", cookie: admin))).StatusCode);
    }

    [Fact]
    public async Task Sem_login_as_rotas_do_cliente_respondem_401()
    {
        await using var api = await CrmApi.StartAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Http.GetAsync("/auth/cliente/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.Http.GetAsync("/clientes/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_expira_o_cookie_do_cliente()
    {
        await using var api = await CrmApi.StartAsync();
        await api.SeedClienteAsync("ana@acme.com");
        var cookie = await api.LoginClienteAsync("ana@acme.com");

        var r = await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/auth/cliente/logout", cookie: cookie));

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var setCookie = r.Headers.GetValues("Set-Cookie").Single().ToLowerInvariant();
        Assert.StartsWith("portal_cliente=;", setCookie);
        Assert.Contains("expires=thu, 01 jan 1970", setCookie);
    }

    [Fact]
    public async Task Pedir_o_link_e_limitado_a_5_por_15_minutos_por_ip()
    {
        await using var api = await CrmApi.StartAsync();

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/auth/cliente/solicitar", new { email = "a@a.com" }))).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/auth/cliente/solicitar", new { email = "a@a.com" }))).StatusCode);
    }

    [Fact]
    public async Task Escrita_com_sessao_de_cliente_exige_o_header_csrf()
    {
        await using var api = await CrmApi.StartAsync();
        await api.SeedClienteAsync("ana@acme.com");
        var cookie = await api.LoginClienteAsync("ana@acme.com");

        Assert.Equal(HttpStatusCode.Forbidden, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/_cliente-escreve", cookie: cookie))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await api.Http.SendAsync(CrmApi.Req(HttpMethod.Post, "/_cliente-escreve", cookie: cookie, csrf: true))).StatusCode);
    }
}
