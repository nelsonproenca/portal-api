using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortalApi.Domain.Entities;

namespace PortalApi.Infrastructure.Data.Configurations;

public class ProjetoConfiguration : IEntityTypeConfiguration<Projeto>
{
    public void Configure(EntityTypeBuilder<Projeto> builder)
    {
        builder.ToTable("Projetos");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Nome).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Categoria).HasMaxLength(120);
        builder.Property(p => p.ImagemCapaUrl).HasMaxLength(500);
        builder.Property(p => p.LinkUrl).HasMaxLength(500);
        builder.Property(p => p.CreatedAt).IsRequired();

        builder.Property(p => p.StatusPublico).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Visibilidade).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Cliente>()
            .WithMany()
            .HasForeignKey(p => p.ClienteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.ClienteId);
        builder.HasIndex(p => p.Visibilidade);
    }
}
