namespace PortalApi.Application.Crm;

/// <summary>
/// Dispara os webhooks do n8n depois que o portal-api grava (substitui o Database Webhook do Supabase).
/// Nunca propaga falha: o registro já está salvo e o n8n fora do ar não pode derrubar o formulário do site.
/// </summary>
public interface INotificadorN8n
{
    Task NotificarLeadAsync(N8nLeadPayload payload, CancellationToken ct);
    Task NotificarPlaygroundAsync(N8nPlaygroundPayload payload, CancellationToken ct);
    Task NotificarEnrichAsync(N8nEnrichPayload payload, CancellationToken ct);
}
