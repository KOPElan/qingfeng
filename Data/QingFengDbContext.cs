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
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<FavoriteFolder> FavoriteFolders { get; set; } = null!;
    public DbSet<ScheduledTask> ScheduledTasks { get; set; } = null!;
    public DbSet<AnydropMessage> AnydropMessages { get; set; } = null!;
    public DbSet<AnydropAttachment> AnydropAttachments { get; set; } = null!;

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

        // Configure User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
        });

        // Configure FavoriteFolder
        modelBuilder.Entity<FavoriteFolder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Path).IsUnique();
            entity.HasIndex(e => e.Order);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Path).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Icon).IsRequired().HasMaxLength(100);
        });

        // Configure ScheduledTask
        modelBuilder.Entity<ScheduledTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TaskType);
            entity.HasIndex(e => e.NextRunTime);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.TaskType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Configuration).HasMaxLength(4000);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
        // Configure AnydropMessage
        modelBuilder.Entity<AnydropMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CreatedAt);
            entity.Property(e => e.MessageType).IsRequired().HasMaxLength(50);
        });

        // Configure AnydropAttachment
        modelBuilder.Entity<AnydropAttachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MessageId);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(200);
            entity.Property(e => e.AttachmentType).IsRequired().HasMaxLength(50);
            
            entity.HasOne(e => e.Message)
                .WithMany(m => m.Attachments)
                .HasForeignKey(e => e.MessageId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
