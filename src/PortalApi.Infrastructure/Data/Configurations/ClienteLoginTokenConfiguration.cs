using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortalApi.Domain.Entities;

namespace PortalApi.Infrastructure.Data.Configurations;

public class ClienteLoginTokenConfiguration : IEntityTypeConfiguration<ClienteLoginToken>
{
    public void Configure(EntityTypeBuilder<ClienteLoginToken> builder)
    {
        builder.ToTable("ClienteLoginTokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Email).HasMaxLength(320).IsRequired();
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired(); // SHA-256 em hexadecimal
        builder.Property(t => t.ExpiresAt).IsRequired();
        builder.Property(t => t.CreatedAt).IsRequired();

        // Garante o uso único mesmo com dois cliques simultâneos: o UPDATE vira "... WHERE UsedAt IS NULL" e o
        // segundo recebe DbUpdateConcurrencyException.
        builder.Property(t => t.UsedAt).IsConcurrencyToken();

        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasIndex(t => new { t.Email, t.CreatedAt });
    }
}
