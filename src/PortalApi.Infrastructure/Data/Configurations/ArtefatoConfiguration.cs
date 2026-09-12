using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortalApi.Domain.Entities;

namespace PortalApi.Infrastructure.Data.Configurations;

public class ArtefatoConfiguration : IEntityTypeConfiguration<Artefato>
{
    public void Configure(EntityTypeBuilder<Artefato> builder)
    {
        builder.ToTable("Artefatos");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Nome).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Url).HasMaxLength(1000).IsRequired();
        builder.Property(a => a.UploadedBy).HasMaxLength(100).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.Tipo).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Projeto>()
            .WithMany()
            .HasForeignKey(a => a.ProjetoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Etapa>()
            .WithMany()
            .HasForeignKey(a => a.EtapaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => a.ProjetoId);
        builder.HasIndex(a => a.EtapaId);
    }
}
