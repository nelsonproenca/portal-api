using Microsoft.EntityFrameworkCore;
using PortalApi.Application.Auth;
using PortalApi.Domain.Entities;

namespace PortalApi.Infrastructure.Data;

public class AdminUserRepository(PortalDbContext db) : IAdminUserRepository
{
    public async Task<IReadOnlyList<AdminUser>> GetAllAsync(CancellationToken ct) =>
        await db.AdminUsers.AsNoTracking().ToListAsync(ct);

    public Task<AdminUser?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.AdminUsers.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task UpdatePasswordHashAsync(Guid id, string newPasswordHash, CancellationToken ct)
    {
        var admin = await db.AdminUsers.FirstAsync(a => a.Id == id, ct);
        admin.PasswordHash = newPasswordHash;
        await db.SaveChangesAsync(ct);
    }
}
