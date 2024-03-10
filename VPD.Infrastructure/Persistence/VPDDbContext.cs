using Microsoft.EntityFrameworkCore;
using VPD.Domain.UserAggregate;

namespace VPD.Infrastructure.Persistence;

public class VPDDbContext(DbContextOptions<VPDDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .ApplyConfigurationsFromAssembly(typeof(VPDDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}