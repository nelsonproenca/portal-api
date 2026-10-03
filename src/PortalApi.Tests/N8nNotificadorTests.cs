using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PortalApi.Application.Crm;
using PortalApi.Infrastructure.N8n;

namespace PortalApi.Tests;

public class N8nNotificadorTests
{
    private static readonly N8nLeadPayload Lead = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Ana", "Acme", "ana@acme.com", "email", "CRM");

    private static N8nNotificador Novo(StubHandler handler, N8nOptions? options = null) =>
        new(new HttpClient(handler), Options.Create(options ?? new N8nOptions { BaseUrl = "http://n8n:5678", WebhookSecret = "segredo-teste" }),
            NullLogger<N8nNotificador>.Instance);

    private static StubHandler Responde(HttpStatusCode status) => new(_ => Task.FromResult(new HttpResponseMessage(status)));

    [Fact]
    public async Task Envia_o_corpo_do_contrato_com_o_segredo_no_header_para_o_path_certo()
    {
        var handler = Responde(HttpStatusCode.OK);

        await Novo(handler).NotificarLeadAsync(Lead, default);

        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("http://n8n:5678/webhook/sitenpi-leads", request.RequestUri!.ToString());
        Assert.Equal("segredo-teste", request.Headers.GetValues(N8nNotificador.SecretHeader).Single());

        using var json = JsonDocument.Parse(body);
        var raiz = json.RootElement;
        Assert.Equal(Lead.Id.ToString(), raiz.GetProperty("id").GetString());
        Assert.Equal("CRM", raiz.GetProperty("desafioTecnico").GetString());
        Assert.False(raiz.TryGetProperty("record", out _)); // sem o envelope do Database Webhook
    }

    [Fact]
    public async Task Cada_aviso_vai_para_o_seu_webhook()
    {
        var handler = Responde(HttpStatusCode.OK);
        var n8n = Novo(handler);

        await n8n.NotificarPlaygroundAsync(new(Guid.NewGuid(), "arq", "texto"), default);
        await n8n.NotificarEnrichAsync(new(Guid.NewGuid(), "Acme", "Varejo"), default);

        Assert.Equal("/webhook/sitenpi-playground", handler.Calls[0].Request.RequestUri!.AbsolutePath);
        Assert.Equal("/webhook/sitenpi-enriquecer-empresa", handler.Calls[1].Request.RequestUri!.AbsolutePath);
        Assert.Contains("\"nomeEmpresa\":\"Acme\"", handler.Calls[1].Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Resposta_de_erro_do_n8n_nao_propaga(HttpStatusCode status)
    {
        await Novo(Responde(status)).NotificarLeadAsync(Lead, default);
    }

    [Fact]
    public async Task Rede_fora_do_ar_nao_propaga()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection refused"));

        await Novo(handler).NotificarLeadAsync(Lead, default);

        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task Timeout_nao_propaga_e_nao_espera_alem_do_limite()
    {
        // O handler atrasa 30 s respeitando o token: só termina cedo se o timeout de 1 s cancelar.
        var n8n = new N8nNotificador(new HttpClient(new DelayHandler()),
            Options.Create(new N8nOptions { BaseUrl = "http://n8n:5678", WebhookSecret = "s", TimeoutSeconds = 1 }),
            NullLogger<N8nNotificador>.Instance);

        var inicio = DateTime.UtcNow;
        await n8n.NotificarLeadAsync(Lead, default);

        Assert.True(DateTime.UtcNow - inicio < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Cancelamento_do_chamador_continua_propagando()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new N8nNotificador(new HttpClient(new DelayHandler()),
                Options.Create(new N8nOptions { BaseUrl = "http://n8n:5678", WebhookSecret = "s" }),
                NullLogger<N8nNotificador>.Instance).NotificarLeadAsync(Lead, cts.Token));
    }

    [Theory]
    [InlineData(null, "segredo")]
    [InlineData("http://n8n:5678", null)]
    [InlineData("", "")]
    public async Task Sem_configuracao_nao_chama_o_n8n(string? baseUrl, string? segredo)
    {
        var handler = Responde(HttpStatusCode.OK);

        await Novo(handler, new N8nOptions { BaseUrl = baseUrl, WebhookSecret = segredo }).NotificarLeadAsync(Lead, default);

        Assert.Empty(handler.Calls);
    }

    private sealed class DelayHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
