using Microsoft.EntityFrameworkCore;
using PortalApi.Domain.Entities;

namespace PortalApi.Infrastructure.Data;

public class PortalDbContext(DbContextOptions<PortalDbContext> options) : DbContext(options)
{
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Projeto> Projetos => Set<Projeto>();
    public DbSet<Etapa> Etapas => Set<Etapa>();
    public DbSet<Artefato> Artefatos => Set<Artefato>();
    public DbSet<Pedido> Pedidos => Set<Pedido>();
    public DbSet<PedidoResposta> PedidoRespostas => Set<PedidoResposta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PortalDbContext).Assembly);
    }
}
