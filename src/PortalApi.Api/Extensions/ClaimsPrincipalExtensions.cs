using System.Security.Claims;

namespace PortalApi.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static bool IsAdmin(this ClaimsPrincipal user) =>
        user.HasClaim(ClaimTypes.Role, "admin");

    /// <summary>E-mail do usuário — cobre tanto o cookie do admin quanto o JWT do
    /// Supabase (que às vezes chega como "email" cru, às vezes mapeado pro
    /// ClaimTypes.Email, dependendo da config de MapInboundClaims — mesma
    /// defensiva já usada no Watchtower).</summary>
    public static string? GetEmail(this ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("email")?.Value;
}
