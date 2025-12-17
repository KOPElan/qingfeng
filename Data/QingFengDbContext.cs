using Microsoft.EntityFrameworkCore;
using QingFeng.Models;

namespace QingFeng.Data;

public class QingFengDbContext : DbContext
{
    public QingFengDbContext(DbContextOptions<QingFengDbContext> options)
        : base(options)
    {
    }

    public DbSet<ShortcutItem> Shortcuts { get; set; } = null!;
    public DbSet<HomeLayoutConfig> HomeConfigs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ShortcutItem
        modelBuilder.Entity<ShortcutItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ShortcutId).IsUnique();
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Icon).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Category).HasMaxLength(100);
        });

        // Configure HomeLayoutConfig
        modelBuilder.Entity<HomeLayoutConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Value).IsRequired();
        });
    }
}
