using Argus.Example.Data.Entity;
using Argus.Sync.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Argus.Example.Data;

public class ArgusDbContext
(
    DbContextOptions<ArgusDbContext> options,
    IConfiguration configuration
) : CardanoDbContext(options, configuration)
{
    public DbSet<TxOutputBySlot> TxOutputsBySlot => Set<TxOutputBySlot>();
    public DbSet<OrderBySlot> OrdersBySlot => Set<OrderBySlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TxOutputBySlot>(entity =>
        {
            entity.HasKey(e => new { e.Slot, e.TxHash, e.Index });
        });

        modelBuilder.Entity<OrderBySlot>(entity =>
        {
            entity.HasKey(e => new { e.Slot, e.TxHash, e.Index });
        });
    }
}