namespace PortalApi.Api.Filters;

/// <summary>
/// Exige um header custom em toda request que muda estado sob a sessão do admin (cookie
/// httpOnly). Uma requisição forjada cross-site (CSRF) não consegue setar headers custom
/// sem passar pelo preflight de CORS — e a API não libera CORS pra nenhuma origem externa
/// (same-origin, roteada por path). Reutilizado por todos os endpoints de escrita do admin,
/// não só o de auth (ver spec, ticket #12).
/// </summary>
public class AdminCsrfEndpointFilter : IEndpointFilter
{
    public const string HeaderName = "X-Portal-Admin";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (!context.HttpContext.Request.Headers.ContainsKey(HeaderName))
            return Results.Forbid();

        return await next(context);
    }
}
