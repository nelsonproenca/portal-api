using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PortalApi.Application.Crm;

namespace PortalApi.Infrastructure.N8n;

public class N8nNotificador(HttpClient http, IOptions<N8nOptions> options, ILogger<N8nNotificador> logger) : INotificadorN8n
{
    public const string SecretHeader = "X-Webhook-Secret";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task NotificarLeadAsync(N8nLeadPayload payload, CancellationToken ct) =>
        PostAsync(options.Value.LeadsPath, "lead", payload.Id, payload, ct);

    public Task NotificarPlaygroundAsync(N8nPlaygroundPayload payload, CancellationToken ct) =>
        PostAsync(options.Value.PlaygroundPath, "playground", payload.Id, payload, ct);

    public Task NotificarEnrichAsync(N8nEnrichPayload payload, CancellationToken ct) =>
        PostAsync(options.Value.EnrichPath, "enrich", payload.Id, payload, ct);

    /// <summary>Falha de rede, timeout ou resposta diferente de 2xx viram só um aviso no log: o registro já foi salvo.</summary>
    private async Task PostAsync<T>(string path, string tipo, Guid id, T payload, CancellationToken ct)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.BaseUrl) || string.IsNullOrWhiteSpace(o.WebhookSecret))
        {
            logger.LogWarning("n8n não configurado (N8n:BaseUrl/WebhookSecret): aviso de {Tipo} {Id} não enviado.", tipo, id);
            return;
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(o.TimeoutSeconds, 1, 30)));

            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(o.BaseUrl.TrimEnd('/') + "/"), path))
            {
                Content = JsonContent.Create(payload, options: Json),
            };
            request.Headers.Add(SecretHeader, o.WebhookSecret);

            using var response = await http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("n8n respondeu {Status} ao aviso de {Tipo} {Id}.", (int)response.StatusCode, tipo, id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Mensagem sem o corpo nem o segredo; o tipo da exceção basta para diagnosticar.
            logger.LogWarning("Falha ao avisar o n8n ({Tipo} {Id}): {Erro}", tipo, id, ex.GetType().Name);
        }
    }
}
