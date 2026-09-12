using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PortalApi.Domain.Entities;

namespace PortalApi.Infrastructure.Data.Configurations;

public class PedidoRespostaConfiguration : IEntityTypeConfiguration<PedidoResposta>
{
    public void Configure(EntityTypeBuilder<PedidoResposta> builder)
    {
        builder.ToTable("PedidoRespostas");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Texto).HasMaxLength(4000).IsRequired();
        builder.Property(r => r.ArquivoUrl).HasMaxLength(1000);
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasOne<Pedido>().WithMany().HasForeignKey(r => r.PedidoId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.PedidoId);
    }
}
