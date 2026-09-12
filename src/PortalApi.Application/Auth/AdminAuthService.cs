using PortalApi.Domain.Entities;

namespace PortalApi.Application.Auth;

public class AdminAuthService(IAdminUserRepository repository, IPasswordHasher hasher)
{
    /// <summary>
    /// Verifica a senha contra todos os admins cadastrados (hoje só o Nelson — sem
    /// CRUD de múltiplos admins ainda, ver ticket #22). Custo irrelevante para N pequeno.
    /// </summary>
    public async Task<AdminUser?> VerifyLoginAsync(string password, CancellationToken ct)
    {
        var admins = await repository.GetAllAsync(ct);
        return admins.FirstOrDefault(admin => hasher.Verify(password, admin.PasswordHash));
    }
}
