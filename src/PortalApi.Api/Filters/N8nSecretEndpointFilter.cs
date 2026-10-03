using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using PortalApi.Infrastructure.N8n;

namespace PortalApi.Api.Filters;

/// <summary>
/// Protege os endpoints que só o n8n chama (callbacks com o resultado da IA e o agendamento do Instagram):
/// exige o header <c>X-Webhook-Secret</c> igual ao <c>N8n:WebhookSecret</c>. Sem segredo configurado, recusa tudo
/// (falha fechada), e a comparação é em tempo constante.
/// </summary>
public class N8nSecretEndpointFilter(IOptions<N8nOptions> options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var esperado = options.Value.WebhookSecret;
        var recebido = context.HttpContext.Request.Headers[N8nNotificador.SecretHeader].ToString();

        if (string.IsNullOrEmpty(esperado) || string.IsNullOrEmpty(recebido)
            || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(esperado), Encoding.UTF8.GetBytes(recebido)))
            return Results.Unauthorized();

        return await next(context);
    }
}
