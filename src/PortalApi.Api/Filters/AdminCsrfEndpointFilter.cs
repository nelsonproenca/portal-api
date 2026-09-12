using PortalApi.Api.Extensions;

namespace PortalApi.Api.Filters;

/// <summary>
/// Exige um header custom em toda request que muda estado sob a sessão do admin (cookie
/// httpOnly). Uma requisição forjada cross-site (CSRF) não consegue setar headers custom
/// sem passar pelo preflight de CORS — e a API não libera CORS pra nenhuma origem externa
/// (same-origin, roteada por path). Reutilizado por todos os endpoints de escrita do admin,
/// não só o de auth (ver spec, ticket #12).
///
/// Só se aplica a quem autenticou via cookie (admin) — Bearer token (cliente, ticket #20)
/// não é vulnerável a CSRF do mesmo jeito (browser nunca anexa Authorization sozinho em
/// request cross-site), então não faz sentido exigir esse header dele.
/// </summary>
public class AdminCsrfEndpointFilter : IEndpointFilter
{
    public const string HeaderName = "X-Portal-Admin";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var isAdmin = context.HttpContext.User.IsAdmin();
        if (isAdmin && !context.HttpContext.Request.Headers.ContainsKey(HeaderName))
            return Results.Forbid();

        return await next(context);
    }
}
