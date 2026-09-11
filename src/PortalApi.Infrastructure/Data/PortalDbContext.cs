using Microsoft.EntityFrameworkCore;

namespace PortalApi.Infrastructure.Data;

public class PortalDbContext(DbContextOptions<PortalDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PortalDbContext).Assembly);
    }
}
