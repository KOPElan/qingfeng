using Microsoft.EntityFrameworkCore;
using QingFeng.Models;

namespace QingFeng.Data;

public class QingFengDbContext : DbContext
{
    public QingFengDbContext(DbContextOptions<QingFengDbContext> options)
        : base(options)
    {
    }

    // New database design
    public DbSet<Application> Applications { get; set; } = null!;
    public DbSet<DockItem> DockItems { get; set; } = null!;
    public DbSet<SystemSetting> SystemSettings { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Application
        modelBuilder.Entity<Application>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AppId).IsUnique();
            entity.Property(e => e.AppId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Icon).HasMaxLength(200);
            entity.Property(e => e.IconColor).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Category).HasMaxLength(100);
        });

        // Configure DockItem
        modelBuilder.Entity<DockItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ItemId).IsUnique();
            entity.Property(e => e.ItemId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Icon).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Url).IsRequired().HasMaxLength(500);
            entity.Property(e => e.IconBackground).HasMaxLength(200);
            entity.Property(e => e.IconColor).HasMaxLength(50);
            entity.Property(e => e.AssociatedAppId).HasMaxLength(100);
        });

        // Configure SystemSetting
        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Value).IsRequired();
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.DataType).HasMaxLength(50);
        });
    }
}
