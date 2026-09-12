using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortalApi.Domain.Entities;

namespace PortalApi.Infrastructure.Data.Configurations;

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Clientes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Nome).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(320).IsRequired();
        builder.Property(c => c.Empresa).HasMaxLength(200);
        builder.Property(c => c.Segmento).HasMaxLength(120);
        builder.Property(c => c.SiteUrl).HasMaxLength(500);
        builder.Property(c => c.LogoUrl).HasMaxLength(500);
        builder.Property(c => c.Status).HasMaxLength(50);
        builder.Property(c => c.CreatedAt).IsRequired();

        // Não único: o histórico do Supabase já tinha e-mails duplicados em alguns
        // casos de teste; a busca por e-mail (magic-link do portal, ticket #19) já
        // lida com isso pegando o primeiro. Sem policy de DELETE aqui também —
        // mesma decisão deliberada já tomada no schema original (ver api.test.ts).
        builder.HasIndex(c => c.Email);
    }
}
