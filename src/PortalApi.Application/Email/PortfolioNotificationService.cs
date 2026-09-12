using Microsoft.Extensions.Logging;
using PortalApi.Application.Clientes;
using PortalApi.Application.Projetos;

namespace PortalApi.Application.Email;

/// <summary>
/// Porta da edge function `portfolio-notify-email` pro backend (ticket #21) — os
/// 4 eventos do Portal do Cliente. Chamado direto pelos serviços que disparam
/// cada evento (ArtefatoService, PedidoService), não mais pelo frontend.
/// Nunca lança: uma falha de e-mail não pode derrubar a operação que a
/// originou (mesma regra que já valia na edge function).
/// </summary>
public class PortfolioNotificationService(
    IEmailService email,
    IProjetoRepository projetos,
    IClienteRepository clientes,
    ILogger<PortfolioNotificationService> logger)
{
    private const string AdminEmail = "nelsonhaproenca@gmail.com";

    public Task NotifyNovoPedidoAsync(Guid projetoId, string titulo, CancellationToken ct) =>
        NotifyClienteAsync(projetoId, ct, nome => (
            $"Novo pedido no projeto {nome}",
            Wrap("Novo pedido", $"""
                <h2 style="font-size:20px;">Você tem um novo pedido em <strong>{nome}</strong></h2>
                <p style="font-size:14px;line-height:1.6;color:#444;">{titulo}</p>
                <p style="font-size:14px;color:#444;">Acesse o portal pra responder.</p>
                """)));

    public Task NotifyNovoArtefatoAsync(Guid projetoId, string nomeArtefato, CancellationToken ct) =>
        NotifyClienteAsync(projetoId, ct, nome => (
            $"Novo artefato disponível em {nome}",
            Wrap("Novo artefato", $"""
                <h2 style="font-size:20px;">Um novo artefato foi adicionado a <strong>{nome}</strong></h2>
                <p style="font-size:14px;line-height:1.6;color:#444;">{nomeArtefato}</p>
                <p style="font-size:14px;color:#444;">Acesse o portal pra visualizar.</p>
                """)));

    public Task NotifyPedidoRespondidoAsync(Guid projetoId, string detalhe, CancellationToken ct) =>
        NotifyAdminAsync(projetoId, ct, nome => (
            $"Cliente respondeu um pedido em {nome}",
            Wrap("Resposta recebida", $"""
                <h2 style="font-size:20px;">Novo retorno em <strong>{nome}</strong></h2>
                <p style="font-size:14px;line-height:1.6;color:#444;">{detalhe}</p>
                """)));

    public Task NotifyPedidoDecididoAsync(Guid projetoId, string detalhe, CancellationToken ct) =>
        NotifyAdminAsync(projetoId, ct, nome => (
            $"Decisão registrada em {nome}",
            Wrap("Decisão do cliente", $"""
                <h2 style="font-size:20px;">Novo retorno em <strong>{nome}</strong></h2>
                <p style="font-size:14px;line-height:1.6;color:#444;">{detalhe}</p>
                """)));

    private async Task NotifyClienteAsync(Guid projetoId, CancellationToken ct, Func<string, (string Subject, string Html)> build)
    {
        try
        {
            var projeto = await projetos.GetByIdAsync(projetoId, ct);
            if (projeto is null) return;

            var cliente = await clientes.GetByIdAsync(projeto.ClienteId, ct);
            if (cliente is null) return;

            var (subject, html) = build(projeto.Nome);
            await email.SendAsync(cliente.Email, subject, html, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao notificar cliente do projeto {ProjetoId} — não afeta a operação principal.", projetoId);
        }
    }

    private async Task NotifyAdminAsync(Guid projetoId, CancellationToken ct, Func<string, (string Subject, string Html)> build)
    {
        try
        {
            var projeto = await projetos.GetByIdAsync(projetoId, ct);
            var nome = projeto?.Nome ?? "(projeto)";

            var (subject, html) = build(nome);
            await email.SendAsync(AdminEmail, subject, html, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao notificar admin sobre o projeto {ProjetoId} — não afeta a operação principal.", projetoId);
        }
    }

    private static string Wrap(string title, string body) => $"""
        <!DOCTYPE html><html lang="pt-BR"><head><meta charset="utf-8" /><title>{title}</title></head>
        <body style="margin:0;padding:0;background:#ffffff;font-family:Arial,Helvetica,sans-serif;color:#1a1a1a;">
          <div style="max-width:560px;margin:0 auto;padding:32px 24px;">
            <div style="border-bottom:2px solid #333;padding-bottom:16px;margin-bottom:24px;">
              <h1 style="margin:0;font-size:18px;letter-spacing:0.1em;color:#1a1a1a;">PORTAL DO CLIENTE</h1>
            </div>
            {body}
            <div style="margin-top:32px;padding-top:16px;border-top:1px solid #e5e5e5;font-size:11px;color:#888;">
              Este é um e-mail automático. Não responda diretamente.
            </div>
          </div>
        </body></html>
        """;
}
