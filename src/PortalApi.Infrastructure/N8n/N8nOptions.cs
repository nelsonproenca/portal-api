namespace PortalApi.Infrastructure.N8n;

public class N8nOptions
{
    public const string SectionName = "N8n";

    /// <summary>Endereço do n8n visto pelo container (http://n8n:5678, rede n8n_default). Vazio desliga os avisos.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Segredo compartilhado: o portal-api o envia nos webhooks e o n8n o devolve nos callbacks
    /// (header X-Webhook-Secret). Vazio desliga os avisos e recusa todo callback.</summary>
    public string? WebhookSecret { get; set; }

    public int TimeoutSeconds { get; set; } = 5;

    public string LeadsPath { get; set; } = "webhook/sitenpi-leads";
    public string PlaygroundPath { get; set; } = "webhook/sitenpi-playground";
    public string EnrichPath { get; set; } = "webhook/sitenpi-enriquecer-empresa";
}
