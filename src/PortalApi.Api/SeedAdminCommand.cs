using Microsoft.EntityFrameworkCore;
using PortalApi.Application.Auth;
using PortalApi.Domain.Entities;
using PortalApi.Infrastructure.Data;

namespace PortalApi.Api;

/// <summary>
/// Bootstrap do primeiro admin — só CLI, nunca um endpoint HTTP (ticket #14). Uso:
/// dotnet run --project src/PortalApi.Api -- seed-admin email@exemplo.com
/// A senha é lida interativamente (mascarada), nunca como argumento — evita ficar no
/// histórico do shell.
/// </summary>
public static class SeedAdminCommand
{
    public static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        var email = args.ElementAtOrDefault(1);
        if (string.IsNullOrWhiteSpace(email))
        {
            Console.Error.WriteLine("Uso: dotnet run --project src/PortalApi.Api -- seed-admin <email>");
            return 1;
        }

        Console.Write("Senha: ");
        var password = ReadPasswordMasked();
        Console.Write("\nConfirme a senha: ");
        var confirmation = ReadPasswordMasked();
        Console.WriteLine();

        if (password != confirmation)
        {
            Console.Error.WriteLine("As senhas não coincidem.");
            return 1;
        }

        if (string.IsNullOrEmpty(password) || password.Length < 8)
        {
            Console.Error.WriteLine("A senha precisa ter pelo menos 8 caracteres.");
            return 1;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var existing = await db.AdminUsers.FirstOrDefaultAsync(a => a.Email == email);
        if (existing is not null)
        {
            existing.PasswordHash = hasher.Hash(password);
            Console.WriteLine($"Admin '{email}' já existia — senha atualizada.");
        }
        else
        {
            db.AdminUsers.Add(new AdminUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                PasswordHash = hasher.Hash(password),
                CreatedAt = DateTimeOffset.UtcNow,
            });
            Console.WriteLine($"Admin '{email}' criado.");
        }

        await db.SaveChangesAsync();
        return 0;
    }

    private static string ReadPasswordMasked()
    {
        var password = string.Empty;
        ConsoleKeyInfo key;
        do
        {
            key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password = password[..^1];
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password += key.KeyChar;
            }
        } while (key.Key != ConsoleKey.Enter);

        return password;
    }
}
