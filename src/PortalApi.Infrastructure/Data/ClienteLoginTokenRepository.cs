using Microsoft.EntityFrameworkCore;
using PortalApi.Application.Auth;
using PortalApi.Domain.Entities;

namespace PortalApi.Infrastructure.Data;

public class ClienteLoginTokenRepository(PortalDbContext db) : IClienteLoginTokenRepository
{
    public async Task AddAsync(ClienteLoginToken token, CancellationToken ct)
    {
        db.ClienteLoginTokens.Add(token);
        await db.SaveChangesAsync(ct);
    }

    public Task<ClienteLoginToken?> FindByHashAsync(string tokenHash, CancellationToken ct) =>
        db.ClienteLoginTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public Task<int> CountDesdeAsync(string email, DateTimeOffset desde, CancellationToken ct) =>
        db.ClienteLoginTokens.CountAsync(t => t.Email == email && t.CreatedAt >= desde, ct);

    public async Task<bool> TryMarcarUsadoAsync(ClienteLoginToken token, DateTimeOffset agora, CancellationToken ct)
    {
        token.UsedAt = agora;
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false; // outra requisição consumiu o token primeiro
        }
    }

    public async Task RemoverExpiradosAntesDeAsync(DateTimeOffset limite, CancellationToken ct)
    {
        var vencidos = await db.ClienteLoginTokens.Where(t => t.ExpiresAt < limite).ToListAsync(ct);
        if (vencidos.Count == 0) return;
        db.ClienteLoginTokens.RemoveRange(vencidos);
        await db.SaveChangesAsync(ct);
    }
}
