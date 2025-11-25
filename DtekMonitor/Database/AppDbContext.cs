using DtekMonitor.Models;
using Microsoft.EntityFrameworkCore;

namespace DtekMonitor.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Subscriber> Subscribers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Subscriber>(entity =>
        {
            entity.HasKey(e => e.ChatId);
            entity.Property(e => e.ChatId).ValueGeneratedNever();
            entity.Property(e => e.GroupName).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Username).HasMaxLength(100);
            entity.HasIndex(e => e.GroupName);
        });
    }
}


