using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortalApi.Domain.Entities;

namespace PortalApi.Infrastructure.Data.Configurations;

public class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToTable("Pedidos");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Titulo).HasMaxLength(500).IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.Tipo).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Projeto>().WithMany().HasForeignKey(p => p.ProjetoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Etapa>().WithMany().HasForeignKey(p => p.EtapaId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => p.ProjetoId);
    }
}
