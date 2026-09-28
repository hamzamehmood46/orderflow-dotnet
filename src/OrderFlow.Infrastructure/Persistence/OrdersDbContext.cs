using Microsoft.EntityFrameworkCore;
using OrderFlow.Core.Domain;

namespace OrderFlow.Infrastructure.Persistence;

public class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.CustomerId).HasMaxLength(100).IsRequired();
            e.Property(o => o.IdempotencyKey).HasMaxLength(100).IsRequired();
            e.HasIndex(o => o.IdempotencyKey).IsUnique();
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            e.Ignore(o => o.Total);
            e.HasMany(o => o.Lines).WithOne().OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderLine>(e =>
        {
            e.HasKey(l => l.Id);
            e.Property(l => l.Sku).HasMaxLength(64).IsRequired();
            e.Property(l => l.UnitPrice).HasPrecision(18, 2);
        });
    }
}
