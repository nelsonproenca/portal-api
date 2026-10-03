using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PortalApi.Api.Endpoints;
using PortalApi.Application.Auth;
using PortalApi.Application.Clientes;
using PortalApi.Application.Crm;
using PortalApi.Application.Email;
using PortalApi.Application.Uploads;
using PortalApi.Domain.Entities;
using PortalApi.Infrastructure.Data;
using PortalApi.Infrastructure.N8n;
using PortalApi.Infrastructure.Storage;

namespace PortalApi.Tests;

internal sealed class FakeEmail : IEmailService
{
    public List<(string To, string Subject, string Html)> Enviados { get; } = [];
    public bool Falha { get; set; }

    public Task SendAsync(string to, string subject, string html, CancellationToken ct)
    {
        if (Falha) throw new HttpRequestException("resend fora do ar");
        Enviados.Add((to, subject, html));
        return Task.CompletedTask;
    }
}

/// <summary>Relógio que só anda quando o teste manda.</summary>
internal sealed class FakeClock(DateTimeOffset inicio) : TimeProvider
{
    private DateTimeOffset _agora = inicio;
    public override DateTimeOffset GetUtcNow() => _agora;
    public void Avancar(TimeSpan t) => _agora += t;
}

/// <summary>Host de teste com o mesmo roteamento, políticas, rate limits e filtros da API, mas banco em memória,
/// n8n e e-mail falsos e uploads numa pasta temporária. O login do admin usa uma rota auxiliar só do teste (que
/// emite o cookie de verdade); o do cliente usa o fluxo real por link.</summary>
internal sealed class CrmApi : IAsyncDisposable
{
    public const string Segredo = "segredo-do-n8n";
    public const string PortalUrl = "https://portal.teste";

    private readonly WebApplication _app;
    private readonly string _uploadsRoot;
    public HttpClient Http { get; }
    public FakeNotificador N8n { get; }
    public FakeEmail Email { get; }
    public IServiceProvider Services => _app.Services;

    private CrmApi(WebApplication app, FakeNotificador n8n, FakeEmail email, string uploadsRoot, HttpClient http)
    { _app = app; N8n = n8n; Email = email; _uploadsRoot = uploadsRoot; Http = http; }

    /// <param name="kestrelReal">Sobe um Kestrel de verdade em porta local: o TestServer não implementa o limite de
    /// tamanho de corpo (IHttpMaxRequestBodySizeFeature), então só o Kestrel prova que o limite vale.</param>
    public static async Task<CrmApi> StartAsync(string? segredo = Segredo, bool kestrelReal = false)
    {
        var builder = WebApplication.CreateBuilder();
        if (kestrelReal) builder.WebHost.UseUrls("http://127.0.0.1:0");
        else builder.WebHost.UseTestServer();

        var uploadsRoot = Path.Combine(Path.GetTempPath(), "portal-uploads-" + Guid.NewGuid());
        var dbName = Guid.NewGuid().ToString();
        var s = builder.Services;
        s.AddDbContext<PortalDbContext>(o => o.UseInMemoryDatabase(dbName));
        s.AddScoped<ICrmRepository, CrmRepository>();
        s.AddScoped<LeadService>();
        s.AddScoped<AnaliseService>();
        s.AddScoped<AgendamentoService>();
        s.AddScoped<CadastroCrmService>();
        s.Configure<N8nOptions>(o => o.WebhookSecret = segredo);

        s.AddScoped<IClienteRepository, ClienteRepository>();
        s.AddScoped<ClienteService>();
        s.AddScoped<IClienteLoginTokenRepository, ClienteLoginTokenRepository>();
        s.AddScoped<ClienteLoginService>();
        s.AddSingleton(TimeProvider.System);
        s.Configure<ClienteLoginOptions>(o => o.PortalBaseUrl = PortalUrl);
        var email = new FakeEmail();
        s.AddSingleton<IEmailService>(email);

        s.Configure<StorageOptions>(o => { o.UploadsRoot = uploadsRoot; o.DownloadTokenSecret = "x"; });
        s.AddSingleton<IUploadStorage, LocalUploadStorage>();
        s.AddScoped<UploadService>();

        s.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = StatusCodes.Status401Unauthorized; return Task.CompletedTask; };
                o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = StatusCodes.Status403Forbidden; return Task.CompletedTask; };
            })
            .AddClienteCookie();
        s.AddAuthorization(o => o.AddPolicy("AdminOrClient", p => p
            .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme, ClienteAuthEndpoints.Scheme)
            .RequireAuthenticatedUser()));
        s.AddCrmPolicies();
        s.AddClienteAuthPolicies();

        var n8n = new FakeNotificador();
        s.AddSingleton<INotificadorN8n>(n8n);

        var app = builder.Build();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapPost("/_login/{papel}", async (string papel, HttpContext http) =>
        {
            await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, papel)], CookieAuthenticationDefaults.AuthenticationScheme)));
            return Results.Ok();
        });
        // Escrita qualquer sob a política AdminOrClient, só para provar o CSRF da sessão de cliente.
        app.MapPost("/_cliente-escreve", () => Results.Ok())
            .AddEndpointFilter<PortalApi.Api.Filters.AdminCsrfEndpointFilter>()
            .RequireAuthorization("AdminOrClient");
        app.MapCrmEndpoints();
        app.MapClienteEndpoints();
        app.MapClienteAuthEndpoints();
        app.MapUploadEndpoints();
        await app.StartAsync();

        var http = kestrelReal
            ? new HttpClient { BaseAddress = new Uri(app.Urls.First()) }
            : app.GetTestClient();
        return new CrmApi(app, n8n, email, uploadsRoot, http);
    }

    /// <summary>Entra como o papel dado e devolve o valor do cookie para anexar nas chamadas seguintes.</summary>
    public async Task<string> LoginAsync(string papel)
    {
        var r = await Http.PostAsync($"/_login/{papel}", null);
        return r.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
    }

    public async Task SeedClienteAsync(string emailCliente, string nome = "Acme")
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
        db.Clientes.Add(new Cliente { Id = Guid.NewGuid(), Nome = nome, Email = emailCliente, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    /// <summary>Faz o fluxo real: pede o link, lê o token do e-mail enviado e o troca pela sessão.
    /// Devolve o cookie do cliente.</summary>
    public async Task<string> LoginClienteAsync(string emailCliente)
    {
        await Http.SendAsync(Req(HttpMethod.Post, "/auth/cliente/solicitar", new { email = emailCliente }));
        var token = TokenDoUltimoEmail();
        var r = await Http.SendAsync(Req(HttpMethod.Post, "/auth/cliente/verificar", new { token }));
        r.EnsureSuccessStatusCode();
        return r.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("portal_cliente=")).Split(';')[0];
    }

    public string TokenDoUltimoEmail()
    {
        var html = System.Net.WebUtility.HtmlDecode(Email.Enviados[^1].Html);
        var inicio = html.IndexOf("?token=", StringComparison.Ordinal) + "?token=".Length;
        var fim = html.IndexOf('"', inicio);
        return Uri.UnescapeDataString(html[inicio..fim]);
    }

    public static HttpRequestMessage Req(HttpMethod metodo, string url, object? corpo = null, string? cookie = null,
        bool csrf = false, string? segredo = null)
    {
        var req = new HttpRequestMessage(metodo, url);
        if (corpo is not null) req.Content = JsonContent.Create(corpo);
        if (cookie is not null) req.Headers.Add("Cookie", cookie);
        if (csrf) req.Headers.Add("X-Portal-Admin", "1");
        if (segredo is not null) req.Headers.Add(N8nNotificador.SecretHeader, segredo);
        return req;
    }

    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        await _app.DisposeAsync();
        if (Directory.Exists(_uploadsRoot)) Directory.Delete(_uploadsRoot, recursive: true);
    }
}
