using PortalApi.Api.Extensions;

namespace PortalApi.Api.Filters;

/// <summary>
/// Exige um header custom em toda request que muda estado sob a sessão do admin (cookie
/// httpOnly). Uma requisição forjada cross-site (CSRF) não consegue setar headers custom
/// sem passar pelo preflight de CORS — e a API não libera CORS pra nenhuma origem externa
/// (same-origin, roteada por path). Reutilizado por todos os endpoints de escrita do admin,
/// não só o de auth (ver spec, ticket #12).
///
/// Vale para quem autenticou por cookie: o admin e, desde que o login do cliente passou a ser
/// cookie (e não mais o Bearer do Supabase), o cliente também. O front manda o header nas
/// escritas nos dois casos.
/// </summary>
public class AdminCsrfEndpointFilter : IEndpointFilter
{
    public const string HeaderName = "X-Portal-Admin";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var user = context.HttpContext.User;
        var autenticadoPorCookie = user.IsAdmin() || user.IsCliente();
        if (autenticadoPorCookie && !context.HttpContext.Request.Headers.ContainsKey(HeaderName))
            return Results.Forbid();

        return await next(context);
    }
}
