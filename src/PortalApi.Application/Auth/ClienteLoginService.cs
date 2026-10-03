using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PortalApi.Application.Clientes;
using PortalApi.Application.Email;
using PortalApi.Domain.Entities;

namespace PortalApi.Application.Auth;

public class ClienteLoginOptions
{
    public const string SectionName = "ClienteLogin";

    /// <summary>Endereço público do site, usado no link do e-mail (https://www.nelson-proenca-info.com.br).
    /// Vazio: nenhum e-mail é enviado (e o erro vai para o log).</summary>
    public string? PortalBaseUrl { get; set; }

    /// <summary>Rota do portal-web que recebe o token e chama POST /auth/cliente/verificar.</summary>
    public string LoginPath { get; set; } = "/portal/entrar";

    public int TtlMinutes { get; set; } = 15;

    /// <summary>Limite de links por e-mail dentro de <see cref="TtlMinutes"/>: evita usar o formulário para
    /// encher a caixa de entrada de alguém.</summary>
    public int MaxLinksPorEmail { get; set; } = 3;
}

public interface IClienteLoginTokenRepository
{
    Task AddAsync(ClienteLoginToken token, CancellationToken ct);
    Task<ClienteLoginToken?> FindByHashAsync(string tokenHash, CancellationToken ct);
    Task<int> CountDesdeAsync(string email, DateTimeOffset desde, CancellationToken ct);

    /// <summary>Marca como usado. False se outra requisição consumiu o mesmo token antes (uso único).</summary>
    Task<bool> TryMarcarUsadoAsync(ClienteLoginToken token, DateTimeOffset agora, CancellationToken ct);

    Task RemoverExpiradosAntesDeAsync(DateTimeOffset limite, CancellationToken ct);
}

/// <summary>
/// Login do portal do cliente sem senha (substitui o magic link do Supabase Auth): <see cref="SolicitarAsync"/>
/// manda um link de uso único para o e-mail de um cliente cadastrado, e <see cref="VerificarAsync"/> troca o
/// token por uma sessão. Nunca revela se um e-mail está ou não cadastrado.
/// </summary>
public class ClienteLoginService(
    IClienteRepository clientes,
    IClienteLoginTokenRepository tokens,
    IEmailService email,
    IOptions<ClienteLoginOptions> options,
    TimeProvider clock,
    ILogger<ClienteLoginService> logger)
{
    private readonly ClienteLoginOptions _opts = options.Value;

    /// <summary>Hash guardado no banco (SHA-256 hexadecimal em minúsculas).</summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>Sempre completa em silêncio: e-mail desconhecido, inválido, limite estourado ou falha no envio
    /// não aparecem para quem chamou (resposta igual nos dois casos).</summary>
    public async Task SolicitarAsync(string? emailInformado, CancellationToken ct)
    {
        var endereco = emailInformado?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(endereco) || endereco.Length > 320 || !endereco.Contains('@')) return;

        var cliente = await clientes.GetByEmailAsync(endereco, ct);
        if (cliente is null) return;

        if (string.IsNullOrWhiteSpace(_opts.PortalBaseUrl))
        {
            logger.LogError("ClienteLogin:PortalBaseUrl não configurado: link de acesso não enviado.");
            return;
        }

        var agora = clock.GetUtcNow();
        var janela = TimeSpan.FromMinutes(_opts.TtlMinutes);
        if (await tokens.CountDesdeAsync(endereco, agora - janela, ct) >= _opts.MaxLinksPorEmail) return;

        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        await tokens.AddAsync(new ClienteLoginToken
        {
            Id = Guid.NewGuid(),
            Email = endereco,
            TokenHash = Hash(token),
            CreatedAt = agora,
            ExpiresAt = agora + janela,
        }, ct);

        try
        {
            await email.SendAsync(endereco, "Seu acesso ao Portal do Cliente", MontarEmail(cliente.Nome, MontarLink(token)), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning("Falha ao enviar o link de acesso: {Erro}", ex.GetType().Name);
        }

        // Faxina oportunista: tokens vencidos há mais de um dia não servem para nada.
        await tokens.RemoverExpiradosAntesDeAsync(agora.AddDays(-1), ct);
    }

    /// <summary>Consome o token e devolve o e-mail do cliente, ou null se for inválido, vencido ou já usado.</summary>
    public async Task<string?> VerificarAsync(string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200) return null;

        var registro = await tokens.FindByHashAsync(Hash(token.Trim()), ct);
        var agora = clock.GetUtcNow();
        if (registro is null || registro.UsedAt is not null || registro.ExpiresAt <= agora) return null;

        return await tokens.TryMarcarUsadoAsync(registro, agora, ct) ? registro.Email : null;
    }

    private string MontarLink(string token) =>
        $"{_opts.PortalBaseUrl!.TrimEnd('/')}{_opts.LoginPath}?token={Uri.EscapeDataString(token)}";

    private string MontarEmail(string nome, string link)
    {
        var nomeSeguro = System.Net.WebUtility.HtmlEncode(nome);
        var linkSeguro = System.Net.WebUtility.HtmlEncode(link);
        return $"""
            <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;padding:24px;color:#222;">
              <h2 style="font-size:20px;">Olá, {nomeSeguro}</h2>
              <p style="font-size:14px;line-height:1.6;color:#444;">Use o botão abaixo para entrar no Portal do Cliente.
              O link vale por {_opts.TtlMinutes} minutos e só funciona uma vez.</p>
              <p style="margin:24px 0;"><a href="{linkSeguro}" style="background:#111;color:#fff;padding:12px 20px;border-radius:6px;text-decoration:none;font-size:14px;">Entrar no portal</a></p>
              <p style="font-size:12px;color:#888;">Se você não pediu este acesso, ignore este e-mail.</p>
            </div>
            """;
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
