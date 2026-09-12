using PortalApi.Domain.Entities;

namespace PortalApi.Application.Auth;

public interface IAdminUserRepository
{
    Task<IReadOnlyList<AdminUser>> GetAllAsync(CancellationToken ct);
    Task<AdminUser?> GetByIdAsync(Guid id, CancellationToken ct);
    Task UpdatePasswordHashAsync(Guid id, string newPasswordHash, CancellationToken ct);
}
