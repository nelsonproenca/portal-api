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

    /// <summary>Troca a própria senha (ticket #22) — não expõe CRUD de outros admins.</summary>
    public async Task<bool> ChangePasswordAsync(Guid adminId, string currentPassword, string newPassword, CancellationToken ct)
    {
        var admin = await repository.GetByIdAsync(adminId, ct);
        if (admin is null || !hasher.Verify(currentPassword, admin.PasswordHash))
            return false;

        var newHash = hasher.Hash(newPassword);
        await repository.UpdatePasswordHashAsync(adminId, newHash, ct);
        return true;
    }
}
