using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortalApi.Domain.Entities;

namespace PortalApi.Infrastructure.Data.Configurations;

public class EtapaConfiguration : IEntityTypeConfiguration<Etapa>
{
    public void Configure(EntityTypeBuilder<Etapa> builder)
    {
        builder.ToTable("Etapas");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Nome).HasMaxLength(200).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Projeto>()
            .WithMany()
            .HasForeignKey(e => e.ProjetoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ProjetoId);
    }
}
