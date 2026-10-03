using System.Security.Claims;

namespace PortalApi.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static bool IsAdmin(this ClaimsPrincipal user) =>
        user.HasClaim(ClaimTypes.Role, "admin");

    public static bool IsCliente(this ClaimsPrincipal user) =>
        user.HasClaim(ClaimTypes.Role, "cliente");

    /// <summary>E-mail do usuário: o cookie do admin e o do cliente carregam o claim de e-mail.</summary>
    public static string? GetEmail(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("email")?.Value;
}
